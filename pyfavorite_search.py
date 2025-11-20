import json
import logging
import os
import time
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

import pika
from pika.adapters.blocking_connection import BlockingConnection, BlockingChannel

import mssql_python
from mssql_python.exceptions import IntegrityError

from sentence_transformers import SentenceTransformer
from tqdm.auto import tqdm

# ========================
#  НАСТРОЙКИ
# ========================

DB_CONNECTION_STRING = os.getenv(
    "DB_CONNECTION_STRING",
    # сюда подставь свою строку подключения, если не используешь env
    "Driver={ODBC Driver 18 for SQL Server};"
    "Server=localhost;"
    "Database=tender1;"
    "UID=sa;PWD=your_password;"
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
    if env_path:
        if os.path.exists(env_path):
            logger.info("Используем appsettings из переменной окружения: %s", env_path)
            return env_path
        logger.warning("APPSETTINGS_PATH задан, но файл не найден: %s", env_path)

    for candidate in DEFAULT_APPSETTINGS_PATHS:
        if os.path.exists(candidate):
            logger.info("Используем appsettings: %s", candidate)
            return candidate

    # последний кандидат — первый из списка, чтобы load_appsettings отработал с предсказуемым путём
    logger.warning(
        "Файл appsettings не найден ни по одному пути %s, пробуем первый: %s",
        DEFAULT_APPSETTINGS_PATHS,
        DEFAULT_APPSETTINGS_PATHS[0],
    )
    return DEFAULT_APPSETTINGS_PATHS[0]

EMBEDDING_MODEL_NAME = os.getenv(
    "EMBEDDING_MODEL_NAME",
    "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
)

# Сколько максимум фаворитов сохранять за один запрос
MAX_FAVORITES_TO_SAVE = 500

# ========================
#  ЛОГГИРОВАНИЕ
# ========================

logger = logging.getLogger("favorite_worker")
logger.setLevel(logging.INFO)

_handler = logging.StreamHandler()
_handler.setFormatter(
    logging.Formatter("%(asctime)s [%(levelname)s] %(name)s - %(message)s")
)
logger.addHandler(_handler)


# ========================
#  ЧТЕНИЕ appsettings.json
# ========================

def load_appsettings(path: str) -> Dict[str, Any]:
    if not os.path.exists(path):
        logger.warning("Файл appsettings не найден: %s", path)
        return {}
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        return data
    except Exception:
        logger.exception("Не удалось прочитать/распарсить appsettings.json: %s", path)
        return {}


def build_rabbitmq_url_from_appsettings(config: Dict[str, Any]) -> str:
    """
    Берём настройки подключения RabbitMQ из:

    "EventBus": {
      ...
      "BusAccess": {
        "Host": "192.168.1.8",
        "UserName": "admin",
        "Password": "121212",
        ...
      }
    }
    """
    event_bus = config.get("EventBus", {}) or {}
    bus_access = event_bus.get("BusAccess", {}) or {}

    host = bus_access.get("Host", "localhost")
    user = bus_access.get("UserName", "guest")
    password = bus_access.get("Password", "guest")

    url = f"amqp://{user}:{password}@{host}:5672/"
    logger.info(
        "RabbitMQ URL собран из appsettings: amqp://%s:***@%s:5672/",
        user,
        host,
    )
    return url


def resolve_favorite_queue_name(config: Dict[str, Any]) -> str:
    """Возвращает очередь команд так же, как это делает .NET сервис.

    Сервер публикует избранное через EventBusOptions.ResolveCommandQueueName(),
    которая выбирает CommandQueueName, а если он пустой — QueueName. Любые
    пользовательские переменные окружения для имени очереди здесь НЕ применяем,
    чтобы слушать ровно ту очередь, куда отправляет сервер.
    """

    event_bus = config.get("EventBus", {}) or {}
    queue_name = event_bus.get("CommandQueueName") or event_bus.get("QueueName")
    if not queue_name or not str(queue_name).strip():
        raise RuntimeError(
            "CommandQueueName/QueueName не сконфигурированы в EventBus (см. appsettings)"
        )

    queue_name = str(queue_name).strip()
    logger.info("Имя очереди (ResolveCommandQueueName): %s", queue_name)
    return queue_name


# ========================
#  ДВИЖОК ПОИСКА
# ========================

class FavoriteSearchEngine:
    def __init__(self, conn_str: str, embedding_model_name: str):
        self._conn_str = conn_str

        logger.info("loading embedding model: %s", embedding_model_name)
        self._model = SentenceTransformer(embedding_model_name)

    def _get_connection(self):
        # каждый раз новый коннект – проще и безопаснее для воркера
        conn = mssql_python.connect(self._conn_str)
        return conn

    def _encode(self, text: str) -> List[float]:
        emb = self._model.encode([text])[0]
        return emb.tolist()

    def _parse_collecting_end_limit(
        self, collecting_end_limit_str: Optional[str]
    ) -> datetime:
        if not collecting_end_limit_str:
            return datetime.now(timezone.utc)

        # формат "2025-11-20T09:40:24.821Z"
        s = collecting_end_limit_str
        if s.endswith("Z"):
            s = s.replace("Z", "+00:00")
        return datetime.fromisoformat(s)

    def process_command(self, command: Dict[str, Any]) -> None:
        # структура команды (как в очереди):
        # {
        #   "UserId": "...",
        #   "Query": "...",
        #   "CollectingEndLimit": "2025-11-20T09:40:24.821Z",
        #   "Top": 20,
        #   "Limit": 500,
        #   "ExpiredOnly": false
        # }

        user_id = command.get("UserId")
        query = command.get("Query") or ""
        collecting_end_limit_str = command.get("CollectingEndLimit")
        top = int(command.get("Top") or 20)
        limit = int(command.get("Limit") or 500)
        expired_only = bool(command.get("ExpiredOnly") or False)

        collecting_end_limit = self._parse_collecting_end_limit(
            collecting_end_limit_str
        )

        logger.info("=== FAVORITE SEARCH REQUEST ===")
        logger.info("query=%s", query)
        logger.info("userId=%s", user_id)
        logger.info("top=%s", top)
        logger.info("collectingEndLimit=%s", collecting_end_limit)
        logger.info("expiredOnly=%s", expired_only)

        # 1. считаем эмбеддинг запроса
        embedding = self._encode(query)
        embedding_json = json.dumps(embedding)

        # 2. выполняем поиск в БД
        rows = self._search_notices(
            embedding_json=embedding_json,
            collecting_end_limit=collecting_end_limit,
            top=top,
        )

        # 3. печатаем топ-20 как у тебя в логах
        self._print_top(rows, top=top)

        # 4. сохраняем избранное (если есть user_id)
        if user_id:
            self._save_favorites(user_id, query, rows)

    def _search_notices(
        self,
        embedding_json: str,
        collecting_end_limit: datetime,
        top: int,
    ) -> List[Dict[str, Any]]:
        sql = """
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
        WHERE e.Model = ? AND (n.CollectingEnd IS NULL OR n.CollectingEnd >= ?)
        ORDER BY Similarity DESC, n.UpdatedAt DESC
        """

        model_name = EMBEDDING_MODEL_NAME

        logger.info("SQL:\n%s", sql)
        logger.info(
            "PARAMS: [%s, '<EMBEDDING_JSON>', '%s', %s]",
            top,
            model_name,
            collecting_end_limit,
        )

        conn = self._get_connection()
        try:
            cursor = conn.cursor()
            cursor.execute(
                sql,
                (top, embedding_json, model_name, collecting_end_limit)
            )
            results = []
            for row in cursor.fetchall():
                results.append(
                    {
                        "NoticeId": row[0],
                        "PurchaseNumber": row[1],
                        "EntryName": row[2],
                        "PurchaseObjectInfo": row[3],
                        "Similarity": float(row[4]),
                    }
                )
            return results
        finally:
            conn.close()

    def _print_top(self, rows: List[Dict[str, Any]], top: int = 20) -> None:
        logger.info("")
        logger.info("TOP-%d RESULTS:", top)
        logger.info("=" * 80)

        for idx, row in enumerate(rows[:top], start=1):
            logger.info(
                "%d. score=%.4f\n   PurchaseNumber: %s\n   NoticeId:       %s\n   Object:         %s\n   EntryName:      %s\n%s",
                idx,
                row["Similarity"],
                row["PurchaseNumber"],
                row["NoticeId"],
                row["PurchaseObjectInfo"],
                row["EntryName"],
                "-" * 80,
            )

    def _save_favorites(
        self,
        user_id: str,
        query: str,
        rows: List[Dict[str, Any]],
    ) -> None:
        logger.info("")
        logger.info("Saving favorites:")
        logger.info("=" * 80)

        if not rows:
            logger.info("Нет результатов для сохранения.")
            return

        rows_to_save = rows[:MAX_FAVORITES_TO_SAVE]

        conn = self._get_connection()
        try:
            cursor = conn.cursor()

            # сначала создаём запись в таблице FavoriteSearches (если есть такая)
            # если у тебя другая схема, адаптируй под неё
            insert_search_sql = """
            INSERT INTO FavoriteSearches (UserId, Query, CreatedAt)
            OUTPUT inserted.Id
            VALUES (?, ?, SYSUTCDATETIME())
            """
            cursor.execute(insert_search_sql, (user_id, query))
            favorite_search_id = cursor.fetchone()[0]

            # затем — в FavoriteNotices
            insert_notice_sql = """
            INSERT INTO FavoriteNotices (FavoriteSearchId, UserId, NoticeId, Score)
            VALUES (?, ?, ?, ?)
            """

            # tqdm просто чтобы у тебя в логах оставалось "Batches: 100% |"
            for row in tqdm([rows_to_save], desc="Batches"):
                for item in row:
                    cursor.execute(
                        insert_notice_sql,
                        (
                            favorite_search_id,
                            user_id,
                            item["NoticeId"],
                            item["Similarity"],
                        ),
                    )

            conn.commit()
        finally:
            conn.close()


# ========================
#  ВОРКЕР ДЛЯ ОЧЕРЕДИ
# ========================

class FavoriteSearchWorker:
    def __init__(self, engine: FavoriteSearchEngine, rabbitmq_url: str, queue_name: str):
        self._engine = engine
        self._rabbitmq_url = rabbitmq_url
        self._queue_name = queue_name

        params = pika.URLParameters(self._rabbitmq_url)
        self._connection: BlockingConnection = pika.BlockingConnection(params)
        self._channel: BlockingChannel = self._connection.channel()

        # гарантируем, что очередь есть
        self._channel.queue_declare(queue=self._queue_name, durable=True)

    def _process_message(self, body: bytes) -> None:
        logger.info("message received")

        try:
            command = json.loads(body.decode("utf-8"))
        except Exception:
            logger.exception("Не удалось распарсить JSON, пропускаю сообщение")
            return

        try:
            self._engine.process_command(command)
        except IntegrityError as e:
            # FK по AspNetUsers — просто логируем и пропускаем
            msg = str(e)
            if "FK_FavoriteNotices_AspNetUsers_UserId" in msg:
                user_id = command.get("UserId")
                logger.warning(
                    "Пользователь %s не найден в AspNetUsers (FK ошибка). "
                    "Фавориты не сохранены, сообщение пропущено.",
                    user_id,
                )
            else:
                logger.exception("SQL IntegrityError при обработке, сообщение пропущено")
        except Exception:
            logger.exception("Общая ошибка при обработке сообщения, сообщение пропущено")

    def start(self) -> None:
        def callback(ch, method, properties, body):
            try:
                self._process_message(body)
            finally:
                # ВАЖНО: ACK ВСЕГДА, даже если внутри была ошибка
                # чтобы сообщение не зацикливалось
                ch.basic_ack(delivery_tag=method.delivery_tag)

        self._channel.basic_qos(prefetch_count=1)
        self._channel.basic_consume(
            queue=self._queue_name,
            on_message_callback=callback,
        )

        logger.info(
            "FavoriteSearchWorker запущен. Ожидание сообщений из очереди '%s'...",
            self._queue_name,
        )
        self._channel.start_consuming()


# ========================
#  MAIN
# ========================

def main():
    # грузим appsettings
    appsettings_path = resolve_appsettings_path()
    config = load_appsettings(appsettings_path)

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
        conn_str=DB_CONNECTION_STRING,
        embedding_model_name=EMBEDDING_MODEL_NAME,
    )

    # петля переподключения к RabbitMQ
    while True:
        try:
            worker = FavoriteSearchWorker(engine, rabbitmq_url, queue_name)
            worker.start()
        except pika.exceptions.AMQPConnectionError:
            logger.exception(
                "Ошибка подключения к RabbitMQ (%s). "
                "Пробую переподключиться через 5 секунд...",
                rabbitmq_url,
            )
            time.sleep(5)
        except KeyboardInterrupt:
            logger.info("Остановка по Ctrl+C")
            break
        except Exception:
            logger.exception(
                "Необработанная ошибка в воркере, перезапуск через 5 секунд..."
            )
            time.sleep(5)


if __name__ == "__main__":
    main()
