#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Utility script to publish test messages to ``pyquery_vector.py`` queues."""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
import uuid
from typing import Any, Dict, List, Optional, Tuple

import pika

from pyquery_vector import DEFAULT_CONFIG_PATH, _first_non_empty, load_config


def _load_settings(args: argparse.Namespace) -> Tuple[str, str, str, str, str]:
    config = load_config(args.config)
    event_bus = config.get("EventBus") or {}
    bus_access = event_bus.get("BusAccess") or {}

    request_queue = _first_non_empty(
        args.request_queue,
        os.environ.get("EVENTBUS_QUERY_VECTOR_REQUEST_QUEUE"),
        event_bus.get("QueryVectorRequestQueueName"),
    )
    response_queue = _first_non_empty(
        args.response_queue,
        os.environ.get("EVENTBUS_QUERY_VECTOR_RESPONSE_QUEUE"),
        event_bus.get("QueryVectorResponseQueueName"),
    )
    host = _first_non_empty(args.host, os.environ.get("EVENTBUS_HOST"), bus_access.get("Host"))
    user = _first_non_empty(
        args.username, os.environ.get("EVENTBUS_USERNAME"), bus_access.get("UserName")
    )
    pwd = _first_non_empty(
        args.password, os.environ.get("EVENTBUS_PASSWORD"), bus_access.get("Password")
    )

    if not host or not user or not pwd:
        raise RuntimeError("Некорректные настройки EventBus.BusAccess (Host/UserName/Password)")
    if not request_queue or not response_queue:
        raise RuntimeError("Не заданы очереди запросов и ответов для векторизации")

    return host, user, pwd, request_queue, response_queue


def _parse_items(raw_items: Optional[List[str]]) -> List[Dict[str, Any]]:
    if not raw_items:
        return []

    items: List[Dict[str, Any]] = []
    for raw in raw_items:
        parts = raw.split("::")
        if len(parts) < 2:
            raise ValueError(
                f"Неверный формат '{raw}'. Используйте id::query или id::query::userId"
            )
        payload: Dict[str, Any] = {"Id": parts[0], "Query": parts[1]}
        if len(parts) >= 3:
            payload["UserId"] = parts[2]
        items.append(payload)

    return items


def _build_payload(args: argparse.Namespace) -> Tuple[Dict[str, Any], Optional[str]]:
    items = _parse_items(args.item)
    service_id = None if args.no_service_id else args.service_id or str(uuid.uuid4())

    if items:
        payload: Dict[str, Any] = {"Items": items}
    else:
        payload = {"Id": args.id, "Query": args.query}

    if service_id:
        payload["ServiceId"] = service_id

    return payload, service_id


def publish_and_wait(args: argparse.Namespace) -> None:
    host, user, pwd, request_queue, response_queue = _load_settings(args)
    payload, service_id = _build_payload(args)

    logging.info(
        "Отправка сообщения в %s (ожидание ответа в %s). ServiceId=%s", request_queue,
        response_queue,
        service_id or "<none>",
    )
    connection = pika.BlockingConnection(
        pika.ConnectionParameters(
            host=host,
            credentials=pika.PlainCredentials(user, pwd),
            heartbeat=60,
            blocked_connection_timeout=120.0,
        )
    )
    channel = connection.channel()
    channel.queue_declare(queue=request_queue, durable=True)
    channel.queue_declare(queue=response_queue, durable=True)

    channel.basic_publish(
        exchange="",
        routing_key=request_queue,
        body=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        properties=pika.BasicProperties(delivery_mode=2, content_type="application/json"),
    )
    logging.info("Сообщение отправлено: %s", json.dumps(payload, ensure_ascii=False))

    if args.no_wait:
        logging.info("Флаг --no-wait установлен, завершение без чтения ответа")
        connection.close()
        return

    logging.info("Ожидание ответа (таймаут %s секунд)...", args.timeout)
    for method, properties, body in channel.consume(
        response_queue, inactivity_timeout=args.timeout
    ):
        if method is None:
            logging.error("Ответ не получен за %s секунд", args.timeout)
            break

        try:
            response = json.loads(body.decode("utf-8"))
        except Exception:
            logging.exception("Не удалось распарсить ответ: %r", body)
            channel.basic_ack(method.delivery_tag)
            continue

        channel.basic_ack(method.delivery_tag)
        logging.info("Ответ получен: %s", json.dumps(response, ensure_ascii=False))

        if service_id and response.get("ServiceId") != service_id:
            logging.info("ServiceId не совпадает, продолжаем ожидание...")
            continue

        if not args.item:
            ids = {str(response.get("Id"))}
            items = response.get("Items")
            if isinstance(items, list):
                ids.update(str(it.get("Id")) for it in items)
            if args.id not in ids:
                logging.info("Id не совпадает, продолжаем ожидание...")
                continue

        break

    connection.close()


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Тестовый паблишер для pyquery_vector.py"
    )
    parser.add_argument(
        "--config", default=DEFAULT_CONFIG_PATH, help="Путь до appsettings.json"
    )
    parser.add_argument("--host", help="Адрес RabbitMQ (перекрывает ENV/конфиг)")
    parser.add_argument("--username", help="Логин RabbitMQ (перекрывает ENV/конфиг)")
    parser.add_argument("--password", help="Пароль RabbitMQ (перекрывает ENV/конфиг)")
    parser.add_argument(
        "--request-queue", help="Очередь запросов (QueryVectorRequestQueueName)"
    )
    parser.add_argument(
        "--response-queue", help="Очередь ответов (QueryVectorResponseQueueName)"
    )
    parser.add_argument("--id", default="test-1", help="Id для одиночного запроса")
    parser.add_argument(
        "--query",
        default="Привет, мир",
        help="Текст запроса для одиночного сообщения",
    )
    parser.add_argument(
        "--item",
        action="append",
        help="Элемент батча в формате id::query или id::query::userId (можно несколько)",
    )
    parser.add_argument("--service-id", help="Явный ServiceId (по умолчанию UUID4)")
    parser.add_argument(
        "--no-service-id", action="store_true", help="Не добавлять поле ServiceId"
    )
    parser.add_argument(
        "--timeout", type=int, default=15, help="Таймаут ожидания ответа в секундах"
    )
    parser.add_argument(
        "--no-wait",
        action="store_true",
        help="Отправить сообщение и не ждать ответ",
    )

    args = parser.parse_args()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(message)s",
        stream=sys.stdout,
    )

    try:
        publish_and_wait(args)
    except Exception:  # pragma: no cover - отладочная утилита
        logging.exception("Ошибка при отправке тестового сообщения")
        sys.exit(1)


if __name__ == "__main__":
    main()
