#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""RabbitMQ worker that executes semantic favorite searches.

The worker expects JSON commands produced by the .NET backend:

{
    "userId": "...",
    "query": "text",
    "collectingEndLimit": "2025-01-01T00:00:00Z",
    "expiredOnly": false,
    "top": 20,
    "limit": 500
}

NOTE: limit is ignored in SQL (no TOP) to avoid cutting result quality.
"""

from __future__ import annotations

import json
import logging
import os
import sys
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Sequence, Tuple

import numpy as np
import pika
import pyodbc
import torch
from sentence_transformers import SentenceTransformer

MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
DEFAULT_CONFIG_PATH = os.environ.get(
    "APPSETTINGS_PATH", "src/Zakupki.Fetcher/appsettings.json"
)


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
    connection = _get_connection_string(appsettings)
    server = database = username = password = None

    for part in connection.split(";"):
        if not part.strip():
            continue
        key, _, value = part.partition("=")
        key, value = key.strip().lower(), value.strip()

        if key in {"data source", "server", "address"}:
            server = value
        elif key in {"database", "initial catalog"}:
            database = value
        elif key in {"user id", "uid"}:
            username = value
        elif key in {"password", "pwd"}:
            password = value

    if not all([server, database, username, password]):
        raise RuntimeError("Неверная строка подключения: невозможно разобрать")

    return (
        "DRIVER={ODBC Driver 17 for SQL Server};"
        f"SERVER={server};DATABASE={database};UID={username};PWD={password};"
        "TrustServerCertificate=Yes;MARS_Connection=Yes"
    )


def _first_non_empty(*values: Optional[Any]) -> Optional[str]:
    for v in values:
        if v is None:
            continue
        s = str(v).strip()
        if s:
            return s
    return None


def _parse_bool(value: Optional[Any]) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return False
    return str(value).strip().lower() in {"1", "true", "yes", "y", "on"}


@dataclass
class FavoriteSearchCommand:
    user_id: str
    query: str
    collecting_end_limit: datetime
    expired_only: bool
    top: int
    limit: int

    @classmethod
    def from_payload(cls, payload: Dict[str, Any]) -> "FavoriteSearchCommand":
        def first(*keys, default=None):
            for k in keys:
                if k in payload:
                    return payload[k]
            return default

        query = first("query", "Query")
        if not query:
            raise ValueError("query is required")

        user_id = first("userId", "UserId")
        if not user_id:
            raise ValueError("userId is required")

        collecting_end = first("collectingEndLimit", "CollectingEndLimit")
        if collecting_end:
            dt = datetime.fromisoformat(str(collecting_end).replace("Z", "+00:00"))
            collecting_end_dt = dt.astimezone(timezone.utc)
        else:
            collecting_end_dt = datetime.fromtimestamp(0, tz=timezone.utc)

        expired_only = bool(first("expiredOnly", "ExpiredOnly", default=False))
        top = int(first("top", "Top", default=20))
        limit = int(first("limit", "Limit", default=500))

        return cls(
            user_id=str(user_id),
            query=str(query).strip(),
            collecting_end_limit=collecting_end_dt,
            expired_only=expired_only,
            top=top,
            limit=limit,
        )


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

    def _fetch_top_similar_notices(
        self,
        cursor: pyodbc.Cursor,
        query_vector: np.ndarray,
        top: int,
        collecting_end_limit: datetime,
        expired_only: bool,
    ) -> List[Tuple[str, str, str, str, float]]:
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
        SELECT TOP (?)
            n.Id,
            n.PurchaseNumber,
            n.EntryName,
            n.PurchaseObjectInfo,
            CAST(1.0 - COSINE_DISTANCE(e.Vector, ?) AS FLOAT) AS Similarity
        FROM NoticeEmbeddings e
        INNER JOIN Notices n ON n.Id = e.NoticeId
        WHERE {where_clause}
        ORDER BY Similarity DESC, n.UpdatedAt DESC
        """

        vector_bytes = self._serialize_vector(query_vector)
        params = [top, vector_bytes] + params

        print("\nSQL:")
        print(sql)
        print("PARAMS:", params)
        sys.stdout.flush()

        cursor.execute(sql, params)
        return [
            (
                str(r.Id),
                r.PurchaseNumber,
                r.EntryName,
                r.PurchaseObjectInfo,
                float(r.Similarity),
            )
            for r in cursor.fetchall()
        ]

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

                # Insert into favorites
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
            # если FK по AspNetUsers — логируем и СЧИТАЕМ, ЧТО СООБЩЕНИЕ ОБРАБОТАНО (просто без фаворитов)
            if "FK_FavoriteNotices_AspNetUsers_UserId" in msg:
                print(
                    f"\n[WARN] FK_FavoriteNotices_AspNetUsers_UserId: "
                    f"пользователь {cmd.user_id} не найден в AspNetUsers. "
                    f"Фавориты не сохранены, сообщение будет пропущено.\n"
                )
                sys.stdout.flush()
                return 0, []
            # любая другая SQL-ошибка пробрасывается выше, там решит воркер
            raise


class RabbitFavoriteWorker:
    def __init__(self, config: Dict[str, Any]):
        event_bus = config.get("EventBus") or {}

        bus_access = event_bus.get("BusAccess") or {}
        broker = _first_non_empty(
            os.environ.get("EVENTBUS_BROKER"),
            event_bus.get("Broker"),
        )

        command_queue = _first_non_empty(
            os.environ.get("EVENTBUS_COMMAND_QUEUE_NAME"),
            event_bus.get("CommandQueueName"),
            event_bus.get("QueueName"),
        )

        host = _first_non_empty(os.environ.get("EVENTBUS_HOST"), bus_access.get("Host"))
        user = _first_non_empty(
            os.environ.get("EVENTBUS_USERNAME"), bus_access.get("UserName")
        )
        pwd = _first_non_empty(
            os.environ.get("EVENTBUS_PASSWORD"), bus_access.get("Password")
        )

        self._connection_params = pika.ConnectionParameters(
            host=host,
            credentials=pika.PlainCredentials(user, pwd),
            heartbeat=60,
            blocked_connection_timeout=120.0,
        )

        conn_str = build_odbc_connection_string(config)
        self._engine = FavoriteSearchEngine(conn_str)

        self._queue = command_queue
        self._exchange = broker
        self._exchange_type = event_bus.get("ExchangeType", "direct")

        self._logger = logging.getLogger("favorite_worker")

    def _declare(self, ch):
        ch.exchange_declare(
            exchange=self._exchange,
            exchange_type=self._exchange_type,
            durable=True,
        )
        ch.queue_declare(queue=self._queue, durable=True)
        ch.queue_bind(
            queue=self._queue,
            exchange=self._exchange,
            routing_key=self._queue,
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
        command = FavoriteSearchCommand.from_payload(payload)
        return self._engine.process_command(command)

    def run(self):
        while True:
            connection = None
            try:
                connection = pika.BlockingConnection(self._connection_params)
                ch = connection.channel()
                self._declare(ch)

                def callback(ch, method, props, body):
                    self._logger.info("message received")
                    try:
                        self._process_message(body)
                    except Exception:
                        # ВАЖНО: НИКАКИХ NACK / REQUEUE
                        # Любая ошибка — логируем и ACK, сообщение выкидываем.
                        self._logger.exception(
                            "processing error, message will be skipped"
                        )
                    finally:
                        try:
                            ch.basic_ack(method.delivery_tag)
                        except Exception:
                            self._logger.exception("failed to ack message")

                ch.basic_consume(self._queue, callback)
                self._logger.info(
                    "Waiting for favorite search commands on queue '%s'...",
                    self._queue,
                )
                ch.start_consuming()

            except KeyboardInterrupt:
                self._logger.info("STOP")
                break
            except Exception:
                self._logger.exception("Unexpected error, reconnect in 5 seconds...")
                time.sleep(5)
            finally:
                if connection and connection.is_open:
                    try:
                        connection.close()
                    except Exception:
                        pass


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
    worker.run()


if __name__ == "__main__":
    main()
