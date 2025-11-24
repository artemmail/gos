#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""RabbitMQ worker that executes semantic favorite searches."""

from __future__ import annotations

import json
import logging
import os
import sys
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Any, Dict, List, Optional, Tuple
from urllib.parse import urlparse

import threading

import numpy as np
import pika
import pyodbc
import torch
from sentence_transformers import SentenceTransformer

MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
DEFAULT_CONFIG_PATH = os.environ.get(
    "APPSETTINGS_PATH", "src/Zakupki.Fetcher/appsettings.json"
)


# ---------- CONNECTION STRING HELPERS ----------


def _get_connection_string(config: Dict[str, Any]) -> str:
    connection_strings = config.get("ConnectionStrings") or {}
    overrides = [
        os.environ.get("ODBC_CONNECTION_STRING"),
        connection_strings.get("Default"),
        connection_strings.get("DefaultConnection"),
        connection_strings.get("DefaultConnectionString"),
    ]
    for candidate in overrides:
        if candidate:
            return candidate
    raise RuntimeError("Не найдена строка подключения в ConnectionStrings")


def build_odbc_connection_string(appsettings: Dict[str, Any]) -> str:
    """
    Берём строку подключения из appsettings и аккуратно превращаем в ODBC:
    - мапим Data Source/Server -> SERVER
    - Initial Catalog/Database -> DATABASE
    - User Id/Password -> UID/PWD
    - Integrated Security/Trusted_Connection -> Trusted_Connection=yes
    - нормализуем TrustServerCertificate и Encrypt
    - добавляем DRIVER, MARS_Connection
    """
    raw = _get_connection_string(appsettings)
    raw = raw.strip().rstrip(";")

    # Если уже полная ODBC-строка с DRIVER — почти не трогаем
    lowered_raw = raw.lower()
    if "driver=" in lowered_raw:
        odbc_conn = raw
    else:
        parts = [p for p in raw.split(";") if p.strip()]

        server: Optional[str] = None
        database: Optional[str] = None
        uid: Optional[str] = None
        pwd: Optional[str] = None
        encrypt: Optional[str] = None
        trust_server_cert: Optional[str] = None
        timeout: Optional[str] = None
        use_trusted_conn: bool = False

        for part in parts:
            if "=" not in part:
                continue
            key, value = part.split("=", 1)
            k = key.strip().lower()
            v = value.strip()

            if not k:
                continue

            # сервер
            if k in ("data source", "server", "addr", "address", "network address"):
                server = v
            # база
            elif k in ("initial catalog", "database"):
                database = v
            # SQL логин
            elif k in ("user id", "user", "uid"):
                uid = v
            elif k in ("password", "pwd"):
                pwd = v
            # интегрированная аутентификация
            elif k in ("integrated security", "trusted_connection"):
                if v.lower() in ("true", "yes", "sspi", "1"):
                    use_trusted_conn = True
            # шифрование
            elif k == "encrypt":
                encrypt = v
            # доверять сертификату
            elif k in ("trustservercertificate", "trust server certificate"):
                trust_server_cert = v
            # таймаут
            elif k in ("connection timeout", "connect timeout", "timeout"):
                timeout = v
            else:
                # остальные параметры игнорим, чтобы не было "Invalid connection string attribute"
                continue

        # ENV-оверрайд сервера
        env_server = os.environ.get("ODBC_SERVER")
        if env_server:
            server = env_server.strip()

        if not server:
            raise RuntimeError(
                "В строке подключения отсутствует сервер (Server/Data Source). "
                "Укажите ODBC_SERVER или исправьте ConnectionStrings.Default"
            )

        conn_parts: List[str] = [f"SERVER={server}"]

        if database:
            conn_parts.append(f"DATABASE={database}")

        if uid:
            conn_parts.append(f"UID={uid}")
        if pwd:
            conn_parts.append(f"PWD={pwd}")

        # Если SQL-логина нет, но есть Integrated Security — включаем Trusted_Connection
        if not uid and not pwd and use_trusted_conn:
            conn_parts.append("Trusted_Connection=yes")

        # Encrypt: по умолчанию включаем, как в index.py
        if encrypt:
            conn_parts.append(f"Encrypt={encrypt}")
        else:
            conn_parts.append("Encrypt=yes")

        if trust_server_cert:
            vl = trust_server_cert.strip().lower()
            if vl in ("true", "yes", "1"):
                tsc = "yes"
            elif vl in ("false", "no", "0"):
                tsc = "no"
            else:
                tsc = trust_server_cert
            conn_parts.append(f"TrustServerCertificate={tsc}")

        if timeout:
            conn_parts.append(f"Connection Timeout={timeout}")

        odbc_conn = ";".join(conn_parts)

    # Добавим DRIVER, если его нет
    lowered = odbc_conn.lower()
    if "driver=" not in lowered:
        odbc_conn = f"DRIVER={{ODBC Driver 17 for SQL Server}};{odbc_conn}"

    # Корректный MARS и TrustServerCertificate, если вдруг отсутствуют
    lowered = odbc_conn.lower()
    if "mars_connection" not in lowered:
        odbc_conn += ";MARS_Connection=Yes"
    if "trustservercertificate" not in lowered:
        odbc_conn += ";TrustServerCertificate=Yes"

    print("\n[DEBUG] ODBC connection string used:")
    print(odbc_conn)
    sys.stdout.flush()

    return odbc_conn



def _first_non_empty(*values: Optional[Any]) -> Optional[str]:
    for v in values:
        if v is None:
            continue
        s = str(v).strip()
        if s:
            return s
    return None


@dataclass
class BatchVectorItem:
    id: str
    text: str


@dataclass
class BatchVectorRequest:
    service_id: str
    items: List[BatchVectorItem]

    @classmethod
    def from_payload(cls, payload: Any) -> "BatchVectorRequest":
        if isinstance(payload, list):
            # Старый формат: просто список объектов {id, string}
            service_id = "unknown-service"
            items_payload = payload
        elif isinstance(payload, dict):
            service_id = _first_non_empty(
                payload.get("serviceId"),
                payload.get("ServiceId"),
                payload.get("service_id"),
            )
            items_payload = payload.get("items") or payload.get("Items")
        else:
            raise ValueError("payload must be an object or array")

        if not isinstance(items_payload, list) or not items_payload:
            raise ValueError("items must be a non-empty array")

        items: List[BatchVectorItem] = []
        for idx, item in enumerate(items_payload):
            if not isinstance(item, dict):
                raise ValueError(f"item {idx} is not an object")

            item_id = _first_non_empty(item.get("id"), item.get("Id"))
            text = _first_non_empty(
                item.get("string"),
                item.get("text"),
                item.get("query"),
                item.get("Query"),
            )

            if not item_id:
                raise ValueError(f"item {idx} is missing id")
            if not text:
                raise ValueError(f"item {idx} is missing text")

            items.append(BatchVectorItem(id=str(item_id), text=str(text)))

        return cls(service_id=str(service_id or "unknown-service"), items=items)


# ---------- ENGINE ----------


class FavoriteSearchEngine:
    def __init__(self, connection_string: str):
        self._connection_string = connection_string
        self._model: Optional[SentenceTransformer] = None
        self._device = "cuda" if torch.cuda.is_available() else "cpu"

    @property
    def model(self):
        if self._model is None:
            print(f"Loading model {MODEL_NAME} on {self._device}...")
            sys.stdout.flush()
            self._model = SentenceTransformer(MODEL_NAME, device=self._device)
        return self._model

    def _connect(self):
        conn = pyodbc.connect(self._connection_string)
        conn.autocommit = False
        return conn

    def _serialize_vector(self, vector: np.ndarray) -> bytes:
        return np.asarray(vector, dtype=np.float64).tobytes()


    def _to_numpy_vector(self, value: Any) -> np.ndarray:
        """Converts various DB-returned vector formats to a numpy array.

        SQL Server may return VECTOR columns either as raw bytes/memoryview or
        as a JSON string representation. Handle both cases and fall back to
        list/tuple.
        """

        if isinstance(value, (bytes, bytearray, memoryview)):
            buffer = memoryview(value)
            return np.frombuffer(buffer, dtype=np.float64)

        if isinstance(value, str):
            try:
                parsed = json.loads(value)
            except json.JSONDecodeError as exc:
                raise ValueError(f"Не удалось распарсить вектор из строки: {exc}") from exc
            return np.asarray(parsed, dtype=np.float64)

        if isinstance(value, (list, tuple)):
            return np.asarray(value, dtype=np.float64)

        try:
            return np.frombuffer(bytes(value), dtype=np.float64)
        except Exception as exc:  # pragma: no cover - safety net for unexpected types
            raise TypeError(f"Неизвестный тип вектора: {type(value)!r}") from exc

    def encode_text(self, text: str) -> List[float]:
        vector = self.model.encode([text], convert_to_numpy=True)[0]
        return np.asarray(vector, dtype=np.float32).tolist()

    def encode_batch(self, texts: List[str]) -> List[List[float]]:
        vectors = self.model.encode(texts, convert_to_numpy=True)
        vectors = np.asarray(vectors, dtype=np.float32)
        return [v.tolist() for v in vectors]


    def _fetch_top_similar_notices(
        self,
        cursor: pyodbc.Cursor,
        query_vector: np.ndarray,
        top: int,
        collecting_end_limit: datetime,
        expired_only: bool,
    ) -> List[Tuple[str, str, str, str, float]]:
        """
        Забираем из БД все (отфильтрованные) вектора и считаем COSINE similarity в Python.
        Это аналогично тому, как делалось в CLI-скрипте, только оформлено под worker.
        """
        filters = ["e.Model = ?"]
        params: List[Any] = [MODEL_NAME]

        if collecting_end_limit:
            if expired_only:
                filters.append("(n.CollectingEnd IS NULL OR n.CollectingEnd <= ?)")
            else:
                filters.append("(n.CollectingEnd IS NULL OR n.CollectingEnd >= ?)")
            params.append(collecting_end_limit)

        where_clause = " AND ".join(filters)

        sql = f"""
        SELECT
            n.Id,
            n.PurchaseNumber,
            n.EntryName,
            n.PurchaseObjectInfo,
            e.Vector,
            n.UpdatedAt
        FROM NoticeEmbeddings e
        INNER JOIN Notices n ON n.Id = e.NoticeId
        WHERE {where_clause}
        """

        print("\nSQL:")
        print(sql)
        print("PARAMS:", params)
        sys.stdout.flush()

        cursor.execute(sql, params)
        rows = cursor.fetchall()

        # Косинусное сходство в Python
        q = np.asarray(query_vector, dtype=np.float64)
        q_norm = float(np.linalg.norm(q)) or 1.0

        scored: List[Tuple[str, str, str, str, float, datetime]] = []

        for r in rows:
            v = self._to_numpy_vector(r.Vector)
            if v.size == 0:
                sim = 0.0
            else:
                v_norm = float(np.linalg.norm(v)) or 1.0
                sim = float(np.dot(q, v) / (q_norm * v_norm))

            scored.append(
                (
                    str(r.Id),
                    r.PurchaseNumber,
                    r.EntryName,
                    r.PurchaseObjectInfo,
                    sim,
                    r.UpdatedAt,
                )
            )

        # сортируем по similarity (desc), затем по UpdatedAt (desc) как было в SQL
        scored.sort(key=lambda x: (x[4], x[5] or datetime.min.replace(tzinfo=timezone.utc)), reverse=True)

        # возвращаем только нужное количество и без UpdatedAt
        return [(i, pn, name, obj, sim) for (i, pn, name, obj, sim, _) in scored[:top]]


    def process_command(self, cmd: FavoriteSearchCommand):
        print("\n=== FAVORITE SEARCH REQUEST ===")
        print(f"query={cmd.query}")
        print(f"userId={cmd.user_id}")
        print(f"top={cmd.top}")
        print(f"collectingEndLimit={cmd.collecting_end_limit}")
        print(f"expiredOnly={cmd.expired_only}")
        sys.stdout.flush()

        query_vec = self.model.encode([cmd.query], convert_to_numpy=True)[0]

        try:
            with self._connect() as conn:
                cursor = conn.cursor()

                rows = self._fetch_top_similar_notices(
                    cursor,
                    query_vec,
                    cmd.top,
                    cmd.collecting_end_limit,
                    cmd.expired_only,
                )
                if not rows:
                    print("NO embeddings found after date filter.")
                    return 0, []

                print(f"\nTOP-{len(rows)} RESULTS:\n" + "=" * 80)
                for rank, (rid, purchase, entry, obj, score) in enumerate(rows, 1):
                    print(f"{rank}. score={score:.4f}")
                    print(f"   PurchaseNumber: {purchase}")
                    print(f"   NoticeId:       {rid}")
                    if obj:
                        print(f"   Object:         {obj}")
                    print(f"   EntryName:      {entry}")
                    print("-" * 80)

                insert_sql = """
                INSERT INTO FavoriteNotices (Id, NoticeId, UserId, CreatedAt)
                SELECT ?, ?, ?, ?
                WHERE NOT EXISTS (
                    SELECT 1 FROM FavoriteNotices WHERE UserId=? AND NoticeId=?
                )
                """

                added = 0
                notice_ids: List[str] = []
                now = datetime.utcnow()

                print("\nSaving favorites:\n" + "=" * 80)

                for rank, (rid, _, _, _, score) in enumerate(rows, 1):
                    notice_ids.append(rid)

                    fid = str(uuid.uuid4())
                    cursor.execute(
                        insert_sql,
                        fid,
                        rid,
                        cmd.user_id,
                        now,
                        cmd.user_id,
                        rid,
                    )

                    status = "ADDED" if cursor.rowcount > 0 else "EXISTS"
                    if status == "ADDED":
                        added += 1

                    print(f"{rank}. [{status}]  score={score:.4f}")
                    print(f"   NoticeId: {rid}")
                    print("-" * 80)

                conn.commit()
                print("\nFAVORITES SAVED.\n")
                return added, notice_ids

        except pyodbc.Error as e:
            msg = str(e)
            # Если FK по пользователю — пропускаем сообщение, просто без фаворитов
            if "FK_FavoriteNotices_AspNetUsers_UserId" in msg:
                print(
                    f"\n[WARN] FK_FavoriteNotices_AspNetUsers_UserId: "
                    f"пользователь {cmd.user_id} не найден в AspNetUsers. "
                    f"Фавориты не сохранены, сообщение будет пропущено.\n"
                )
                sys.stdout.flush()
                return 0, []
            raise


# ---------- HTTP SERVER FOR VECTORIZATION ----------


class _VectorRequestHandler(BaseHTTPRequestHandler):
    engine: FavoriteSearchEngine

    def _send_json(self, status: int, payload: Any):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path not in ("/", "/vector", "/vectors"):
            self._send_json(404, {"error": "not found"})
            return

        body_bytes = self._read_body()
        if not body_bytes:
            self._send_json(400, {"error": "empty request body"})
            return

        try:
            payload = json.loads(body_bytes.decode("utf-8"))
        except Exception:
            self._send_json(400, {"error": "invalid json"})
            return

        try:
            batch = BatchVectorRequest.from_payload(payload)
        except ValueError as exc:
            self._send_json(400, {"error": str(exc)})
            return

        texts = [item.text for item in batch.items]
        vectors = self.engine.encode_batch(texts)

        results: List[Dict[str, Any]] = []
        for item, vector in zip(batch.items, vectors):
            results.append({"id": item.id, "string": item.text, "vector": vector})

        response = {"serviceId": batch.service_id, "items": results}
        self._send_json(200, response)

    def _read_body(self) -> bytes:
        transfer_encoding = (self.headers.get("Transfer-Encoding") or "").lower()
        if transfer_encoding == "chunked":
            return self._read_chunked_body()

        try:
            content_length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            content_length = 0

        if content_length <= 0:
            return b""

        return self.rfile.read(content_length)

    def _read_chunked_body(self) -> bytes:
        body = bytearray()

        while True:
            line = self.rfile.readline()
            if not line:
                break

            try:
                chunk_size = int(line.strip().split(b";", 1)[0], 16)
            except ValueError:
                break

            if chunk_size == 0:
                # consume trailer headers if any
                while True:
                    trailer = self.rfile.readline()
                    if trailer in (b"\r\n", b"\n", b""):
                        break
                break

            body.extend(self.rfile.read(chunk_size))
            # read the trailing CRLF after each chunk
            self.rfile.read(2)

        return bytes(body)


class VectorHttpServer:
    def __init__(self, engine: FavoriteSearchEngine, port: int):
        self._engine = engine
        self._port = port
        self._thread: Optional[threading.Thread] = None
        self._httpd: Optional[HTTPServer] = None

    def start(self):
        handler = _VectorRequestHandler
        handler.engine = self._engine
        self._httpd = HTTPServer(("0.0.0.0", self._port), handler)
        self._thread = threading.Thread(target=self._httpd.serve_forever, daemon=True)
        self._thread.start()
        print(f"[DEBUG] HTTP vector server started on port {self._port}")
        sys.stdout.flush()

    def stop(self):
        if self._httpd:
            try:
                self._httpd.shutdown()
            except Exception:
                pass
        if self._thread:
            self._thread.join(timeout=2)


# ---------- RABBIT WORKER ----------


class RabbitFavoriteWorker:
    def __init__(self, config: Dict[str, Any]):
        event_bus = config.get("EventBus") or {}

        bus_access = event_bus.get("BusAccess") or {}
        broker = _first_non_empty(
            os.environ.get("EVENTBUS_BROKER"),
            event_bus.get("Broker"),
        )

        request_queue = _first_non_empty(
            os.environ.get("EVENTBUS_BATCH_VECTOR_REQUEST_QUEUE"),
            os.environ.get("EVENTBUS_COMMAND_QUEUE_NAME"),
            event_bus.get("BatchVectorRequestQueueName"),
            event_bus.get("CommandQueueName"),
            event_bus.get("QueueName"),
        )
        response_queue = _first_non_empty(
            os.environ.get("EVENTBUS_BATCH_VECTOR_RESPONSE_QUEUE"),
            event_bus.get("BatchVectorResponseQueueName"),
        )

        host = _first_non_empty(os.environ.get("EVENTBUS_HOST"), bus_access.get("Host"))
        user = _first_non_empty(
            os.environ.get("EVENTBUS_USERNAME"), bus_access.get("UserName")
        )
        pwd = _first_non_empty(
            os.environ.get("EVENTBUS_PASSWORD"), bus_access.get("Password")
        )

        if not host or not user or not pwd:
            raise RuntimeError(
                "Некорректные настройки EventBus.BusAccess (Host/UserName/Password)"
            )

        self._connection_params = pika.ConnectionParameters(
            host=host,
            credentials=pika.PlainCredentials(user, pwd),
            heartbeat=60,
            blocked_connection_timeout=120.0,
        )

        conn_str = build_odbc_connection_string(config)
        self._engine = FavoriteSearchEngine(conn_str)

        if not request_queue:
            raise RuntimeError("Не задана очередь запросов для batch-векторизации")

        self._request_queue = request_queue
        self._response_queue = response_queue or f"{self._request_queue}.response"
        self._exchange = broker or ""
        self._exchange_type = event_bus.get("ExchangeType", "direct")

        self._channel: Optional[pika.adapters.blocking_connection.BlockingChannel] = None
        self._logger = logging.getLogger("favorite_worker")

        print(
            f"\n[DEBUG] RabbitMQ: host={host}, user={user}, "
            f"exchange={self._exchange or '[default]'}, "
            f"request={self._request_queue}, response={self._response_queue}\n"
        )
        sys.stdout.flush()

    @property
    def engine(self) -> FavoriteSearchEngine:
        return self._engine

    def _declare(self, ch):
        if self._exchange:
            ch.exchange_declare(
                exchange=self._exchange,
                exchange_type=self._exchange_type,
                durable=True,
            )

        ch.queue_declare(queue=self._request_queue, durable=True)
        ch.queue_declare(queue=self._response_queue, durable=True)

        if self._exchange:
            ch.queue_bind(
                queue=self._request_queue,
                exchange=self._exchange,
                routing_key=self._request_queue,
            )
            ch.queue_bind(
                queue=self._response_queue,
                exchange=self._exchange,
                routing_key=self._response_queue,
            )

        ch.basic_qos(prefetch_count=1)

    def _process_message(self, body: bytes):
        try:
            text = body.decode("utf-8")
        except Exception:
            text = body.decode("utf-8", errors="replace")

        print("\n========== NEW MESSAGE ==========")
        print(text)
        sys.stdout.flush()

        payload = json.loads(text)
        batch = BatchVectorRequest.from_payload(payload)

        vectors = self._engine.encode_batch([item.text for item in batch.items])
        response_items = []
        for item, vector in zip(batch.items, vectors):
            response_items.append(
                {"id": item.id, "string": item.text, "vector": vector}
            )

        response_body = json.dumps(
            {"serviceId": batch.service_id, "items": response_items},
            ensure_ascii=False,
        ).encode("utf-8")

        ch = self._channel
        if ch is None:
            raise RuntimeError("Channel is not ready")

        ch.basic_publish(
            exchange=self._exchange,
            routing_key=self._response_queue,
            body=response_body,
            properties=pika.BasicProperties(
                delivery_mode=2,
                content_type="application/json",
            ),
        )

    def run(self):
        while True:
            connection = None
            try:
                connection = pika.BlockingConnection(self._connection_params)
                ch = connection.channel()
                self._channel = ch
                self._declare(ch)

                def callback(ch, method, props, body):
                    self._logger.info("message received")
                    try:
                        self._process_message(body)
                    except Exception:
                        # Любая ошибка -> лог и ACK (сообщение пропускаем)
                        self._logger.exception(
                            "processing error, message will be skipped"
                        )
                    finally:
                        try:
                            ch.basic_ack(method.delivery_tag)
                        except Exception:
                            self._logger.exception("failed to ack message")

                ch.basic_consume(self._request_queue, callback)
                self._logger.info(
                    "Waiting for batch vector requests on queue '%s'...",
                    self._request_queue,
                )
                ch.start_consuming()

            except KeyboardInterrupt:
                self._logger.info("STOP")
                break
            except Exception:
                self._logger.exception("Unexpected error, reconnect in 5 seconds...")
                time.sleep(5)
            finally:
                self._channel = None
                if connection and connection.is_open:
                    try:
                        connection.close()
                    except Exception:
                        pass


# ---------- ENTRY POINT ----------


def load_config(path: str):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def main():
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
        stream=sys.stdout,
    )

    config = load_config(DEFAULT_CONFIG_PATH)
    worker = RabbitFavoriteWorker(config)

    http_port = os.environ.get("FAVORITE_HTTP_PORT") or os.environ.get("HTTP_PORT")
    if not http_port:
        http_port = "8000"
        print(
            "[INFO] FAVORITE_HTTP_PORT/HTTP_PORT not set, starting vector server on "
            "default port 8000"
        )
        sys.stdout.flush()

    server: Optional[VectorHttpServer] = None
    try:
        port = int(http_port)
        server = VectorHttpServer(worker.engine, port)
        server.start()
    except Exception as exc:  # pragma: no cover - startup helper
        print(f"[WARN] Failed to start HTTP vector server on {http_port}: {exc}")
        sys.stdout.flush()

    worker.run()


if __name__ == "__main__":
    main()
