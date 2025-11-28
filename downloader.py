#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ЕИС (getDocsIP): поиск уведомлений (PRIZ/44-ФЗ и опц. RI223/223-ФЗ),
извлечение ссылок на вложения, сохранение XML и manifest.tsv без скачивания файлов.
Опционально — «пакет по номеру закупки» (getDocsByReestrNumberRequest).

Вывод:
out/<purchaseNumber>/
    notice_<docType>_<yyyy-mm-dd>_<original_xml_name>.xml
    package_<yyyy-mm-dd>_<K>.xml         # XML из «пакета по номеру» (если включено)
    files/                                # каталог для будущих загрузок
    manifest.tsv                          # метаданные + список ссылок (без контента)

Пример:
    python eis_fetch_all.py --token "ВАШ_ТОКЕН" --days 3 --regions 77 --limit 0 --fetch-by-purchase
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

DOC_TYPES_44 = [
    "epNotificationEF2020",
    "epNotificationEOK2020",
    "epNotificationOK2020",
    "epNotificationEZK2020",
    "epNotificationES2020",  # <<< ДОБАВЬ ЭТО
    "epNotificationEA2020",  # <<< ВАЖНО
    "epNotificationAE2020",  # <<< РЕДКО НО БЫВАЕТ
    "epNotificationSMB2020", # <<< Закупки для СМП
]

DOC_TYPES_223 = ["purchaseNotice"]

REGIONS_ALL = [
 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,
 31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,
 58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,83,86,87,89,90,91,92
]

ATT_LOCALNAMES = {"url", "href", "docurl", "documenturl", "fileurl", "downloadurl"}

# ---------- utils ----------
def fmt_iso(d: dt.datetime) -> str: return d.strftime("%Y-%m-%dT%H:%M:%S")
def fmt_date(d: dt.datetime) -> str: return d.strftime("%Y-%m-%d")
def localname(tag: str) -> str: return tag.split("}")[-1] if "}" in tag else tag

def sanitize_name(name: str, maxlen: int = 180) -> str:
    name = re.sub(r'[/\\?%*:|"<>\r\n\t]', "_", name)
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

# ---------- SOAP helpers ----------
def build_getDocsByOrgRegion(token: str, region: int, subsystem: str, doc_type: str, exact_date: str) -> str:
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
        URL, data=xml.encode("utf-8"),
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
    return True, None, None

# ---------- extract ----------
def extract_details_and_links(xb: bytes) -> dict:
    root = etree.fromstring(xb)
    ln = localname

    doc_kind = ln(root.tag)
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

    links = []
    seen = set()

    # текстовые URL-элементы
    for el in root.iter():
        ln_tag = ln(el.tag).lower()
        if ln_tag in ATT_LOCALNAMES and el.text:
            url = el.text.strip()
            if url.lower().startswith("http") and url not in seen:
                seen.add(url)
                file_name = ""
                parent = el.getparent() if hasattr(el, "getparent") else None
                if parent is not None:
                    for tag in ("fileName", "documentName", "name", "docName"):
                        cand = parent.find(f".//{{*}}{tag}")
                        if cand is not None and cand.text and cand.text.strip():
                            file_name = cand.text.strip()
                            break
                links.append({"url": url, "name": file_name})

    # URL в атрибутах
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

# ---------- main ----------
def main():
    ap = argparse.ArgumentParser(description="ЕИС: поиск → извлечение ссылок (без скачивания вложений).")
    ap.add_argument("--token", required=True, help="individualPerson_token из PMD (физлицо)")
    ap.add_argument("--days", type=int, default=7, help="сколько последних дней (суточными окнами)")
    ap.add_argument("--regions", help="например 77,78,50 (по умолчанию все регионы)")
    ap.add_argument("--include223", action="store_true", help="перебирать также RI223/223-ФЗ (purchaseNotice)")
    ap.add_argument("--sleep", type=float, default=0.4, help="пауза между запросами, сек")
    ap.add_argument("--limit", type=int, default=0, help="0 = без лимита по числу найденных закупок")
    ap.add_argument("--fetch-by-purchase", action="store_true", help="дотягивать «пакет по номеру закупки» (XML)")
    ap.add_argument("--upload-url", help="куда отправлять zip-архив с выгрузкой (POST)")
    ap.add_argument("--missing-check-url", help="endpoint для проверки существующих закупок (POST)")
    args = ap.parse_args()

    regs = REGIONS_ALL if not args.regions else [int(x) for x in args.regions.split(",") if x.strip()]
    now = dt.datetime.now()
    start = now - dt.timedelta(days=args.days)

    sess = requests.Session()
    sess.trust_env = False

    def filter_missing_numbers(region: int, purchase_numbers: list[str]) -> set[str]:
        if not args.missing_check_url or not purchase_numbers:
            return set(purchase_numbers)

        payload = {
            "region": f"{region:02d}",
            "purchaseNumbers": purchase_numbers,
        }

        try:
            resp = sess.post(args.missing_check_url, json=payload, timeout=120)
            resp.raise_for_status()
            data = resp.json()
            if isinstance(data, list):
                return {str(x) for x in data}
            print(f"[SKIP] Некорректный ответ от {args.missing_check_url}, продолжаем без фильтрации")
        except Exception as exc:
            print(f"[SKIP] Ошибка при проверке существующих закупок: {exc}")

        return set(purchase_numbers)

    # sanity check
    rx = sess.get(URL + "?xsd=getDocsIP-ws-api.xsd", timeout=20)
    print(f"[XSD] HTTP {rx.status_code}")
    rx.raise_for_status()

    out_root = Path("out"); out_root.mkdir(exist_ok=True)
    seen_numbers = set()
    total_rows = 0

    def save_manifest_row(folder: Path, data: dict, file_rows: list[dict]) -> Path:
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
        return manifest

    def planned_name(base: str, ordinal: str | int, prefix: str = "") -> str:
        base = sanitize_name(base) or "file"
        if prefix:
            return f"{prefix}{ordinal:03d}__{base}" if isinstance(ordinal, int) else f"{prefix}{ordinal}__{base}"
        return f"{int(ordinal):03d}__{base}" if isinstance(ordinal, int) else f"{ordinal}__{base}"

    def scan_day(region: int, date_str: str, subsystem: str, doc_types: list[str], region_files: set[Path]):
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

            try:
                z = sess.get(url, headers={"individualPerson_token": args.token}, timeout=300)
                z.raise_for_status()
                zbytes = z.content
            except Exception as e:
                print(f"[{region:02d}] {date_str} download-zip(XMLs): {e}")
                continue

            with zipfile.ZipFile(io.BytesIO(zbytes)) as zf:
                batch = []
                batch_numbers = set()
                for name in zf.namelist():
                    if not name.lower().endswith(".xml"):
                        continue
                    xb = zf.read(name)

                    det = extract_details_and_links(xb)
                    num = (det["purchaseNumber"] or "").strip()
                    if not num or num in seen_numbers or num in batch_numbers:
                        continue

                    batch_numbers.add(num)
                    batch.append((num, name, xb, det))

                if not batch:
                    continue

                missing_numbers = filter_missing_numbers(region, list(batch_numbers))
                missing_set = set(missing_numbers)

                for num in batch_numbers:
                    seen_numbers.add(num)

                for num, name, xb, det in batch:
                    if num not in missing_set:
                        continue

                    total_rows += 1

                    print(f"  • [{region:02d}] {date_str} {num} | {det['placingName'] or '—'} | {det['maxPrice'] or '—'} | {det['name'] or '—'}")
                    folder = out_root / f"{date_str}_{region:02d}" / num
                    (folder / "files").mkdir(parents=True, exist_ok=True)

                    notice_fname = f"notice_{dt_code}_{date_str}_{sanitize_name(os.path.basename(name))}"
                    notice_path = folder / notice_fname
                    notice_path.write_bytes(xb)
                    region_files.add(notice_path)

                    file_rows = []
                    # вместо скачивания: фиксируем плановые имена
                    for i, link in enumerate(det.get("links", []), start=1):
                        url_i = link["url"]
                        base_name = link["name"] or guess_filename_from_url(url_i)
                        planned = planned_name(base_name, i)
                        file_rows.append({
                            "ordinal": i, "source": "notice", "url": url_i,
                            "saved_as": planned, "content_type": "", "bytes": ""
                        })

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
                                        pkg_path = folder / f"package_{date_str}_{k:03d}.xml"
                                        pkg_path.write_bytes(xb2)
                                        region_files.add(pkg_path)
                                        det2 = extract_details_and_links(xb2)
                                        for j, lnk in enumerate(det2.get("links", []), start=1):
                                            url_j = lnk["url"]
                                            base_name = lnk["name"] or guess_filename_from_url(url_j)
                                            planned = planned_name(base_name, f"p{k:03d}_{j:03d}")
                                            file_rows.append({
                                                "ordinal": f"p{k:03d}_{j:03d}", "source": "package", "url": url_j,
                                                "saved_as": planned, "content_type": "", "bytes": ""
                                            })
                        except Exception:
                            pass

                    manifest_path = save_manifest_row(folder, det, file_rows)
                    region_files.add(manifest_path)

                    if args.limit > 0 and total_rows >= args.limit:
                        return "stop"
        return "ok"

    for r in regs:
        print(f"\n=== Регион {str(r).zfill(2)} ===")
        region_files: set[Path] = set()
        day = start
        while day.date() <= now.date():
            d = fmt_date(day)
            res = scan_day(r, d, "PRIZ", DOC_TYPES_44, region_files)
            if res == "stop": break
            if args.include223:
                res = scan_day(r, d, "RI223", DOC_TYPES_223, region_files)
                if res == "stop": break
            if args.limit > 0 and total_rows >= args.limit: break
            day += dt.timedelta(days=1)
            time.sleep(args.sleep)
        if args.limit > 0 and total_rows >= args.limit: break

        if args.upload_url:
            region_files = {p for p in region_files if p.exists() and p.is_file()}
            if not region_files:
                print(f"[UPLOAD] Регион {r:02d}: нет файлов для отправки")
            else:
                zip_buf = io.BytesIO()
                with zipfile.ZipFile(zip_buf, "w", compression=zipfile.ZIP_DEFLATED) as zip_out:
                    for path in sorted(region_files):
                        zip_out.write(path, path.relative_to(out_root).as_posix())

                zip_buf.seek(0)
                fname = f"notices_{fmt_date(now)}_{r:02d}.zip"
                try:
                    resp = requests.post(
                        args.upload_url,
                        files={"file": (fname, zip_buf.getvalue(), "application/zip")},
                        timeout=600,
                    )
                    print(f"[UPLOAD] Регион {r:02d} HTTP {resp.status_code}")
                    resp.raise_for_status()
                except Exception as exc:
                    print(f"[UPLOAD] Регион {r:02d} ошибка отправки: {exc}")

    if total_rows == 0:
        print("\nИтог: совпадений не найдено.")
    else:
        print(f"\nИтог: обработано закупок: {total_rows}. Смотри папку: {out_root.resolve()}")

if __name__ == "__main__":
    main()
