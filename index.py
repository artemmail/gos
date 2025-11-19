#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Индексация закупок (Notice) в таблицу NoticeEmbeddings.

Зависимости:
    pip install pyodbc sentence-transformers torch

Убедись, что установлен ODBC драйвер:
    "ODBC Driver 17 for SQL Server" (или 18) и поправь имя драйвера ниже при необходимости.

Скрипт:
- читает строку подключения из appsettings.json (ConnectionStrings.Default),
- выбирает записи Notice без эмбеддингов (или с устаревшими),
- считает эмбеддинги на GPU (если доступно),
- пишет/обновляет строки в NoticeEmbeddings.
"""

import json
import uuid
import datetime as dt
import pyodbc
import os
from typing import List, Tuple

import torch
from sentence_transformers import SentenceTransformer
import numpy as np

# ======= НАСТРОЙКИ =======

APPSETTINGS_PATH = "appsettings.json"  # поменяй, если файл называется иначе
MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
VECTOR_DIMENSIONS = 768
BATCH_SIZE = 64
ODBC_DRIVER = "{ODBC Driver 17 for SQL Server}"  # или "{ODBC Driver 18 for SQL Server}"

# сколько записей за один проход из базы
DB_BATCH_SIZE = 500


# ======= УТИЛИТЫ =======

def load_connection_string(path: str) -> str:
    """
    Читает ConnectionStrings.Default из appsettings.json
    и собирает корректную ODBC-строку для pyodbc + SQL Server.
    """
    import json

    with open(path, "r", encoding="utf-8") as f:
        config = json.load(f)

    ado_conn = config["ConnectionStrings"]["Default"]

    server = None
    database = None
    user = None
    password = None

    parts = [p.strip() for p in ado_conn.split(";") if p.strip()]
    for part in parts:
        key, _, value = part.partition("=")
        key = key.strip().lower()
        value = value.strip()

        if key in ("data source", "server"):
            server = value
        elif key in ("database", "initial catalog"):
            database = value
        elif key in ("user id", "uid"):
            user = value
        elif key in ("password", "pwd"):
            password = value

    if not all([server, database, user, password]):
        raise RuntimeError(
            f"Не удалось разобрать строку подключения из appsettings.json. "
            f"server={server}, database={database}, user={user}, password={'***' if password else None}"
        )

    # Собираем ODBC-строку в самом простом и понятном драйверу виде
    conn_str = (
        f"DRIVER={ODBC_DRIVER};"
        f"SERVER={server};"
        f"DATABASE={database};"
        f"UID={user};"
        f"PWD={password};"
        f"TrustServerCertificate=Yes;"
        f"MARS_Connection=Yes"
    )

    print("ODBC строка подключения:")
    print(conn_str)

    return conn_str

def get_db_connection() -> pyodbc.Connection:
    conn_str = load_connection_string(APPSETTINGS_PATH)
    conn = pyodbc.connect(conn_str)
    conn.autocommit = False
    return conn


def build_notice_text(row: pyodbc.Row) -> str:
    """
    Собираем "паспорт" текста для эмбеддинга.
    Подгоняй по вкусу: какие поля важнее, какие можно выкинуть.
    """
    parts = []

    def add(label: str, value):
        if value is not None and str(value).strip():
            parts.append(f"{label}: {value}")

    add("Название", row.EntryName)
    add("Предмет закупки", row.PurchaseObjectInfo)
    okpd2_code = (row.Okpd2Code or "").strip()
    okpd2_name = (row.Okpd2Name or "").strip()
    if okpd2_code or okpd2_name:
        if okpd2_name:
            if okpd2_code:
                okpd2_value = f"{okpd2_code} ({okpd2_name})"
            else:
                okpd2_value = f"({okpd2_name})"
        else:
            okpd2_value = okpd2_code
        add("ОКПД2", okpd2_value)
    add("КВР", f"{row.KvrCode} {row.KvrName}" if row.KvrCode or row.KvrName else None)
    add("Источник", row.Source)
    add("Тип документа", row.DocumentType)

    return "\n".join(parts)


def vector_to_bytes(vector: np.ndarray) -> bytes:
    """Преобразует numpy-вектор в бинарный блок float64 для SQL VECTOR."""

    return np.asarray(vector, dtype=np.float64).tobytes()


def fetch_notices_for_indexing(cursor: pyodbc.Cursor, model_name: str, limit: int) -> List[pyodbc.Row]:
    """
    Выбираем Notice, для которых нет эмбеддинга указанной модели
    или он старше UpdatedAt у Notice.
    Предполагается, что:
      - таблицы называются [Notices] и [NoticeEmbeddings],
      - в NoticeEmbeddings есть поля NoticeId, Model, UpdatedAt.
    """
    sql = f"""
    SELECT TOP ({limit})
        n.Id,
        n.EntryName,
        n.PurchaseObjectInfo,
        n.Okpd2Code,
        n.Okpd2Name,
        n.KvrCode,
        n.KvrName,
        n.Source,
        n.DocumentType,
        n.UpdatedAt,
        e.LatestUpdatedAt AS EmbeddingUpdatedAt
    FROM [Notices] AS n
    LEFT JOIN (
        SELECT NoticeId, MAX(UpdatedAt) AS LatestUpdatedAt
        FROM [NoticeEmbeddings]
        WHERE Model = ?
        GROUP BY NoticeId
    ) AS e ON e.NoticeId = n.Id
    WHERE
        e.NoticeId IS NULL
        OR n.UpdatedAt > e.LatestUpdatedAt
    ORDER BY n.UpdatedAt DESC
    """
    cursor.execute(sql, model_name)
    rows = cursor.fetchall()
    return rows


def upsert_embeddings(
    cursor: pyodbc.Cursor,
    model_name: str,
    rows: List[pyodbc.Row],
    embeddings: np.ndarray,
    source: str = "python-indexer"
):
    """
    Для каждого Notice:
      - удаляем старую запись по (NoticeId, Model),
      - вставляем новую с вектором.
    Вектор сохраняем как SQL Server VECTOR(FLOAT64, N).
    """
    now = dt.datetime.utcnow()
    dims = embeddings.shape[1]

    if dims != VECTOR_DIMENSIONS:
        raise ValueError(
            f"Размерность эмбеддинга {dims} не совпадает с ожидаемым значением {VECTOR_DIMENSIONS}"
        )

    delete_sql = """
    DELETE FROM [NoticeEmbeddings]
    WHERE NoticeId = ? AND Model = ?
    """

    insert_sql = """
    INSERT INTO [NoticeEmbeddings] (
        Id,
        NoticeId,
        Model,
        Dimensions,
        Vector,
        CreatedAt,
        UpdatedAt,
        Source
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    """

    for row, emb in zip(rows, embeddings):
        notice_id = row.Id
        vector_bytes = pyodbc.Binary(vector_to_bytes(emb))

        embedding_id = uuid.uuid4()

        # удаляем старую запись (если была)
        cursor.execute(delete_sql, notice_id, model_name)

        # вставляем новую
        cursor.execute(
            insert_sql,
            str(embedding_id),
            str(notice_id),
            model_name,
            dims,
            vector_bytes,
            now,
            now,
            source
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
            notices = fetch_notices_for_indexing(cursor, MODEL_NAME, DB_BATCH_SIZE)
            if not notices:
                print("Нет записей для индексации. Выход.")
                break

            print(f"Нашёл {len(notices)} записей для индексации, считаю эмбеддинги...")

            texts = [build_notice_text(row) for row in notices]

            # рассчитываем эмбеддинги батчами, используя GPU если доступно
            embeddings = model.encode(
                texts,
                batch_size=BATCH_SIZE,
                show_progress_bar=True,
                convert_to_numpy=True,
                normalize_embeddings=False  # можно True, если хочешь сразу нормализованные
            )

            print("Записываю эмбеддинги в базу...")
            upsert_embeddings(cursor, MODEL_NAME, notices, embeddings)

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
