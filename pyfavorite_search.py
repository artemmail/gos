#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""RabbitMQ worker that executes semantic favorite searches.

The worker expects JSON commands produced by the .NET backend with the
following shape::

    {
        "userId": "...",
        "query": "text",
        "collectingEndLimit": "2025-01-01T00:00:00Z",
        "expiredOnly": false,
        "top": 20,
        "limit": 500
    }

Configuration (connection strings + RabbitMQ settings) is read from the same
``appsettings.json`` file that the ASP.NET Core application uses. By default we
look for ``src/Zakupki.Fetcher/appsettings.json`` but the path can be
overridden via the ``APPSETTINGS_PATH`` environment variable.
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
    raise RuntimeError("Не найдена строка подключения в разделе ConnectionStrings")


def build_odbc_connection_string(appsettings: Dict[str, Any]) -> str:
    connection = _get_connection_string(appsettings)
    server = database = username = password = None
    for part in connection.split(";"):
        key, _, value = part.partition("=")
        key = key.strip().lower()
        value = value.strip()
        if key in {"data source", "server", "address"}:
            server = value
        elif key in {"database", "initial catalog"}:
            database = value
        elif key in {"user id", "uid"}:
            username = value
        elif key in {"password", "pwd"}:
            password = value
    if not all([server, database, username, password]):
        raise RuntimeError("Не удалось разобрать строку подключения из appsettings.json")
    return (
        "DRIVER={ODBC Driver 17 for SQL Server};"
        f"SERVER={server};DATABASE={database};UID={username};PWD={password};"
        "TrustServerCertificate=Yes;MARS_Connection=Yes"
    )


def _first_non_empty(*values: Optional[Any]) -> Optional[str]:
    for value in values:
        if value is None:
            continue
        if isinstance(value, str):
            candidate = value.strip()
        else:
            candidate = str(value).strip()
        if candidate:
            return candidate
    return None


def _parse_int(value: Optional[Any], default: int) -> int:
    try:
        if value is None:
            raise ValueError
        return int(float(value))
    except (TypeError, ValueError):
        return default


def _parse_float(value: Optional[Any], default: float) -> float:
    try:
        if value is None:
            raise ValueError
        return float(value)
    except (TypeError, ValueError):
        return default


def _parse_bool(value: Optional[Any]) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return False
    return str(value).strip().lower() in {"1", "true", "yes", "on", "y"}


@dataclass
class FavoriteSearchCommand:
    user_id: str
    query: str
    collecting_end_limit: datetime
    expired_only: bool = False
    top: int = 20
    limit: int = 500

    @classmethod
    def from_payload(cls, payload: Dict[str, Any]) -> "FavoriteSearchCommand":
        def first_present(*keys: str, default: Optional[Any] = None) -> Any:
            for key in keys:
                if key in payload:
                    return payload[key]
            return default

        collecting_end = first_present("collectingEndLimit", "CollectingEndLimit")
        if not collecting_end:
            raise ValueError("collectingEndLimit is required")
        collecting_end_dt = datetime.fromisoformat(collecting_end.replace("Z", "+00:00"))
        query = first_present("query", "Query")
        if not query or not str(query).strip():
            raise ValueError("query is required")
        user_id = first_present("userId", "UserId")
        if not user_id:
            raise ValueError("userId is required")
        top = max(1, int(first_present("top", "Top", default=20)))
        limit = max(top, int(first_present("limit", "Limit", default=500)))
        expired_only = bool(first_present("expiredOnly", "ExpiredOnly", default=False))
        return cls(
            user_id=str(user_id),
            query=str(query).strip(),
            collecting_end_limit=collecting_end_dt.astimezone(timezone.utc),
            expired_only=expired_only,
            top=top,
            limit=limit,
        )


class FavoriteSearchEngine:
    def __init__(self, connection_string: str, model_name: str = MODEL_NAME):
        self._connection_string = connection_string
        self._model_name = model_name
        self._model: Optional[SentenceTransformer] = None
        self._device = "cuda" if torch.cuda.is_available() else "cpu"

    @property
    def model(self) -> SentenceTransformer:
        if self._model is None:
            print(f"Загружаю модель {self._model_name} на устройстве {self._device}...")
            self._model = SentenceTransformer(self._model_name, device=self._device)
        return self._model

    def _connect(self) -> pyodbc.Connection:
        conn = pyodbc.connect(self._connection_string)
        conn.autocommit = False
        return conn

    def _fetch_notice_embeddings(
        self,
        cursor: pyodbc.Cursor,
        limit: int,
        collecting_end_limit: datetime,
        expired_only: bool,
    ) -> List[Tuple[str, str, str, str, str]]:
        top_clause = f"TOP ({limit}) " if limit and limit > 0 else ""
        filters = ["e.Model = ?"]
        params: List[Any] = [self._model_name]
        if collecting_end_limit:
            if expired_only:
                filters.append("(n.CollectingEnd IS NULL OR n.CollectingEnd <= ?)")
            else:
                filters.append("(n.CollectingEnd IS NULL OR n.CollectingEnd >= ?)")
            params.append(collecting_end_limit)

        where_clause = " AND ".join(filters)
        sql = f"""
        SELECT {top_clause}
            n.Id,
            n.PurchaseNumber,
            n.EntryName,
            n.PurchaseObjectInfo,
            e.Vector
        FROM [NoticeEmbeddings] AS e
        INNER JOIN [Notices] AS n ON n.Id = e.NoticeId
        WHERE {where_clause}
        ORDER BY n.UpdatedAt DESC
        """
        cursor.execute(sql, *params)
        rows = cursor.fetchall()
        result: List[Tuple[str, str, str, str, str]] = []
        for row in rows:
            result.append(
                (
                    str(row.Id),
                    row.PurchaseNumber,
                    row.EntryName,
                    row.PurchaseObjectInfo,
                    row.Vector,
                )
            )
        return result

    @staticmethod
    def _parse_vectors(rows: Sequence[Tuple[str, str, str, str, str]]) -> np.ndarray:
        vectors = [json.loads(vector_json) for *_, vector_json in rows]
        return np.array(vectors, dtype=np.float32)

    @staticmethod
    def _cosine_similarity(query_vec: np.ndarray, matrix: np.ndarray) -> np.ndarray:
        q = query_vec / (np.linalg.norm(query_vec) + 1e-12)
        matrix_norm = matrix / (np.linalg.norm(matrix, axis=1, keepdims=True) + 1e-12)
        return matrix_norm @ q

    @staticmethod
    def _print_top_results(
        rows: Sequence[Tuple[str, str, str, str, str]],
        sims: np.ndarray,
        top_indices: Sequence[int],
    ) -> None:
        print()
        print(f"ТОП-{len(top_indices)} результатов (перед сохранением в избранное):")
        print("=" * 80)
        for rank, idx in enumerate(top_indices, start=1):
            notice_id, purchase_number, entry_name, purchase_object_info, _ = rows[idx]
            score = sims[idx]
            print(f"{rank:2d}. score={score:.4f}")
            print(f"    ЕИС ID (PurchaseNumber): {purchase_number}")
            print(f"    NoticeId:               {notice_id}")
            if purchase_object_info:
                print(f"    Предмет закупки:        {purchase_object_info}")
            print(f"    EntryName:              {entry_name}")
            print("-" * 80)

    @staticmethod
    def _upsert_favorites(
        cursor: pyodbc.Cursor,
        rows: Sequence[Tuple[str, str, str, str, str]],
        sims: np.ndarray,
        top_indices: Sequence[int],
        user_id: str,
    ) -> Tuple[int, List[str]]:
        now = datetime.utcnow()
        insert_sql = """
        INSERT INTO [FavoriteNotices] (Id, NoticeId, UserId, CreatedAt)
        SELECT ?, ?, ?, ?
        WHERE NOT EXISTS (
            SELECT 1
            FROM [FavoriteNotices]
            WHERE UserId = ? AND NoticeId = ?
        )
        """

        print()
        print("Сохраняю в избранное:")
        print("=" * 80)

        added = 0
        notice_ids: List[str] = []
        for rank, idx in enumerate(top_indices, start=1):
            notice_id, purchase_number, entry_name, purchase_object_info, _ = rows[idx]
            score = sims[idx]
            favorite_id = uuid.uuid4()
            cursor.execute(
                insert_sql,
                str(favorite_id),
                notice_id,
                user_id,
                now,
                user_id,
                notice_id,
            )
            status = "УЖЕ ЕСТЬ"
            if cursor.rowcount and cursor.rowcount > 0:
                status = "ДОБАВЛЕНО"
                added += 1
            notice_ids.append(notice_id)
            print(f"{rank:2d}. [{status}] score={score:.4f}")
            print(f"    ЕИС ID (PurchaseNumber): {purchase_number}")
            print(f"    NoticeId:               {notice_id}")
            if purchase_object_info:
                print(f"    Предмет закупки:        {purchase_object_info}")
            print(f"    EntryName:              {entry_name}")
            print("-" * 80)
        return added, notice_ids

    def process_command(self, command: FavoriteSearchCommand) -> Tuple[int, List[str]]:
        print("Получены параметры команды для favorite search:")
        print(f"  query:             {command.query}")
        print(f"  userId:            {command.user_id}")
        print(f"  top:               {command.top}")
        print(f"  limit:             {command.limit}")
        print(f"  collectingEndLimit:{command.collecting_end_limit.isoformat()}")
        print(f"  expiredOnly:       {command.expired_only}")

        query_vec = self.model.encode(
            [command.query], convert_to_numpy=True, normalize_embeddings=False
        )[0]

        with self._connect() as conn:
            cursor = conn.cursor()
            try:
                rows = self._fetch_notice_embeddings(
                    cursor,
                    command.limit,
                    command.collecting_end_limit,
                    command.expired_only,
                )
                if not rows:
                    print(
                        "В базе нет эмбеддингов для указанной модели. Сначала запусти индексатор."
                    )
                    return 0, []

                print(
                    f"Загружено {len(rows)} векторов из БД, считаю косинусное сходство..."
                )

                matrix = self._parse_vectors(rows)
                sims = self._cosine_similarity(query_vec, matrix)

                top_k = min(command.top, len(rows))
                top_indices = np.argsort(-sims)[:top_k]

                self._print_top_results(rows, sims, top_indices)

                added, notice_ids = self._upsert_favorites(
                    cursor, rows, sims, top_indices, command.user_id
                )
                conn.commit()
                print()
                print("Избранное успешно сохранено (транзакция закоммичена).")
                return added, notice_ids
            except Exception:
                conn.rollback()
                print("ОШИБКА, транзакция откатена")
                raise


class RabbitFavoriteWorker:
    def __init__(self, config: Dict[str, Any]):
        event_bus = config.get("EventBus") or {}
        if not _parse_bool(event_bus.get("Enabled", True)):
            raise RuntimeError("Event bus disabled in configuration")
        self._event_bus = event_bus
        bus_access = event_bus.get("BusAccess") or {}
        broker = _first_non_empty(os.environ.get("EVENTBUS_BROKER"), event_bus.get("Broker"))
        if not broker:
            raise RuntimeError("Event bus exchange (Broker) не настроен")

        command_queue = _first_non_empty(
            os.environ.get("EVENTBUS_COMMAND_QUEUE_NAME"),
            event_bus.get("CommandQueueName"),
            event_bus.get("QueueName"),
        )
        if not command_queue:
            raise RuntimeError("Command queue name is not configured in appsettings.json")

        response_queue = _first_non_empty(
            os.environ.get("EVENTBUS_QUEUE_NAME"),
            event_bus.get("QueueName"),
        )

        heartbeat_seconds = max(
            1,
            _parse_int(
                os.environ.get("EVENTBUS_HEARTBEAT_SECONDS") or event_bus.get("HeartbeatSeconds"),
                60,
            ),
        )
        blocked_timeout = max(
            1.0,
            _parse_float(
                os.environ.get("EVENTBUS_BLOCKED_CONNECTION_TIMEOUT")
                or event_bus.get("BlockedConnectionTimeout"),
                120.0,
            ),
        )
        consumer_timeout_ms = max(
            0,
            _parse_int(
                os.environ.get("EVENTBUS_CONSUMER_TIMEOUT_MS"),
                event_bus.get("ConsumerTimeoutMs", 0),
            ),
        )
        retry_count_value = os.environ.get("EVENTBUS_RETRY_COUNT") or event_bus.get("RetryCount", 5)
        retry_count = max(0, _parse_int(retry_count_value, 5))
        retry_delay_seconds = max(
            1.0,
            _parse_float(os.environ.get("EVENTBUS_RETRY_DELAY"), event_bus.get("RetryDelaySeconds", 5.0)),
        )
        prefetch_count = max(
            1,
            _parse_int(os.environ.get("EVENTBUS_PREFETCH_COUNT"), event_bus.get("PrefetchCount", 1)),
        )

        host = _first_non_empty(os.environ.get("EVENTBUS_HOST"), bus_access.get("Host")) or "localhost"
        username = _first_non_empty(os.environ.get("EVENTBUS_USERNAME"), bus_access.get("UserName")) or "guest"
        password = _first_non_empty(os.environ.get("EVENTBUS_PASSWORD"), bus_access.get("Password")) or "guest"

        self._connection_params = pika.ConnectionParameters(
            host=host,
            heartbeat=heartbeat_seconds,
            blocked_connection_timeout=blocked_timeout,
            credentials=pika.PlainCredentials(username, password),
        )
        connection_string = build_odbc_connection_string(config)
        self._engine = FavoriteSearchEngine(connection_string)
        self._command_queue = command_queue
        self._response_queue = response_queue
        self._exchange = broker
        self._exchange_type = _first_non_empty(os.environ.get("EVENTBUS_EXCHANGE_TYPE"), event_bus.get("ExchangeType")) or "direct"
        self._prefetch_count = prefetch_count
        self._consumer_timeout_ms = consumer_timeout_ms
        self._retry_count = retry_count
        self._retry_delay_seconds = retry_delay_seconds
        self._logger = logging.getLogger("favorite_worker")
        self._logger.info(
            "favorite_worker configured host=%s exchange=%s command_queue=%s response_queue=%s consumer_timeout_ms=%s",
            host,
            self._exchange,
            self._command_queue,
            self._response_queue or "(none)",
            self._consumer_timeout_ms if self._consumer_timeout_ms > 0 else "off",
        )

    def _declare(self, channel: pika.adapters.blocking_connection.BlockingChannel) -> None:
        queue_arguments = None
        if self._consumer_timeout_ms > 0:
            queue_arguments = {"x-consumer-timeout": self._consumer_timeout_ms}
        channel.exchange_declare(exchange=self._exchange, exchange_type=self._exchange_type, durable=True)
        channel.queue_declare(queue=self._command_queue, durable=True, arguments=queue_arguments)
        channel.queue_bind(queue=self._command_queue, exchange=self._exchange, routing_key=self._command_queue)
        if self._response_queue:
            channel.queue_declare(queue=self._response_queue, durable=True, arguments=queue_arguments)
        channel.basic_qos(prefetch_count=self._prefetch_count)

    def _process_message(self, body: bytes) -> Tuple[int, List[str]]:
        payload = json.loads(body.decode("utf-8"))
        command = FavoriteSearchCommand.from_payload(payload)
        return self._engine.process_command(command)

    def run(self) -> None:
        attempts = 0
        while True:
            connection = None
            try:
                connection = pika.BlockingConnection(self._connection_params)
                attempts = 0
                channel = connection.channel()
                self._declare(channel)

                def callback(ch, method, properties, body):  # type: ignore[override]
                    try:
                        added, notice_ids = self._process_message(body)
                        self._logger.info(
                            "Processed favorite command: added=%s notices=%s",
                            added,
                            notice_ids,
                        )
                        ch.basic_ack(method.delivery_tag)
                    except (pyodbc.Error, pika.exceptions.AMQPError) as exc:
                        self._logger.exception("Transient error: %s", exc)
                        ch.basic_nack(method.delivery_tag, requeue=True)
                    except Exception:
                        self._logger.exception("Failed to process message")
                        ch.basic_ack(method.delivery_tag)

                channel.basic_consume(queue=self._command_queue, on_message_callback=callback)
                self._logger.info("Waiting for favorite search commands ...")
                channel.start_consuming()
            except KeyboardInterrupt:
                self._logger.info("Stopping worker")
                break
            except pika.exceptions.AMQPConnectionError as exc:
                attempts += 1
                max_attempts_text = self._retry_count if self._retry_count > 0 else "∞"
                self._logger.error(
                    "RabbitMQ connection lost: %s (attempt %s/%s)",
                    exc,
                    attempts,
                    max_attempts_text,
                )
                if self._retry_count > 0 and attempts >= self._retry_count:
                    self._logger.error("Maximum retry attempts exceeded")
                    raise
                time.sleep(self._retry_delay_seconds)
            except Exception:
                self._logger.exception("Unexpected error")
                time.sleep(self._retry_delay_seconds)
            finally:
                if connection is not None and getattr(connection, "is_open", False):
                    try:
                        connection.close()
                    except Exception:
                        pass


def load_config(path: str) -> Dict[str, Any]:
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
        stream=sys.stdout,
    )
    config_path = DEFAULT_CONFIG_PATH
    config = load_config(config_path)
    worker = RabbitFavoriteWorker(config)
    worker.run()


if __name__ == "__main__":
    main()
