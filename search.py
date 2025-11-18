#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Семантический поиск по закупкам.

Примеры:
    python search.py "пластиковые окна"
    python search.py "поставка продуктов питания для школы" --top 20 --limit 5000
    python search.py "пластиковые окна" --check-pn 0133300012625000105

Зависимости:
    pip install pyodbc sentence-transformers torch numpy
"""

import argparse
import json
import sys
from typing import List, Tuple

import pyodbc
import numpy as np
import torch
from sentence_transformers import SentenceTransformer

# === НАСТРОЙКИ ===

APPSETTINGS_PATH = "appsettings.json"  # если файл называется иначе — поправь
ODBC_DRIVER = "{ODBC Driver 17 for SQL Server}"
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


# === MAIN ===

def main():
    parser = argparse.ArgumentParser(description="Семантический поиск по закупкам")
    parser.add_argument(
        "query",
        nargs="+",
        help="Текст запроса (что ищем)"
    )
    parser.add_argument(
        "--top",
        type=int,
        default=40,
        help="Сколько лучших результатов показать (по умолчанию 10)"
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=0,
        help="Максимум закупок для сравнения (0 = без ограничения, по умолчанию 0)"
    )
    parser.add_argument(
        "--check-pn",
        type=str,
        default=None,
        help="Проверить наличие и позицию конкретного PurchaseNumber (например 0133300012625000105)"
    )

    args = parser.parse_args()
    query_text = " ".join(args.query).strip()

    if not query_text:
        print("Пустой запрос. Пример: python search.py \"пластиковые окна для школы\"")
        sys.exit(1)

    print(f"Запрос: {query_text}")

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

        # если нужно проверить конкретный PurchaseNumber
        if args.check_pn:
            target_index = None
            for i, (_, purchase_number, _, _, _) in enumerate(rows):
                if purchase_number == args.check_pn:
                    target_index = i
                    break

            if target_index is None:
                print()
                print(f"⚠ Для PurchaseNumber={args.check_pn} НЕТ строки в NoticeEmbeddings "
                      f"для модели {MODEL_NAME}. Индексатор, скорее всего, не создал эмбеддинг.")
            else:
                target_score = sims[target_index]
                # ранг = сколько имеют score строго больше
                rank = int((sims > target_score).sum()) + 1
                total = len(sims)

                print()
                print(f"✔ PurchaseNumber={args.check_pn} найден в эмбеддингах.")
                print(f"   score={target_score:.4f}, позиция (ранг)={rank} из {total}")

        # обычный TOP-N вывод
        top_k = min(args.top, len(rows))
        top_indices = np.argsort(-sims)[:top_k]

        print()
        print(f"ТОП-{top_k} результатов:")
        print("=" * 80)
        for rank, idx in enumerate(top_indices, start=1):
            notice_id, purchase_number, entry_name, purchase_object_info, _ = rows[idx]
            score = sims[idx]
            print(f"{rank:2d}. score={score:.4f}")
            print(f"    ЕИС ID (PurchaseNumber): {purchase_number}")
            if purchase_object_info:
                print(f"    Предмет закупки:        {purchase_object_info}")
            print("-" * 80)

    finally:
        cursor.close()
        conn.close()


if __name__ == "__main__":
    main()
