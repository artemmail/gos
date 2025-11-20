#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""RabbitMQ worker that returns embeddings for ad-hoc queries."""

from __future__ import annotations

import json
import logging
import os
import sys
import time
from typing import Any, Dict, Optional

import numpy as np
import pika
from sentence_transformers import SentenceTransformer

MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
DEFAULT_CONFIG_PATH = os.environ.get("APPSETTINGS_PATH", "src/Zakupki.Fetcher/appsettings.json")


def _first_non_empty(*values: Optional[Any]) -> Optional[str]:
    for v in values:
        if v is None:
            continue
        s = str(v).strip()
        if s:
            return s
    return None


def load_config(path: str) -> Dict[str, Any]:
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


class QueryVectorWorker:
    def __init__(self, config: Dict[str, Any]):
        event_bus = config.get("EventBus") or {}
        bus_access = event_bus.get("BusAccess") or {}

        self._request_queue = _first_non_empty(
            os.environ.get("EVENTBUS_QUERY_VECTOR_REQUEST_QUEUE"),
            event_bus.get("QueryVectorRequestQueueName"),
        )
        self._response_queue = _first_non_empty(
            os.environ.get("EVENTBUS_QUERY_VECTOR_RESPONSE_QUEUE"),
            event_bus.get("QueryVectorResponseQueueName"),
        )

        host = _first_non_empty(os.environ.get("EVENTBUS_HOST"), bus_access.get("Host"))
        user = _first_non_empty(os.environ.get("EVENTBUS_USERNAME"), bus_access.get("UserName"))
        pwd = _first_non_empty(os.environ.get("EVENTBUS_PASSWORD"), bus_access.get("Password"))

        if not host or not user or not pwd:
            raise RuntimeError(
                "Некорректные настройки EventBus.BusAccess (Host/UserName/Password)"
            )

        if not self._request_queue or not self._response_queue:
            raise RuntimeError("Не заданы очереди запросов и ответов для векторизации")

        self._connection_params = pika.ConnectionParameters(
            host=host,
            credentials=pika.PlainCredentials(user, pwd),
            heartbeat=60,
            blocked_connection_timeout=120.0,
        )

        self._model = SentenceTransformer(MODEL_NAME)
        self._logger = logging.getLogger("query_vector_worker")
        self._channel: Optional[pika.adapters.blocking_connection.BlockingChannel] = None

        print(
            f"\n[DEBUG] RabbitMQ: host={host}, user={user}, request={self._request_queue}, response={self._response_queue}\n"
        )
        sys.stdout.flush()

    def _declare(self, ch: pika.adapters.blocking_connection.BlockingChannel) -> None:
        ch.queue_declare(queue=self._request_queue, durable=True)
        ch.queue_declare(queue=self._response_queue, durable=True)
        ch.basic_qos(prefetch_count=1)

    def _process_message(self, body: bytes) -> None:
        try:
            payload = json.loads(body.decode("utf-8"))
        except Exception:
            self._logger.warning("Invalid message encoding, skipping")
            return

        request_id = payload.get("id") or payload.get("Id")
        query = payload.get("query") or payload.get("Query")

        if not request_id or not query:
            self._logger.warning("Request is missing id or query")
            return

        vector = self._model.encode(query, convert_to_numpy=True)
        if isinstance(vector, list):
            vector = np.asarray(vector)
        vector = np.asarray(vector, dtype=np.float32).tolist()

        # Отвечаем в том же стиле, что и NoticeEmbeddings/SQL-клиент:
        # поля с заглавной буквы, чтобы десериализация в C# прошла без настроек
        response = {"Id": request_id, "Vector": vector}
        body = json.dumps(response, ensure_ascii=False).encode("utf-8")

        ch = self._channel
        if ch is None:
            raise RuntimeError("Channel is not ready")

        ch.basic_publish(
            exchange="",
            routing_key=self._response_queue,
            body=body,
            properties=pika.BasicProperties(
                delivery_mode=2,
                content_type="application/json",
            ),
        )

    def run(self) -> None:
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
                        self._logger.exception("processing error, message will be skipped")
                    finally:
                        try:
                            ch.basic_ack(method.delivery_tag)
                        except Exception:
                            self._logger.exception("failed to ack message")

                ch.basic_consume(self._request_queue, callback)
                self._logger.info(
                    "Waiting for query vector commands on queue '%s'...",
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
                if connection and connection.is_open:
                    try:
                        connection.close()
                    except Exception:
                        pass


def main():
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
        stream=sys.stdout,
    )

    config = load_config(DEFAULT_CONFIG_PATH)
    worker = QueryVectorWorker(config)
    worker.run()


if __name__ == "__main__":
    main()
