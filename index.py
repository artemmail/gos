#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Индексация закупок (Notice) в таблицу NoticeEmbeddings.

Зависимости:
    pip install mssql-python sentence-transformers torch numpy

Скрипт:
- читает строку подключения из appsettings.json (ConnectionStrings.Default),
- конвертирует её в формат, понятный mssql_python,
- выбирает записи Notice без эмбеддингов (или с устаревшими),
- считает эмбеддинги на GPU (если доступно),
- пишет/обновляет строки в NoticeEmbeddings.

Ожидается, что:
  * есть таблица [Notices] с полями (минимум):
      Id,
      EntryName,
      PurchaseObjectInfo,
      Okpd2Code,
      Okpd2Name,
      KvrCode,
      KvrName,
      Source,
      DocumentType
  * есть таблица [NoticeEmbeddings] c полями:
      Id           (uniqueidentifier),
      NoticeId     (ссылка на Notices.Id),
      Vector       (VECTOR(768) или совместимое поле),
      Source       (nvarchar)
"""

import json
import uuid
import os
from typing import Any, List, Dict

import mssql_python
import torch
from sentence_transformers import SentenceTransformer
import numpy as np

# ======= НАСТРОЙКИ =======

APPSETTINGS_PATH = os.environ.get("APPSETTINGS_PATH", "appsettings.json")
MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
VECTOR_DIMENSIONS = 768
BATCH_SIZE = 64              # размер батча для модели
DB_BATCH_SIZE = 500          # сколько Notice за раз вытаскиваем из БД


# ======= КОНВЕРТЕР СТРОКИ ПОДКЛЮЧЕНИЯ =======

def convert_to_mssql_python_conn_str(raw_conn_str: str) -> str:
    """
    Конвертирует классическую ADO/ODBC-строку подключения в формат,
    который ожидает mssql_python:

        SERVER=...;DATABASE=...;UID=...;PWD=...;
        Authentication=...;Encrypt=...;TrustServerCertificate=...

    Поддерживаем базовые синонимы:
      - Data Source / Server -> SERVER
      - Initial Catalog / Database -> DATABASE
      - User Id / User ID / UID -> UID
      - Password / Pwd -> PWD
      - Integrated Security / Trusted_Connection -> Authentication=ActiveDirectoryIntegrated
      - Encrypt -> Encrypt
      - TrustServerCertificate / Trust Server Certificate -> TrustServerCertificate

    Лишние параметры (MultipleActiveResultSets, Connection Timeout, ...) выбрасываем.
    """

    # если строка уже выглядит как SERVER=...;DATABASE=..., оставим её как есть
    lowered = raw_conn_str.lower()
    if "server=" in lowered or "database=" in lowered:
        normalized = ";".join(part.strip() for part in raw_conn_str.split(";") if part.strip())
        return normalized

    pairs = [p for p in raw_conn_str.split(";") if p.strip()]
    result: Dict[str, str] = {}

    for pair in pairs:
        if "=" not in pair:
            continue
        key, value = pair.split("=", 1)
        k = key.strip().lower()
        v = value.strip()

        if not k:
            continue

        if k in ("data source", "server", "addr", "address"):
            result["SERVER"] = v
        elif k in ("initial catalog", "database"):
            result["DATABASE"] = v
        elif k in ("user id", "user", "uid"):
            result["UID"] = v
        elif k in ("password", "pwd"):
            result["PWD"] = v
        elif k in ("authentication",):
            result["Authentication"] = v
        elif k in ("integrated security", "trusted_connection"):
            if v.lower() in ("true", "yes", "sspi"):
                # для Windows-аутентификации
                result.setdefault("Authentication", "ActiveDirectoryIntegrated")
        elif k == "encrypt":
            result["Encrypt"] = v
        elif k in ("trustservercertificate", "trust server certificate"):
            # приводим к yes/no, иначе ODBC Driver 18 ругается на True/False
            vl = v.lower()
            if vl in ("true", "yes", "1"):
                result["TrustServerCertificate"] = "yes"
            elif vl in ("false", "no", "0"):
                result["TrustServerCertificate"] = "no"
            else:
                # странное значение — не ставим параметр
                pass
        else:
            # игнорируем остальные
            continue

    # если Encrypt не задан, включим его (типичный сценарий с TrustServerCertificate)
    if "Encrypt" not in result:
        result["Encrypt"] = "yes"

    conn_parts = [f"{k}={v}" for k, v in result.items()]
    normalized_conn_str = ";".join(conn_parts)
    return normalized_conn_str


# ======= УТИЛИТЫ =======

def load_connection_string(path: str) -> str:
    """
    Читает ConnectionStrings.Default из appsettings.json
    и конвертирует в формат, понятный mssql_python.
    """
    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)

    raw_conn = config["ConnectionStrings"]["Default"]

    print("Исходная строка подключения из appsettings.json:")
    print(raw_conn)

    norm_conn = convert_to_mssql_python_conn_str(raw_conn)

    print("Нормализованная строка подключения (mssql_python):")
    print(norm_conn)

    return norm_conn


def get_db_connection():
    """
    Открывает соединение через mssql-python.
    """
    conn_str = load_connection_string(APPSETTINGS_PATH)
    conn = mssql_python.connect(conn_str)
    conn.autocommit = False
    return conn


def build_notice_text(row: Any) -> str:
    """
    Собираем "паспорт" текста для эмбеддинга.
    Подгоняй по вкусу: какие поля важнее, какие можно выкинуть.
    """
    parts: List[str] = []

    def add(label: str, value: Any):
        if value is None:
            return
        s = str(value).strip()
        if s:
            parts.append(f"{label}: {s}")

    # Основные поля
    add("Название", getattr(row, "PurchaseNumber", None))
    add("Предмет закупки", getattr(row, "PurchaseObjectInfo", None))

    # ОКПД2
    okpd2_code = (getattr(row, "Okpd2Code", "") or "").strip()
    okpd2_name = (getattr(row, "Okpd2Name", "") or "").strip()
    if okpd2_code or okpd2_name:
        if okpd2_code and okpd2_name:
            okpd2_value = f"{okpd2_code} ({okpd2_name})"
        elif okpd2_name:
            okpd2_value = okpd2_name
        else:
            okpd2_value = okpd2_code
        add("ОКПД2", okpd2_value)

    # КВР
    kvr_code = getattr(row, "KvrCode", None)
    kvr_name = getattr(row, "KvrName", None)
    if kvr_code or kvr_name:
        kvr_text = f"{kvr_code or ''} {kvr_name or ''}".strip()
        add("КВР", kvr_text)

    # Прочее
    add("Источник", getattr(row, "Source", None))
    add("Тип документа", getattr(row, "DocumentType", None))

    return "\n".join(parts)


def vector_to_sql_vector(vector: np.ndarray) -> str:
    """
    Приводит numpy-вектор к строке JSON вида "[0.1, 2.0, ...]".
    SQL Server 2025 VECTOR(...) умеет неявно конвертировать
    nvarchar/json-строку в VECTOR.
    """
    arr = np.asarray(vector, dtype=np.float32).tolist()
    return json.dumps(arr, ensure_ascii=False)


def fetch_notices_for_indexing(cursor: Any, limit: int) -> List[Any]:
    """
    Выбираем Notice, для которых нет эмбеддинга.
    """
    sql = f"""
    SELECT TOP ({limit})
        n.Id,
        n.PurchaseNumber,
        n.PurchaseObjectInfo,
        n.Okpd2Code,
        n.Okpd2Name,
        n.KvrCode,
        n.KvrName,
        n.Source,
        n.DocumentType
    FROM [Notices] AS n
    LEFT JOIN (
        SELECT DISTINCT NoticeId
        FROM [NoticeEmbeddings]
    ) AS e ON e.NoticeId = n.Id
    WHERE e.NoticeId IS NULL
    ORDER BY n.Id DESC
    """
    cursor.execute(sql)
    rows = cursor.fetchall()
    return rows


def upsert_embeddings(
    cursor: Any,
    rows: List[Any],
    embeddings: np.ndarray,
    source: str = "python-indexer",
):
    """
    Для каждого Notice:
      - удаляем старую запись по NoticeId,
      - вставляем новую с вектором.
    Вектор сохраняем в VECTOR(...) через JSON-строку.
    """
    dims = embeddings.shape[1]

    if dims != VECTOR_DIMENSIONS:
        raise ValueError(
            f"Размерность эмбеддинга {dims} не совпадает с ожидаемым значением {VECTOR_DIMENSIONS}"
        )

    insert_sql = """
    INSERT INTO [NoticeEmbeddings] (
        Id,
        NoticeId,
        Vector,
        Source
    ) VALUES (?, ?, ?, ?)
    """

    delete_sql = """
    DELETE FROM [NoticeEmbeddings]
    WHERE NoticeId = ?
    """

    for row, emb in zip(rows, embeddings):
        notice_id = row.Id
        vector_for_sql = vector_to_sql_vector(emb)
        embedding_id = uuid.uuid4()

        cursor.execute(delete_sql, (notice_id,))

        cursor.execute(
            insert_sql,
            (
                str(embedding_id),
                str(notice_id),
                vector_for_sql,
                source,
            ),
        )


# ======= MAIN =======

def main():
    # Модель эмбеддингов
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"Загружаю модель {MODEL_NAME} на устройстве {device}...")
    model = SentenceTransformer(MODEL_NAME, device=device)

    conn = get_db_connection()
    cursor = conn.cursor()

    total_processed = 0

    try:
        while True:
            notices = fetch_notices_for_indexing(cursor, DB_BATCH_SIZE)
            if not notices:
                print("Нет записей для индексации. Выход.")
                break

            print(f"Нашёл {len(notices)} записей для индексации, считаю эмбеддинги...")

            texts = [build_notice_text(row) for row in notices]

            embeddings = model.encode(
                texts,
                batch_size=BATCH_SIZE,
                show_progress_bar=True,
                convert_to_numpy=True,
                normalize_embeddings=False,
            )

            print("Записываю эмбеддинги в базу...")
            upsert_embeddings(cursor, notices, embeddings)

            conn.commit()

            total_processed += len(notices)
            print(f"Готово, обработано суммарно: {total_processed}")

    except Exception as ex:
        conn.rollback()
        print("ОШИБКА, транзакция откатена:", ex)
        raise
    finally:
        cursor.close()
        conn.close()
        print("Соединение с БД закрыто.")


if __name__ == "__main__":
    main()
