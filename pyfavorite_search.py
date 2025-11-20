import json
import logging
import os
import time
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Sequence, Tuple

import numpy as np
import pika
import torch
from mssql_python import connect
from mssql_python.exceptions import IntegrityError

from sentence_transformers import SentenceTransformer
from tqdm.auto import tqdm

# ==============
# НАСТРОЙКИ ЛОГИРОВАНИЯ
# ==============
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
)
logger = logging.getLogger("favorite_worker")

# ============
# КОНСТАНТЫ
# ============

EMBEDDING_MODEL_NAME = os.getenv(
    "EMBEDDING_MODEL_NAME",
    "sentence-transformers/paraphrase-multilingual-mpnet-base-v2",
)

DB_CONNECTION_STRING = os.getenv(
    "DB_CONNECTION_STRING",
    # запасной вариант, если ничего не найдём в appsettings.json
    "Server=localhost;"
    "Database=Tender1;"
    "Trusted_Connection=Yes;"
    "TrustServerCertificate=Yes;"
)

# путь к appsettings.json
# по умолчанию берём конфигурацию .NET-сервиса (src/Zakupki.Fetcher/appsettings.json),
# но можно переопределить переменной окружения APPSETTINGS_PATH
DEFAULT_APPSETTINGS_PATHS = [
    "appsettings.json",
    os.path.join("src", "Zakupki.Fetcher", "appsettings.json"),
]


def resolve_appsettings_path() -> str:
    env_path = os.getenv("APPSETTINGS_PATH")
    if env_path and os.path.exists(env_path):
        logger.info("Используем appsettings из переменной окружения: %s", env_path)
        return env_path

    for p in DEFAULT_APPSETTINGS_PATHS:
        if os.path.exists(p):
            logger.info("Используем appsettings: %s", p)
            return p

    # если ничего не нашли — все равно вернем первый вариант по умолчанию
    logger.warning(
        "appsettings.json не найден ни по одному из путей, "
        "будет использован путь по умолчанию: %s",
        DEFAULT_APPSETTINGS_PATHS[0],
    )
    return DEFAULT_APPSETTINGS_PATHS[0]


def load_appsettings(path: str) -> Dict[str, Any]:
    if not os.path.exists(path):
        logger.warning("Файл appsettings.json не найден по пути: %s", path)
        return {}

    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def build_rabbitmq_url_from_appsettings(config: Dict[str, Any]) -> str:
    event_bus = config.get("EventBus", {}).get("BusAccess", {})
    host = event_bus.get("Host", "localhost")
    username = event_bus.get("UserName", "guest")
    password = event_bus.get("Password", "guest")
    port = event_bus.get("Port", 5672)

    # пример: amqp://user:pass@host:5672/
    url = f"amqp://{username}:{password}@{host}:{port}/"
    logger.info("RabbitMQ URL собран из appsettings: %s", url)
    return url


def resolve_favorite_queue_name(config: Dict[str, Any]) -> str:
    # имя очереди берём из ZakupkiOptions: FavoriteSearchCommandQueueName,
    # если не указано — дефолт
    zakupki_options = config.get("ZakupkiOptions", {})
    queue_name = zakupki_options.get(
        "FavoriteSearchCommandQueueName",
        "w.ds_development_cmd_zak_local",
    )
    logger.info("Имя очереди (ResolveCommandQueueName): %s", queue_name)
    return queue_name


# ============
# МОДЕЛЬ EMBEDDINGS
# ============


class EmbeddingModel:
    def __init__(self, model_name: str) -> None:
        self.model_name = model_name
        self._device = "cuda" if torch.cuda.is_available() else "cpu"
        logger.info("loading embedding model: %s", model_name)
        self._model = SentenceTransformer(model_name, device=self._device)

    def encode(self, texts: Sequence[str]) -> np.ndarray:
        # Возвращаем float32, как привычно в большинстве моделей
        return self._model.encode(
            list(texts),
            convert_to_numpy=True,
            show_progress_bar=False,
            batch_size=16,
        ).astype(np.float32)


# ============
# SQL-УТИЛИТЫ
# ============


def to_utc(dt: datetime) -> datetime:
    if dt.tzinfo is None:
        return dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def vector_to_mssql_bytes(vec: np.ndarray) -> bytes:
    """
    Преобразует numpy-вектор float32 в байты для вставки в VECTOR(768).
    По сути — просто raw bytes массива.
    """
    return vec.tobytes()


# ============
# СТРУКТУРА КОМАНДЫ
# ============


class FavoriteSearchCommand:
    def __init__(
        self,
        user_id: str,
        query: str,
        collecting_end_limit: Optional[datetime],
        expired_only: bool,
        top: int,
        limit: Optional[int],
    ) -> None:
        self.user_id = user_id
        self.query = query
        self.collecting_end_limit = collecting_end_limit
        self.expired_only = expired_only
        self.top = top
        self.limit = limit

    @staticmethod
    def from_json(data: Dict[str, Any]) -> "FavoriteSearchCommand":
        user_id = data["userId"]
        query = data["query"]
        top = int(data.get("top", 20))
        limit = data.get("limit")
        if limit is not None:
            limit = int(limit)

        collecting_end_limit_raw = data.get("collectingEndLimit")
        if collecting_end_limit_raw:
            collecting_end_limit = datetime.fromisoformat(
                collecting_end_limit_raw.replace("Z", "+00:00")
            )
        else:
            collecting_end_limit = None

        expired_only = bool(data.get("expiredOnly", False))

        return FavoriteSearchCommand(
            user_id=user_id,
            query=query,
            collecting_end_limit=collecting_end_limit,
            expired_only=expired_only,
            top=top,
            limit=limit,
        )


# ============
# ЯДРО ПОИСКА
# ============


class FavoriteSearchEngine:
    def __init__(self, conn_str: str, embedding_model_name: str) -> None:
        self._conn_str = conn_str
        self._embedding = EmbeddingModel(embedding_model_name)

    def _get_connection(self):
        logger.debug("Открываем подключение к MS SQL")
        conn = connect(self._conn_str)
        return conn

    def _encode_query(self, query: str) -> np.ndarray:
        vec = self._embedding.encode([query])[0]
        return vec

    def _search_notices(
        self,
        query: str,
        collecting_end_limit: Optional[datetime],
        expired_only: bool,
        top: int,
        limit: Optional[int],
    ) -> List[Dict[str, Any]]:
        """
        Основной SQL-запрос.

        CLI-логика:
        - берем TOP (@top) по Similarity
        - фильтруем по n.CollectingEnd (если есть collectingEndLimit)
        - если expired_only = True, то CollectingEnd < now
        - если False — CollectingEnd >= now ИЛИ NULL
        """
        q_vec = self._encode_query(query)
        q_bytes = vector_to_mssql_bytes(q_vec)

        collecting_end_limit_utc: Optional[datetime] = None
        if collecting_end_limit is not None:
            collecting_end_limit_utc = to_utc(collecting_end_limit)

        logger.info("=== FAVORITE SEARCH REQUEST ===")
        logger.info("query=%s", query)
        # userId в запросе не участвует, но логируем для отладки
        logger.info("top=%s", top)
        logger.info("collectingEndLimit=%s", collecting_end_limit_utc)
        logger.info("expiredOnly=%s", expired_only)

        now_utc = datetime.now(timezone.utc)

        where_clauses = ["e.Model = ?"]
        params: List[Any] = [EMBEDDING_MODEL_NAME]

        # фильтр по collectingEnd
        if collecting_end_limit_utc is not None:
            if expired_only:
                # Только просроченные (CollectingEnd < collectingEndLimit)
                where_clauses.append("n.CollectingEnd IS NOT NULL AND n.CollectingEnd < ?")
                params.append(collecting_end_limit_utc)
            else:
                # Не истекшие (CollectingEnd >= collectingEndLimit или NULL)
                where_clauses.append(
                    "(n.CollectingEnd IS NULL OR n.CollectingEnd >= ?)"
                )
                params.append(collecting_end_limit_utc)
        else:
            # collectingEndLimit не указан — фильтруем по "сейчас"
            if expired_only:
                where_clauses.append("n.CollectingEnd IS NOT NULL AND n.CollectingEnd < ?")
                params.append(now_utc)
            else:
                where_clauses.append(
                    "(n.CollectingEnd IS NULL OR n.CollectingEnd >= ?)"
                )
                params.append(now_utc)

        where_sql = " AND ".join(where_clauses)

        sql = f"""
        SELECT TOP (?)
            n.Id,
            n.PurchaseNumber,
            n.EntryName,
            n.PurchaseObjectInfo,
            CAST(
                1.0 - VECTOR_DISTANCE(
                    'cosine',
                    e.Vector,
                    CAST(? AS VECTOR(768))
                ) AS FLOAT
            ) AS Similarity
        FROM NoticeEmbeddings e
        INNER JOIN Notices n ON n.Id = e.NoticeId
        WHERE {where_sql}
        ORDER BY Similarity DESC, n.UpdatedAt DESC
        """

        # Параметры: TOP, вектор, далее – параметры фильтра
        full_params: List[Any] = [top, q_bytes] + params

        logger.info("SQL:\n%s", sql)
        # В лог – без огромного бинарника, поэтому показываем <EMBEDDING_JSON>
        log_params = [full_params[0], "<EMBEDDING_JSON>"] + full_params[2:]
        logger.info("PARAMS: %s", log_params)

        rows: List[Dict[str, Any]] = []
        with self._get_connection() as conn:
            with conn.cursor() as cur:
                cur.execute(sql, tuple(full_params))
                cols = [c[0] for c in cur.description]
                for r in cur.fetchall():
                    row_dict = {col: val for col, val in zip(cols, r)}
                    rows.append(row_dict)

        return rows

    def process_command(self, cmd: FavoriteSearchCommand) -> None:
        """
        Обрабатывает одну команду.
        """
        rows = self._search_notices(
            query=cmd.query,
            collecting_end_limit=cmd.collecting_end_limit,
            expired_only=cmd.expired_only,
            top=cmd.top,
            limit=cmd.limit,
        )

        if not rows:
            logger.info("Нет результатов для запроса: %s", cmd.query)
            return

        logger.info("Найдено записей: %d", len(rows))
        for i, r in enumerate(rows[:5], start=1):
            logger.info(
                "[%d] #%s (%s) — %.4f",
                i,
                r.get("PurchaseNumber"),
                (r.get("EntryName") or "")[:80],
                r.get("Similarity"),
            )


# ============
# ОБРАБОТЧИК СООБЩЕНИЙ ИЗ ОЧЕРЕДИ
# ============


class FavoriteSearchWorker:
    def __init__(self, engine: FavoriteSearchEngine, queue_name: str) -> None:
        self._engine = engine
        self._queue_name = queue_name

    def _on_message(self, ch, method, properties, body: bytes) -> None:
        logger.info("message received")
        try:
            data = json.loads(body.decode("utf-8"))
        except Exception as e:
            logger.error("Не удалось распарсить JSON команды: %s", e, exc_info=True)
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        try:
            command = FavoriteSearchCommand.from_json(data)
        except Exception as e:
            logger.error("Ошибка при разборе FavoriteSearchCommand: %s", e, exc_info=True)
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        try:
            self._engine.process_command(command)
        except Exception as e:
            logger.error(
                "Общая ошибка при обработке сообщения, сообщение пропущено",
                exc_info=True,
            )
        finally:
            ch.basic_ack(delivery_tag=method.delivery_tag)

    def run(self, rabbitmq_url: str) -> None:
        """
        Основной цикл: подключение к RabbitMQ, подписка на очередь, обработка сообщений.
        """
        logger.info("FavoriteSearchWorker запущен. Ожидание сообщений из очереди '%s'...", self._queue_name)

        while True:
            try:
                params = pika.URLParameters(rabbitmq_url)
                connection = pika.BlockingConnection(params)
                channel = connection.channel()

                channel.queue_declare(queue=self._queue_name, durable=True)
                channel.basic_qos(prefetch_count=1)
                channel.basic_consume(
                    queue=self._queue_name,
                    on_message_callback=self._on_message,
                )

                channel.start_consuming()
            except KeyboardInterrupt:
                logger.info("Остановка по Ctrl+C")
                break
            except Exception as e:
                logger.error("Ошибка подключения/обработки из RabbitMQ: %s", e, exc_info=True)
                time.sleep(5.0)


# ============
# Точка входа
# ============


def main():
    # грузим appsettings
    appsettings_path = resolve_appsettings_path()
    config = load_appsettings(appsettings_path)

    # Строка подключения к БД:
    # 1) если есть переменная окружения DB_CONNECTION_STRING – используем её
    # 2) иначе берём ConnectionStrings.Default из appsettings.json
    # 3) если и там пусто – используем DB_CONNECTION_STRING (запасной вариант сверху)
    conn_str_env = os.getenv("DB_CONNECTION_STRING")
    if conn_str_env:
        conn_str = conn_str_env
        logger.info("Строка подключения к БД взята из DB_CONNECTION_STRING")
    else:
        conn_str = (
            (config.get("ConnectionStrings") or {}).get("Default")
            or DB_CONNECTION_STRING
        )
        logger.info("Строка подключения к БД взята из appsettings ConnectionStrings:Default")

    # URL RabbitMQ:
    # 1) если есть переменная окружения RABBITMQ_URL – используем её
    # 2) иначе собираем из appsettings: EventBus.BusAccess.{Host,UserName,Password}
    rabbitmq_url_env = os.getenv("RABBITMQ_URL")
    if rabbitmq_url_env:
        rabbitmq_url = rabbitmq_url_env
        logger.info("RabbitMQ URL взят из RABBITMQ_URL: %s", rabbitmq_url)
    else:
        rabbitmq_url = build_rabbitmq_url_from_appsettings(config)

    queue_name = resolve_favorite_queue_name(config)

    engine = FavoriteSearchEngine(
        conn_str=conn_str,
        embedding_model_name=EMBEDDING_MODEL_NAME,
    )

    # петля переподключения к RabbitMQ
    while True:
        try:
            worker = FavoriteSearchWorker(engine, queue_name=queue_name)
            worker.run(rabbitmq_url=rabbitmq_url)
        except KeyboardInterrupt:
            break
        except Exception as e:
            logger.error("Ошибка верхнего уровня в FavoriteSearchWorker: %s", e, exc_info=True)
            time.sleep(5.0)


if __name__ == "__main__":
    main()
