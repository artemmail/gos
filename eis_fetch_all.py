#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ЕИС (getDocsIP): поиск уведомлений о закупках (PRIZ/44-ФЗ и опционально RI223/223-ФЗ),
извлечение ссылок на вложения + скачивание всех найденных вложений.
Опционально — добираем «пакет по номеру закупки» (getDocsByReestrNumberRequest).

Структура вывода:
out/<purchaseNumber>/
    notice_<docType>_<yyyy-mm-dd>_<original_xml_name>.xml
    package_<yyyy-mm-dd>_<K>.xml           # XML из "пакета по номеру" (если включено)
    files/
        <ordinal>__<safe_name>.[ext]       # скачанные вложения
    manifest.tsv                           # метаданные + список файлов

Пример запуска:
    python eis_fetch_all.py --token "ВАШ_ТОКЕН" --days 3 --regions 77 --limit 0 --download-attachments --fetch-by-purchase
"""

import argparse
import datetime as dt
import io
import os
import re
import time
import uuid
import zipfile
from pathlib import Path
from urllib.parse import urlparse, unquote, parse_qs

import requests
from lxml import etree

URL = "https://int44.zakupki.gov.ru/eis-integration/services/getDocsIP"
NS_SOAP = "http://schemas.xmlsoap.org/soap/envelope/"
NS_WS   = "http://zakupki.gov.ru/fz44/get-docs-ip/ws"

# Частые типы уведомлений 44-ФЗ (PRIZ). Список можно расширять при необходимости.
DOC_TYPES_44 = [
    "epNotificationEF2020",   # электронный аукцион
    "epNotificationEOK2020",  # электронный открытый конкурс
    "epNotificationOK2020",   # открытый конкурс
    "epNotificationEZK2020",  # электронный запрос котировок
]
# 223-ФЗ (RI223)
DOC_TYPES_223 = ["purchaseNotice"]

REGIONS_ALL = [
 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,
 31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,
 58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,83,86,87,89,90,91,92
]

# Ключевые слова для фильтра "разработка/ПО". Можно ослабить/расширить.
KEYWORDS = [
    r"разработ", r"доработ", r"модерниз", r"создан", r"внедрени",
    r"программ", r"\bПО\b", r"\bИС\b", r"software",
    r"информационн", r"автоматизац", r"портал", r"веб[- ]?разработ", r"сайт", r"мобильн"
]

# Локальные имена элементов, где часто лежат URL вложений
ATT_LOCALNAMES = {"url", "href", "docurl", "documenturl", "fileurl", "downloadurl"}


# ----------------------- Утилиты -----------------------
def fmt_iso(d: dt.datetime) -> str:
    return d.strftime("%Y-%m-%dT%H:%M:%S")

def fmt_date(d: dt.datetime) -> str:
    return d.strftime("%Y-%m-%d")

def localname(tag: str) -> str:
    return tag.split("}")[-1] if "}" in tag else tag

def sanitize_name(name: str, maxlen: int = 180) -> str:
    name = re.sub(r"[/\\?%*:|\"<>]", "_", name)
    name = re.sub(r"\s+", " ", name).strip()
    return (name[:maxlen]).rstrip("._ ")

def guess_filename_from_url(u: str) -> str:
    p = urlparse(u)
    fname = os.path.basename(p.path) or "file"
    fname = unquote(fname)
    qs = parse_qs(p.query)
    if not os.path.splitext(fname)[1]:
        for k in ("filename", "fileName", "name"):
            if k in qs and qs[k]:
                v = qs[k][0]
                if v:
                    fname = unquote(v)
                    break
    return sanitize_name(fname)

def ensure_ext(fname: str, content_type: str | None) -> str:
    """Если у файла нет расширения — попробуем добавить по Content-Type."""
    if os.path.splitext(fname)[1]:
        return fname
    if not content_type:
        return fname
    mapping = {
        "application/pdf": ".pdf",
        "application/zip": ".zip",
        "application/xml": ".xml",
        "text/xml": ".xml",
        "text/plain": ".txt",
        "application/msword": ".doc",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document": ".docx",
        "application/vnd.ms-excel": ".xls",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": ".xlsx",
        "application/vnd.ms-powerpoint": ".ppt",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation": ".pptx",
        "image/jpeg": ".jpg",
        "image/png": ".png",
        "image/tiff": ".tif",
    }
    ct = (content_type or "").lower().split(";")[0].strip()
    ext = mapping.get(ct)
    return fname + ext if ext else fname

def parse_disposition_filename(content_disp: str | None) -> str | None:
    if not content_disp:
        return None
    # Content-Disposition: attachment; filename="name.ext"; filename*=UTF-8''name.ext
    m = re.search(r'filename\*=(?:UTF-8\'\')?([^;]+)', content_disp, flags=re.I)
    if m:
        return sanitize_name(unquote(m.group(1).strip().strip('"')))
    m = re.search(r'filename="?([^";]+)"?', content_disp, flags=re.I)
    if m:
        return sanitize_name(unquote(m.group(1).strip()))
    return None

def any_kw(txt: str) -> bool:
    low = txt.lower()
    return any(re.search(p, low) for p in KEYWORDS)

def xml_text(xb: bytes) -> str:
    return xb.decode("utf-8", "ignore")

def val(root: etree._Element, paths: list[str]) -> str:
    for p in paths:
        el = root.find(p)
        if el is not None and el.text:
            t = el.text.strip()
            if t:
                return t
    return ""


# ----------------------- SOAP helpers -----------------------
def build_getDocsByOrgRegion(token: str, region: int, subsystem: str, doc_type: str, exact_date: str) -> str:
    # Порядок элементов ВАЖЕН: orgRegion → subsystemType → documentType44 → periodInfo/exactDate
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<soapenv:Envelope xmlns:soapenv="{NS_SOAP}" xmlns:ws="{NS_WS}">
  <soapenv:Header>
    <individualPerson_token>{token}</individualPerson_token>
  </soapenv:Header>
  <soapenv:Body>
    <ws:getDocsByOrgRegionRequest>
      <index>
        <id>{uuid.uuid4()}</id>
        <createDateTime>{fmt_iso(dt.datetime.now())}</createDateTime>
        <mode>PROD</mode>
      </index>
      <selectionParams>
        <orgRegion>{str(region).zfill(2)}</orgRegion>
        <subsystemType>{subsystem}</subsystemType>
        <documentType44>{doc_type}</documentType44>
        <periodInfo><exactDate>{exact_date}</exactDate></periodInfo>
      </selectionParams>
    </ws:getDocsByOrgRegionRequest>
  </soapenv:Body>
</soapenv:Envelope>""".strip()

def build_getDocsByReestrNumber(token: str, reestr: str) -> str:
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<soapenv:Envelope xmlns:soapenv="{NS_SOAP}" xmlns:ws="{NS_WS}">
  <soapenv:Header>
    <individualPerson_token>{token}</individualPerson_token>
  </soapenv:Header>
  <soapenv:Body>
    <ws:getDocsByReestrNumberRequest>
      <index>
        <id>{uuid.uuid4()}</id>
        <createDateTime>{fmt_iso(dt.datetime.now())}</createDateTime>
        <mode>PROD</mode>
      </index>
      <selectionParams>
        <subsystemType>PRIZ</subsystemType>
        <reestrNumber>{reestr}</reestrNumber>
      </selectionParams>
    </ws:getDocsByReestrNumberRequest>
  </soapenv:Body>
</soapenv:Envelope>""".strip()

def soap_post(sess: requests.Session, xml: str) -> bytes:
    r = sess.post(
        URL,
        data=xml.encode("utf-8"),
        headers={"Content-Type": "text/xml; charset=utf-8"},
        timeout=120,
    )
    r.raise_for_status()
    return r.content

def parse_archive_url(xml_bytes: bytes) -> tuple[bool, str | None, str | None]:
    root = etree.fromstring(xml_bytes)
    fault = root.find(f".//{{{NS_SOAP}}}Fault")
    if fault is not None:
        return False, None, (fault.findtext("faultstring") or "").strip()
    arch = root.find(".//{*}archiveUrl")
    if arch is not None and arch.text:
        return True, arch.text.strip(), None
    # «нет данных» — это тоже окей
    return True, None, None


# ----------------------- Извлечение деталей и ссылок -----------------------
def extract_details_and_links(xb: bytes) -> dict:
    root = etree.fromstring(xb)

    doc_kind = localname(root.tag)
    purchase_number = val(root, [".//{*}purchaseNumber", ".//{*}notificationNumber"])
    ikz = val(root, [".//{*}IKZ", ".//{*}ikz"])
    placing_code = val(root, [".//{*}placingWay/{*}code"])
    placing_name = val(root, [".//{*}placingWay/{*}name"])

    customer_name = val(root, [".//{*}customer/{*}fullName", ".//{*}customer/{*}shortName", ".//{*}organizationName"])
    customer_inn  = val(root, [".//{*}customer/{*}INN", ".//{*}customer/{*}inn"])
    customer_kpp  = val(root, [".//{*}customer/{*}KPP", ".//{*}customer/{*}kpp"])

    max_price = val(root, [".//{*}maxPrice", ".//{*}initialSum", ".//{*}contractMaxPrice"])
    currency  = val(root, [".//{*}currency/{*}code", ".//{*}currency"])

    name      = val(root, [".//{*}purchaseObjectInfo", ".//{*}subject", ".//{*}purchaseName", ".//{*}fullName"])
    publish_date = val(root, [".//{*}publishDate", ".//{*}docPublishDate", ".//{*}placementDate"])
    app_start = val(root, [".//{*}applicationsStartDate", ".//{*}applicationStartDate"])
    app_end   = val(root, [".//{*}applicationsEndDate",   ".//{*}applicationEndDate", ".//{*}endDate"])
    platform  = val(root, [".//{*}electronicPlace/{*}name",
                           ".//{*}electronicPlatformName", ".//{*}platformName",
                           ".//{*}oosElectronicPlace/{*}name"])

    okpd2 = set()
    for el in root.findall(".//{*}OKPD2/{*}code"):
        if el.text: okpd2.add(el.text.strip())
    if not okpd2:
        for el in root.findall(".//{*}OKPD/{*}code"):
            if el.text: okpd2.add(el.text.strip())

    # Сбор ссылок на вложения:
    links = []
    seen = set()

    # 1) Текстовые значения известных URL-элементов
    for el in root.iter():
        ln = localname(el.tag).lower()
        if ln in ATT_LOCALNAMES and el.text:
            url = el.text.strip()
            if url.lower().startswith("http") and url not in seen:
                seen.add(url)
                file_name = ""
                parent = el.getparent() if hasattr(el, "getparent") else None
                if parent is not None:
                    # аккуратно ищем соседние поля с именем
                    for tag in ("fileName", "documentName", "name", "docName"):
                        cand = parent.find(f".//{{*}}{tag}")
                        if cand is not None and cand.text and cand.text.strip():
                            file_name = cand.text.strip()
                            break
                links.append({"url": url, "name": file_name})

    # 2) Возможные URL в атрибутах (href, url, link)
    for el in root.iter():
        for attr in ("href", "url", "link"):
            v = el.attrib.get(attr)
            if v and v.lower().startswith("http") and v not in seen:
                seen.add(v)
                links.append({"url": v, "name": ""})

    return {
        "docKind": doc_kind,
        "purchaseNumber": purchase_number,
        "ikz": ikz,
        "placingCode": placing_code,
        "placingName": placing_name,
        "customerName": customer_name,
        "customerINN": customer_inn,
        "customerKPP": customer_kpp,
        "maxPrice": max_price,
        "currency": currency,
        "name": name,
        "publishDate": publish_date,
        "appStart": app_start,
        "appEnd": app_end,
        "platform": platform,
        "okpd2": ",".join(sorted(okpd2)) if okpd2 else "",
        "links": links,
    }


# ----------------------- Скачивание -----------------------
def download_with_token(sess: requests.Session, url: str, token: str, dst_path: Path,
                        max_bytes: int | None = None, chunk: int = 128 * 1024) -> tuple[str | None, int]:
    """
    Скачивает URL с заголовком individualPerson_token. Возвращает (Content-Type, bytes_written).
    Если нет расширения — добавит по Content-Type. Также учитывает filename из Content-Disposition.
    """
    headers = {"individualPerson_token": token}
    with sess.get(url, headers=headers, timeout=300, stream=True) as r:
        r.raise_for_status()
        # имя по Content-Disposition (если пригодится)
        cd_name = parse_disposition_filename(r.headers.get("Content-Disposition"))
        ct = r.headers.get("Content-Type")
        total = 0

        # если сервер предложил имя файла — заменим базовое
        final_path = dst_path
        if cd_name:
            final_path = dst_path.with_name(sanitize_name(cd_name))

        with open(final_path, "wb") as f:
            for b in r.iter_content(chunk_size=chunk):
                if not b:
                    continue
                f.write(b)
                total += len(b)
                if max_bytes and total > max_bytes:
                    raise RuntimeError(f"File too large (> {max_bytes} bytes): {url}")

    # Добавим расширение по Content-Type, если до сих пор его нет
    if not os.path.splitext(final_path.name)[1] and ct:
        new_name = ensure_ext(final_path.name, ct)
        if new_name != final_path.name:
            new_path = final_path.with_name(new_name)
            try:
                final_path.rename(new_path)
                final_path = new_path
            except Exception:
                pass

    return ct, total


# ----------------------- Основной процесс -----------------------
def main():
    ap = argparse.ArgumentParser(description="ЕИС: поиск → извлечение ссылок → скачивание вложений.")
    ap.add_argument("--token", required=True, help="individualPerson_token из PMD (физлицо)")
    ap.add_argument("--days", type=int, default=7, help="сколько последних дней (суточными окнами)")
    ap.add_argument("--regions", help="например 77,78,50 (по умолчанию все регионы)")
    ap.add_argument("--include223", action="store_true", help="перебирать также RI223/223-ФЗ (purchaseNotice)")
    ap.add_argument("--sleep", type=float, default=0.4, help="пауза между запросами, сек")
    ap.add_argument("--limit", type=int, default=0, help="0 = без лимита по числу найденных закупок")
    ap.add_argument("--download-attachments", dest="download_attachments", action="store_true",
                    help="скачивать вложения из уведомления")
    ap.add_argument("--fetch-by-purchase", action="store_true",
                    help="дотягивать «пакет по номеру закупки» и его вложения")
    ap.add_argument("--max-file-mb", type=int, default=200,
                    help="ограничить размер одного файла (MB); 0 = без ограничения")
    args = ap.parse_args()

    regs = REGIONS_ALL if not args.regions else [int(x) for x in args.regions.split(",") if x.strip()]
    now = dt.datetime.now()
    start = now - dt.timedelta(days=args.days)
    max_bytes = args.max_file_mb * 1024 * 1024 if args.max_file_mb > 0 else None

    sess = requests.Session()
    sess.trust_env = False

    # sanity check: доступен XSD?
    rx = sess.get(URL + "?xsd=getDocsIP-ws-api.xsd", timeout=20)
    print(f"[XSD] HTTP {rx.status_code}")
    rx.raise_for_status()

    out_root = Path("out"); out_root.mkdir(exist_ok=True)
    seen_numbers = set()
    total_rows = 0

    def save_manifest_row(folder: Path, data: dict, file_rows: list[dict]):
        manifest = folder / "manifest.tsv"
        hdr1 = ["purchaseNumber","docKind","placingCode","placingName","customerName","customerINN","customerKPP",
                "maxPrice","currency","publishDate","appStart","appEnd","platform","okpd2","name"]
        hdr2 = ["ordinal","source","url","saved_as","content_type","bytes"]
        first_write = not manifest.exists()
        with manifest.open("a", encoding="utf-8") as f:
            if first_write:
                f.write("# meta\n")
                f.write("\t".join(hdr1) + "\n")
            f.write("\t".join([data.get(k, "") or "" for k in hdr1]) + "\n")
            if file_rows:
                if first_write:
                    f.write("# files\n")
                    f.write("\t".join(hdr2) + "\n")
                for r in file_rows:
                    f.write("\t".join([str(r.get(k, "") or "") for k in hdr2]) + "\n")

    def scan_day(region: int, date_str: str, subsystem: str, doc_types: list[str]):
        nonlocal total_rows
        for dt_code in doc_types:
            xml = build_getDocsByOrgRegion(args.token, region, subsystem, dt_code, date_str)
            try:
                resp = soap_post(sess, xml)
            except Exception as e:
                print(f"[{region:02d}] {date_str} {subsystem}:{dt_code} HTTP/SOAP: {e}")
                continue
            ok, url, err = parse_archive_url(resp)
            if not ok and err:
                if "token" in err.lower():
                    print(f"[AUTH] {err}"); return "stop"
                print(f"[{region:02d}] {date_str} {subsystem}:{dt_code} ERR: {err}")
                continue
            if not url:
                continue

            # получаем архив
            try:
                z = sess.get(url, headers={"individualPerson_token": args.token}, timeout=300)
                z.raise_for_status()
                zbytes = z.content
            except Exception as e:
                print(f"[{region:02d}] {date_str} download: {e}")
                continue

            with zipfile.ZipFile(io.BytesIO(zbytes)) as zf:
                for name in zf.namelist():
                    if not name.lower().endswith(".xml"):
                        continue
                    xb = zf.read(name)
                    if not any_kw(xml_text(xb)):
                        continue

                    det = extract_details_and_links(xb)
                    num = (det["purchaseNumber"] or "").strip()
                    if not num:
                        continue
                    if num in seen_numbers:
                        continue
                    seen_numbers.add(num)
                    total_rows += 1

                    print(f"  • [{region:02d}] {date_str} {num} | {det['placingName'] or '—'} | {det['maxPrice'] or '—'} | {det['name'] or '—'}")
                    folder = out_root / num
                    (folder / "files").mkdir(parents=True, exist_ok=True)

                    # сохраняем исходный XML уведомления
                    notice_fname = f"notice_{dt_code}_{date_str}_{sanitize_name(os.path.basename(name))}"
                    (folder / notice_fname).write_bytes(xb)

                    # скачиваем вложения из уведомления
                    file_rows = []
                    if args.download_attachments and det["links"]:
                        for i, link in enumerate(det["links"], start=1):
                            url_i = link["url"]
                            base_name = link["name"] or guess_filename_from_url(url_i)
                            base_name = sanitize_name(base_name) or "file"
                            dst = folder / "files" / f"{i:03d}__{base_name}"
                            try:
                                ct, sz = download_with_token(sess, url_i, args.token, dst, max_bytes=max_bytes)
                                file_rows.append({
                                    "ordinal": i, "source": "notice", "url": url_i,
                                    "saved_as": dst.name if dst.exists() else base_name,
                                    "content_type": ct or "", "bytes": sz
                                })
                                time.sleep(0.1)
                            except Exception as e:
                                file_rows.append({
                                    "ordinal": i, "source": "notice", "url": url_i,
                                    "saved_as": f"ERROR: {e}", "content_type": "", "bytes": ""
                                })

                    # опционально — «пакет по номеру закупки»
                    if args.fetch_by_purchase:
                        xml2 = build_getDocsByReestrNumber(args.token, num)
                        try:
                            resp2 = soap_post(sess, xml2)
                            ok2, url2, _ = parse_archive_url(resp2)
                            if ok2 and url2:
                                zp = sess.get(url2, headers={"individualPerson_token": args.token}, timeout=300)
                                zp.raise_for_status()
                                with zipfile.ZipFile(io.BytesIO(zp.content)) as z2:
                                    k = 0
                                    for nm in z2.namelist():
                                        if not nm.lower().endswith(".xml"):
                                            continue
                                        xb2 = z2.read(nm)
                                        k += 1
                                        (folder / f"package_{date_str}_{k:03d}.xml").write_bytes(xb2)
                                        det2 = extract_details_and_links(xb2)
                                        if args.download_attachments and det2["links"]:
                                            for j, lnk in enumerate(det2["links"], start=1):
                                                url_j = lnk["url"]
                                                base_name = lnk["name"] or guess_filename_from_url(url_j)
                                                base_name = sanitize_name(base_name) or "file"
                                                dst2 = folder / "files" / f"p{k:03d}_{j:03d}__{base_name}"
                                                try:
                                                    ct2, sz2 = download_with_token(sess, url_j, args.token, dst2, max_bytes=max_bytes)
                                                    file_rows.append({
                                                        "ordinal": f"p{k:03d}_{j:03d}", "source": "package", "url": url_j,
                                                        "saved_as": dst2.name if dst2.exists() else base_name,
                                                        "content_type": ct2 or "", "bytes": sz2
                                                    })
                                                    time.sleep(0.1)
                                                except Exception as e:
                                                    file_rows.append({
                                                        "ordinal": f"p{k:03d}_{j:03d}", "source": "package", "url": url_j,
                                                        "saved_as": f"ERROR: {e}", "content_type": "", "bytes": ""
                                                    })
                        except Exception:
                            pass

                    # обновляем манифест
                    save_manifest_row(folder, det, file_rows)

                    if args.limit > 0 and total_rows >= args.limit:
                        return "stop"
        return "ok"

    # основной цикл: регионы × дни
    for r in regs:
        print(f"\n=== Регион {str(r).zfill(2)} ===")
        day = start
        while day.date() <= now.date():
            d = fmt_date(day)
            res = scan_day(r, d, "PRIZ", DOC_TYPES_44)
            if res == "stop":
                break
            if args.include223:
                res = scan_day(r, d, "RI223", DOC_TYPES_223)
                if res == "stop":
                    break
            if args.limit > 0 and total_rows >= args.limit:
                break
            day += dt.timedelta(days=1)
            time.sleep(args.sleep)
        if args.limit > 0 and total_rows >= args.limit:
            break

    if total_rows == 0:
        print("\nИтог: совпадений не найдено.")
    else:
        print(f"\nИтог: обработано закупок: {total_rows}. Смотри папку: {out_root.resolve()}")

if __name__ == "__main__":
    main()
