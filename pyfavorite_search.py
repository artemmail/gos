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
from sentence_transformers import SentenceTransformer

MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
DEFAULT_CONFIG_PATH = os.environ.get(
    "APPSETTINGS_PATH", "src/Zakupki.Fetcher/appsettings.json"
)


def build_odbc_connection_string(appsettings: Dict[str, Any]) -> str:
    connection = appsettings["ConnectionStrings"]["Default"]
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
        collecting_end = payload.get("collectingEndLimit")
        if not collecting_end:
            raise ValueError("collectingEndLimit is required")
        collecting_end_dt = datetime.fromisoformat(collecting_end.replace("Z", "+00:00"))
        top = max(1, int(payload.get("top", 20)))
        limit = max(top, int(payload.get("limit", 500)))
        expired_only = bool(payload.get("expiredOnly", False))
        return cls(
            user_id=payload["userId"],
            query=payload["query"].strip(),
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

    @property
    def model(self) -> SentenceTransformer:
        if self._model is None:
            self._model = SentenceTransformer(self._model_name)
        return self._model

    def _connect(self) -> pyodbc.Connection:
        conn = pyodbc.connect(self._connection_string)
        conn.autocommit = False
        return conn

    def _fetch_candidates(
        self,
        cursor: pyodbc.Cursor,
        limit: int,
        collecting_end_limit: datetime,
        expired_only: bool,
    ) -> List[Tuple[str, str]]:
        if expired_only:
            sql = """
            SELECT TOP (?)
                n.Id,
                e.Vector
            FROM [NoticeEmbeddings] AS e
            INNER JOIN [Notices] AS n ON n.Id = e.NoticeId
            WHERE e.Model = ?
              AND (n.CollectingEnd IS NULL OR n.CollectingEnd <= ?)
            ORDER BY n.UpdatedAt DESC
            """
        else:
            sql = """
            SELECT TOP (?)
                n.Id,
                e.Vector
            FROM [NoticeEmbeddings] AS e
            INNER JOIN [Notices] AS n ON n.Id = e.NoticeId
            WHERE e.Model = ?
              AND (n.CollectingEnd IS NULL OR n.CollectingEnd >= ?)
            ORDER BY n.UpdatedAt DESC
            """
        cursor.execute(sql, limit, self._model_name, collecting_end_limit)
        return [(str(row.Id), row.Vector) for row in cursor.fetchall()]

    def _score(self, query: str, rows: Sequence[Tuple[str, str]]) -> List[Tuple[str, float]]:
        if not rows:
            return []
        query_vec = self.model.encode(query)
        vectors = np.array([json.loads(vector_json) for _, vector_json in rows], dtype=np.float32)
        query_norm = query_vec / (np.linalg.norm(query_vec) + 1e-12)
        matrix_norm = vectors / (np.linalg.norm(vectors, axis=1, keepdims=True) + 1e-12)
        sims = matrix_norm @ query_norm
        return list(zip((row_id for row_id, _ in rows), sims))

    def search(self, command: FavoriteSearchCommand) -> List[str]:
        with self._connect() as conn:
            cursor = conn.cursor()
            rows = self._fetch_candidates(
                cursor,
                command.limit,
                command.collecting_end_limit,
                command.expired_only,
            )
            scored = self._score(command.query, rows)
            top_sorted = sorted(scored, key=lambda x: x[1], reverse=True)[: command.top]
            return [notice_id for notice_id, _ in top_sorted]

    def add_to_favorites(self, command: FavoriteSearchCommand, notice_ids: Sequence[str]) -> int:
        if not notice_ids:
            return 0
        added = 0
        with self._connect() as conn:
            cursor = conn.cursor()
            for notice_id in notice_ids:
                cursor.execute(
                    "SELECT 1 FROM FavoriteNotices WHERE UserId = ? AND NoticeId = ?",
                    command.user_id,
                    notice_id,
                )
                if cursor.fetchone():
                    continue
                cursor.execute(
                    """
                    INSERT INTO FavoriteNotices (Id, NoticeId, UserId, CreatedAt)
                    VALUES (?, ?, ?, GETUTCDATE())
                    """,
                    str(uuid.uuid4()),
                    notice_id,
                    command.user_id,
                )
                added += 1
            conn.commit()
        return added


class RabbitFavoriteWorker:
    def __init__(self, config: Dict[str, Any]):
        event_bus = config.get("EventBus") or {}
        if not event_bus.get("Enabled", False):
            raise RuntimeError("Event bus disabled in configuration")
        self._event_bus = event_bus
        self._connection_params = pika.ConnectionParameters(
            host=event_bus["BusAccess"]["Host"],
            heartbeat=60,
            blocked_connection_timeout=120,
            credentials=pika.PlainCredentials(
                event_bus["BusAccess"]["UserName"], event_bus["BusAccess"]["Password"]
            ),
        )
        connection_string = build_odbc_connection_string(config)
        self._engine = FavoriteSearchEngine(connection_string)
        self._command_queue = event_bus["CommandQueueName"]
        self._exchange = event_bus["Broker"]
        self._exchange_type = event_bus.get("ExchangeType", "direct")
        self._logger = logging.getLogger("favorite_worker")

    def _declare(self, channel: pika.adapters.blocking_connection.BlockingChannel) -> None:
        channel.exchange_declare(exchange=self._exchange, exchange_type=self._exchange_type, durable=True)
        channel.queue_declare(queue=self._command_queue, durable=True)
        channel.queue_bind(queue=self._command_queue, exchange=self._exchange, routing_key=self._command_queue)
        channel.basic_qos(prefetch_count=1)

    def _process_message(self, body: bytes) -> Tuple[int, List[str]]:
        payload = json.loads(body.decode("utf-8"))
        command = FavoriteSearchCommand.from_payload(payload)
        notice_ids = self._engine.search(command)
        added = self._engine.add_to_favorites(command, notice_ids)
        return added, notice_ids

    def run(self) -> None:
        while True:
            try:
                with pika.BlockingConnection(self._connection_params) as connection:
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
            except pika.exceptions.AMQPConnectionError as exc:
                self._logger.error("RabbitMQ connection lost: %s", exc)
                time.sleep(5)
            except KeyboardInterrupt:
                self._logger.info("Stopping worker")
                break
            except Exception:
                self._logger.exception("Unexpected error")
                time.sleep(5)


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
