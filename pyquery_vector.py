#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""RabbitMQ worker that returns embeddings for ad-hoc queries."""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
import time
from typing import Any, Dict, Optional

import numpy as np
import pika
from sentence_transformers import SentenceTransformer

MODEL_NAME = "ai-forever/ru-en-RoSBERTa"
DEFAULT_CONFIG_PATH = os.environ.get(
    "APPSETTINGS_PATH", "src/Zakupki.Fetcher/appsettings.json"
)


def _first_non_empty(*values: Optional[Any]) -> Optional[str]:
    """Вернуть первую непустую строку из списка значений."""
    for v in values:
        if v is None:
            continue
        s = str(v).strip()
        if s:
            return s
    return None


def load_config(path: str) -> Dict[str, Any]:
    if not os.path.exists(path):
        raise FileNotFoundError(f"Config file not found: {path}")

    with open(path, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    if not isinstance(cfg, dict):
        raise ValueError("Config root must be a JSON object")

    return cfg


class QueryVectorWorker:
    def __init__(self, config: Dict[str, Any]) -> None:
        event_bus = config.get("EventBus") or {}
        bus_access = event_bus.get("BusAccess") or {}

        # Имена очередей можно переопределить через переменные окружения
        self._request_queue = _first_non_empty(
            os.environ.get("EVENTBUS_QUERY_VECTOR_REQUEST_QUEUE"),
            event_bus.get("QueryVectorRequestQueueName"),
        )
        self._response_queue = _first_non_empty(
            os.environ.get("EVENTBUS_QUERY_VECTOR_RESPONSE_QUEUE"),
            event_bus.get("QueryVectorResponseQueueName"),
        )

        # Параметры доступа к RabbitMQ (EventBus)
        host = _first_non_empty(
            os.environ.get("EVENTBUS_HOST"),
            bus_access.get("Host"),
        )
        user = _first_non_empty(
            os.environ.get("EVENTBUS_USERNAME"),
            bus_access.get("UserName"),
        )
        pwd = _first_non_empty(
            os.environ.get("EVENTBUS_PASSWORD"),
            bus_access.get("Password"),
        )

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
            f"\n[DEBUG] RabbitMQ: host={host}, user={user}, "
            f"request={self._request_queue}, response={self._response_queue}\n"
        )
        sys.stdout.flush()

    # --------- Кодирование запросов ---------

    def encode_queries(self, queries: list[str]) -> list[list[float]]:
        """Закодировать список запросов одним батчевым вызовом модели."""
        if not queries:
            return []

        vectors = self._model.encode(queries, convert_to_numpy=True)

        if isinstance(vectors, list):
            vectors = np.asarray(vectors)

        vectors = np.asarray(vectors, dtype=np.float32)

        # Гарантируем форму (batch_size, dim)
        if vectors.ndim == 1:
            vectors = vectors.reshape(1, -1)

        return vectors.tolist()

    def encode_query(self, query: str) -> list[float]:
        """Совместимость: один запрос через батчевый encode_queries."""
        vectors = self.encode_queries([query])
        return vectors[0] if vectors else []

    # --------- RabbitMQ ---------

    def _declare(self, ch: pika.adapters.blocking_connection.BlockingChannel) -> None:
        ch.queue_declare(queue=self._request_queue, durable=True)
        ch.queue_declare(queue=self._response_queue, durable=True)
        ch.basic_qos(prefetch_count=1)

    def _process_message(
        self,
        ch: pika.adapters.blocking_connection.BlockingChannel,
        body: bytes,
    ) -> None:
        try:
            payload = json.loads(body.decode("utf-8"))
        except Exception:
            self._logger.warning("Invalid message encoding, skipping")
            return

        items = payload.get("Items")
        service_id = payload.get("ServiceId")
        responses: list[Dict[str, Any]] = []

        # ---------- Батчевый запрос ----------
        if isinstance(items, list):
            valid_items: list[tuple[str, Optional[str], str]] = []

            for i, item in enumerate(items, start=1):
                request_id = item.get("id") or item.get("Id")
                query = (
                    item.get("String")
                    or item.get("Query")
                    or item.get("query")
                )
                user_id = item.get("UserId") or item.get("userId")

                if not request_id or not query:
                    self._logger.warning(
                        "Batch item %s is missing id or query, skipping", i
                    )
                    continue

                valid_items.append((request_id, user_id, query))

            if not valid_items:
                self._logger.warning(
                    "Batch message contained %s items, but none were valid",
                    len(items),
                )
                return

            # Одно сообщение лога на батч
            self._logger.info(
                "batch received: total_items=%d, valid_items=%d",
                len(items),
                len(valid_items),
            )

            queries = [q for (_, _, q) in valid_items]
            vectors = self.encode_queries(queries)

            for (request_id, user_id, query), vector in zip(valid_items, vectors):
                responses.append(
                    {
                        "Id": request_id,
                        "UserId": user_id,
                        "String": query,
                        "Vector": vector,
                    }
                )
        
        # ---------- Одиночный запрос (старый режим) ----------
        else:
            request_id = payload.get("id") or payload.get("Id")
            query = payload.get("query") or payload.get("Query")

            if not request_id or not query:
                self._logger.warning("Request is missing id or query")
                return

            self._logger.info("single request received: id=%s", request_id)
            vector = self.encode_query(query)
            responses.append({"Id": request_id, "Vector": vector})

        if not responses:
            self._logger.warning("No valid request items found in message")
            return

        # Отвечаем в стиле QueryVectorBatchResponse (C# десериализация чувствительна к регистру)
        response_body: Dict[str, Any] = {"Items": responses}
        if service_id:
            response_body["ServiceId"] = service_id

        ch.basic_publish(
            exchange="",
            routing_key=self._response_queue,
            body=json.dumps(response_body, ensure_ascii=False).encode("utf-8"),
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
                        self._process_message(ch, body)
                    except Exception:
                        self._logger.exception(
                            "processing error, message will be nacked"
                        )
                        try:
                            ch.basic_nack(
                                delivery_tag=method.delivery_tag,
                                requeue=False,
                            )
                        except Exception:
                            self._logger.exception("failed to nack message")
                        return

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


def main() -> None:
    parser = argparse.ArgumentParser(description="Query vector worker")
    # пока без параметров CLI, но задел оставим
    parser.parse_args()

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
