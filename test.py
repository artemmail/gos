#!/usr/bin/env python3
"""
Вспомогательный скрипт для ручной отправки выгруженных закупок.
Он архивирует каталог `out/<YYYY-MM-DD>` (по умолчанию текущая дата)
и POST-отправляет архив на заданный URL, печатая ход выполнения.
"""

import argparse
import datetime as dt
import io
import sys
from pathlib import Path
import zipfile

import requests


def build_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Zip & upload daily exports")
    parser.add_argument(
        "upload_url",
        help="Адрес для POST-отправки архива",
    )
    parser.add_argument(
        "--out-dir",
        default="out",
        help="Корневая папка с выгрузками (по умолчанию ./out)",
    )
    parser.add_argument(
        "--date",
        default=dt.date.today().isoformat(),
        help="Дата выгрузки в формате YYYY-MM-DD (по умолчанию сегодня)",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=600,
        help="Таймаут HTTP-запроса в секундах",
    )
    return parser.parse_args()


def zip_folder(src: Path) -> tuple[str, bytes]:
    print(f"[ZIP] Сбор файлов из {src}")
    files = [p for p in src.rglob("*") if p.is_file()]
    if not files:
        raise FileNotFoundError(f"В каталоге {src} нет файлов для архивации")

    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", compression=zipfile.ZIP_DEFLATED) as zip_out:
        for f in files:
            rel = f.relative_to(src)
            zip_out.write(f, rel.as_posix())
            print(f"[ZIP] Добавлен {rel}")
    buffer.seek(0)
    fname = f"notices_{src.name}.zip"
    print(f"[ZIP] Готово: {len(files)} файл(ов), размер {buffer.getbuffer().nbytes} байт")
    return fname, buffer.getvalue()


def upload_zip(url: str, filename: str, payload: bytes, timeout: int) -> None:
    print(f"[HTTP] Отправка {filename} на {url}")
    resp = requests.post(url, files={"file": (filename, payload, "application/zip")}, timeout=timeout)
    print(f"[HTTP] Статус: {resp.status_code}")
    resp.raise_for_status()
    print("[HTTP] Отправлено успешно")


def main() -> int:
    args = build_args()

    base_dir = Path(args.out_dir).expanduser().resolve()
    day_dir = base_dir / args.date
    print(f"[INIT] Базовая папка: {base_dir}")
    print(f"[INIT] Папка за дату {args.date}: {day_dir}")

    if not day_dir.exists() or not day_dir.is_dir():
        print(f"[ERROR] Каталог {day_dir} не найден")
        return 1

    try:
        fname, payload = zip_folder(day_dir)
        upload_zip(args.upload_url, fname, payload, args.timeout)
    except Exception as exc:  # noqa: BLE001
        print(f"[ERROR] {exc}")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())