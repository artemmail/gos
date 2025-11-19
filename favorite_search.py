#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Семантический поиск по закупкам с сохранением результатов в избранное (FavoriteNotices).

Примеры:
    python favorite_search.py "пластиковые окна" --user-id testuser --top 10
    python favorite_search.py "продукты питания" --user-id 123 --top 50 --limit 5000

Зависимости:
    pip install pyodbc sentence-transformers torch numpy
"""

import argparse
import json
import sys
import uuid
from datetime import datetime
from typing import List, Tuple

import pyodbc
import numpy as np
import torch
from sentence_transformers import SentenceTransformer

# === НАСТРОЙКИ ===

APPSETTINGS_PATH = "appsettings.json"  # если файл называется иначе — поправь
ODBC_DRIVER = "{ODBC Driver 17 for SQL Server}"

# Используем ту же модель, что и в индексаторе
MODEL_NAME = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"


# === УТИЛИТЫ ПОДКЛЮЧЕНИЯ К БАЗЕ ===

def load_connection_string(path: str) -> str:
    """
    Читает ConnectionStrings.Default из appsettings.json
    и собирает корректную ODBC-строку для pyodbc + SQL Server.
    """
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
            f"server={server}, database={database}, user={user}, "
            f"password={'***' if password else None}"
        )

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


# === РАБОТА С ЭМБЕДДИНГАМИ ===

def fetch_notice_embeddings(
    cursor: pyodbc.Cursor,
    model_name: str,
    limit: int
) -> List[Tuple[str, str, str, str, str]]:
    """
    Забираем из базы эмбеддинги и данные по закупке.

    Возвращаем список кортежей:
        (notice_id, purchase_number, entry_name, purchase_object_info, vector_json)

    Если limit > 0 — добавляем TOP (limit),
    если limit == 0 — забираем все.
    """
    top_clause = f"TOP ({limit}) " if limit and limit > 0 else ""

    sql = f"""
    SELECT {top_clause}
        n.Id,
        n.PurchaseNumber,
        n.EntryName,
        n.PurchaseObjectInfo,
        e.Vector
    FROM [NoticeEmbeddings] AS e
    INNER JOIN [Notices] AS n ON n.Id = e.NoticeId
    WHERE e.Model = ?
    ORDER BY n.UpdatedAt DESC
    """

    cursor.execute(sql, model_name)
    rows = cursor.fetchall()

    result: List[Tuple[str, str, str, str, str]] = []
    for row in rows:
        result.append((
            str(row.Id),
            row.PurchaseNumber,
            row.EntryName,
            row.PurchaseObjectInfo,
            row.Vector
        ))
    return result


def parse_vectors(rows: List[Tuple[str, str, str, str, str]]) -> np.ndarray:
    """
    Преобразуем JSON-векторы в numpy-массив размера (N, D).

    rows: (notice_id, purchase_number, entry_name, purchase_object_info, vector_json)
    """
    import json as _json

    vectors = []
    for _, _, _, _, vector_json in rows:
        vec = _json.loads(vector_json)
        vectors.append(vec)

    return np.array(vectors, dtype=np.float32)


def cosine_similarity_matrix(query_vec: np.ndarray, matrix: np.ndarray) -> np.ndarray:
    """
    Косинусное сходство между query_vec (D,) и matrix (N, D).
    Возвращает массив (N,) со значениями сходства.
    """
    q = query_vec / (np.linalg.norm(query_vec) + 1e-12)
    m_norm = matrix / (np.linalg.norm(matrix, axis=1, keepdims=True) + 1e-12)
    sims = np.dot(m_norm, q)
    return sims


# === ВСТАВКА В FavoriteNotices ===

def upsert_favorites(
    cursor: pyodbc.Cursor,
    rows: List[Tuple[str, str, str, str, str]],
    sims: np.ndarray,
    top_indices: np.ndarray,
    user_id: str
):
    """
    Для выбранных top-результатов вставляем записи в [FavoriteNotices],
    избегая дублей по (UserId, NoticeId).
    """
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

    for rank, idx in enumerate(top_indices, start=1):
        notice_id, purchase_number, entry_name, purchase_object_info, _ = rows[idx]
        score = sims[idx]

        favorite_id = uuid.uuid4()

        cursor.execute(
            insert_sql,
            str(favorite_id),   # Id
            notice_id,          # NoticeId
            user_id,            # UserId
            now,                # CreatedAt
            user_id,            # UserId для WHERE NOT EXISTS
            notice_id           # NoticeId для WHERE NOT EXISTS
        )

        # rowcount > 0 -> вставка реально произошла
        if cursor.rowcount and cursor.rowcount > 0:
            status = "ДОБАВЛЕНО"
        else:
            status = "УЖЕ ЕСТЬ"

        print(f"{rank:2d}. [{status}] score={score:.4f}")
        print(f"    ЕИС ID (PurchaseNumber): {purchase_number}")
        print(f"    NoticeId:               {notice_id}")
        if purchase_object_info:
            print(f"    Предмет закупки:        {purchase_object_info}")
        print(f"    EntryName:              {entry_name}")
        print("-" * 80)


# === MAIN ===

def main():
    parser = argparse.ArgumentParser(
        description="Семантический поиск по закупкам и сохранение результатов в избранное (FavoriteNotices)"
    )
    parser.add_argument(
        "query",
        nargs="+",
        help="Текст запроса (что ищем)"
    )
    parser.add_argument(
        "--user-id",
        type=str,
        required=True,
        help="UserId (строка из ASP.NET Identity / ApplicationUser.Id), под которым сохранять избранное"
    )
    parser.add_argument(
        "--top",
        type=int,
        default=10,
        help="Сколько лучших результатов сохранить (по умолчанию 10)"
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=0,
        help="Максимум закупок для сравнения (0 = без ограничения, по умолчанию 0)"
    )

    args = parser.parse_args()
    query_text = " ".join(args.query).strip()
    user_id = args.user_id.strip()

    if not query_text:
        print("Пустой запрос. Пример: python favorite_search.py \"пластиковые окна для школы\" --user-id testuser")
        sys.exit(1)

    if not user_id:
        print("UserId не указан или пустой. Пример: --user-id testuser")
        sys.exit(1)

    print(f"Запрос: {query_text}")
    print(f"UserId: {user_id}")

    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"Загружаю модель {MODEL_NAME} на устройстве {device}...")
    model = SentenceTransformer(MODEL_NAME, device=device)

    # Эмбеддинг запроса
    query_vec = model.encode(
        [query_text],
        convert_to_numpy=True,
        normalize_embeddings=False
    )[0]

    conn = get_db_connection()
    cursor = conn.cursor()

    try:
        rows = fetch_notice_embeddings(cursor, MODEL_NAME, args.limit)
        if not rows:
            print("В базе нет эмбеддингов для указанной модели. Сначала запусти индексатор.")
            return

        print(f"Загружено {len(rows)} векторов из БД, считаю косинусное сходство...")

        matrix = parse_vectors(rows)
        sims = cosine_similarity_matrix(query_vec, matrix)

        top_k = min(args.top, len(rows))
        top_indices = np.argsort(-sims)[:top_k]

        # Покажем ТОП-N перед сохранением
        print()
        print(f"ТОП-{top_k} результатов (перед сохранением в избранное):")
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

        # Теперь вставляем в FavoriteNotices с защитой от дублей
        upsert_favorites(cursor, rows, sims, top_indices, user_id)

        conn.commit()
        print()
        print("Избранное успешно сохранено (транзакция закоммичена).")

    except Exception as ex:
        conn.rollback()
        print("ОШИБКА, транзакция откатена:", ex)
        raise
    finally:
        cursor.close()
        conn.close()


if __name__ == "__main__":
    main()
