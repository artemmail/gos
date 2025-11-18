#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
g.py — ЕИС (getDocsIP): пакет по номеру (getDocsByReestrNumber) без скачивания вложений,
с форматом и структурой, максимально совместимыми с eis_fetch_all.py:

- out/<purchaseNumber>/notice_<docKind>_<date>_<orig>.xml — один главный XML
- out/<purchaseNumber>/files/                           — каталог для будущих вложений
- out/<purchaseNumber>/manifest.tsv                    — в старом формате (# meta / # files)

Примеры:
    python g.py --token "ВАШ_ТОКЕН" --number 0175200001525000044 --subsystem PRIZ
    python g.py --token "ВАШ_ТОКЕН" --number 2910201216025000045  # PRIZ -> RGK подхватится автоматически
"""

import argparse
import datetime as dt
import io
import os
import re
import sys
import uuid
import zipfile
from pathlib import Path
from urllib.parse import urlparse, unquote, parse_qs

import requests
from lxml import etree


URL = "https://int44.zakupki.gov.ru/eis-integration/services/getDocsIP"
NS_SOAP = "http://schemas.xmlsoap.org/soap/envelope/"
NS_WS   = "http://zakupki.gov.ru/fz44/get-docs-ip/ws"

ATT_LOCALNAMES = {"url", "href", "docurl", "documenturl", "fileurl", "downloadurl"}

# типы уведомлений 44-ФЗ (как в исходном eis_fetch_all.py)
DOC_TYPES_44 = [
    "epNotificationEF2020",
    "epNotificationEOK2020",
    "epNotificationOK2020",
    "epNotificationEZK2020",
    "epNotificationES2020",
    "epNotificationEA2020",
    "epNotificationAE2020",
    "epNotificationSMB2020",
]


# ---------- utils ----------

def fmt_iso(d: dt.datetime) -> str:
    return d.strftime("%Y-%m-%dT%H:%M:%S")


def fmt_date(d: dt.datetime) -> str:
    return d.strftime("%Y-%m-%d")


def localname(tag: str) -> str:
    return tag.split("}")[-1] if "}" in tag else tag


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


def val(root: etree._Element, paths: list[str]) -> str:
    for p in paths:
        el = root.find(p)
        if el is not None and el.text:
            t = el.text.strip()
            if t:
                return t
    return ""


# ---------- SOAP helpers ----------

def build_getDocsByReestrNumber(token: str, reestr: str, subsystem: str = "PRIZ") -> str:
    """
    subsystemType:
      PRIZ — извещения / протоколы (определение поставщика),
      RGK  — реестр контрактов,
      и т.п.
    """
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
        <subsystemType>{subsystem}</subsystemType>
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
    """
    Возвращает (ok, archiveUrl, faultString|None)
    """
    root = etree.fromstring(xml_bytes)
    fault = root.find(f".//{{{NS_SOAP}}}Fault")
    if fault is not None:
        return False, None, (fault.findtext("faultstring") or "").strip()
    arch = root.find(".//{*}archiveUrl")
    if arch is not None and arch.text:
        return True, arch.text.strip(), None
    return True, None, None


def download_archive(sess: requests.Session, url: str, token: str):
    """
    Качает ZIP по archiveUrl.
    Делает фолбэк int -> int44.
    Не роняет скрипт на 404, просто возвращает None.
    """

    def _try(u: str):
        print(f"[DL] GET {u}")
        r = sess.get(u, headers={"individualPerson_token": token}, timeout=300)
        if r.status_code == 404:
            print(f"[WARN] 404 Not Found для {u}")
            return None
        r.raise_for_status()
        return r.content

    # 1-я попытка — как дали
    zbytes = _try(url)
    if zbytes is not None:
        return zbytes

    # фолбэк: int -> int44
    if "://int.zakupki.gov.ru" in url:
        alt = url.replace("://int.zakupki.gov.ru", "://int44.zakupki.gov.ru")
        print(f"[INFO] Пытаемся альтернативный URL: {alt}")
        zbytes = _try(alt)
        if zbytes is not None:
            return zbytes

    return None


# ---------- разбор XML ----------

def extract_details_and_links(xb: bytes) -> dict:
    root = etree.fromstring(xb)
    ln = localname

    doc_kind = ln(root.tag)
    purchase_number = val(root, [".//{*}purchaseNumber", ".//{*}notificationNumber"])
    ikz = val(root, [".//{*}IKZ", ".//{*}ikz"])
    placing_code = val(root, [".//{*}placingWay/{*}code"])
    placing_name = val(root, [".//{*}placingWay/{*}name"])

    customer_name = val(root, [".//{*}customer/{*}fullName",
                               ".//{*}customer/{*}shortName",
                               ".//{*}organizationName"])
    customer_inn  = val(root, [".//{*}customer/{*}INN", ".//{*}customer/{*}inn"])
    customer_kpp  = val(root, [".//{*}customer/{*}KPP", ".//{*}customer/{*}kpp"])

    max_price = val(root, [".//{*}maxPrice", ".//{*}initialSum", ".//{*}contractMaxPrice"])
    currency  = val(root, [".//{*}currency/{*}code", ".//{*}currency"])

    name      = val(root, [".//{*}purchaseObjectInfo",
                           ".//{*}subject",
                           ".//{*}purchaseName",
                           ".//{*}fullName"])
    publish_date = val(root, [".//{*}publishDate", ".//{*}docPublishDate", ".//{*}placementDate"])
    app_start    = val(root, [".//{*}applicationsStartDate", ".//{*}applicationStartDate"])
    app_end      = val(root, [".//{*}applicationsEndDate", ".//{*}applicationEndDate", ".//{*}endDate"])
    platform     = val(root, [
        ".//{*}electronicPlace/{*}name",
        ".//{*}electronicPlatformName", ".//{*}platformName",
        ".//{*}oosElectronicPlace/{*}name"
    ])

    okpd2 = set()
    for el in root.findall(".//{*}OKPD2/{*}code"):
        if el.text:
            okpd2.add(el.text.strip())
    if not okpd2:
        for el in root.findall(".//{*}OKPD/{*}code"):
            if el.text:
                okpd2.add(el.text.strip())

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


def planned_name(base: str, ordinal, prefix: str = "") -> str:
    """
    Совместимо с исходным скриптом:
      - int -> "001__file"
      - str ("p001_001") -> "p001_001__file"
    """
    base = sanitize_name(base) or "file"
    if prefix:
        if isinstance(ordinal, int):
            return f"{prefix}{ordinal:03d}__{base}"
        return f"{prefix}{ordinal}__{base}"
    if isinstance(ordinal, int):
        return f"{ordinal:03d}__{base}"
    return f"{ordinal}__{base}"


def save_manifest_row(folder: Path, data: dict, file_rows: list[dict]) -> None:
    """
    Копия save_manifest_row из eis_fetch_all.py — формат manifest.tsv 1:1.
    """
    manifest = folder / "manifest.tsv"
    hdr1 = [
        "purchaseNumber","docKind","placingCode","placingName",
        "customerName","customerINN","customerKPP",
        "maxPrice","currency","publishDate",
        "appStart","appEnd","platform","okpd2","name"
    ]
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


def choose_main_doc(docs: list[dict], subsystem: str) -> dict | None:
    """
    docs: список {name, xb, det}
    Возвращает один "главный" документ:
      - для PRIZ: docKind в DOC_TYPES_44 (уведомление)
      - иначе: первый по порядку
    """
    if not docs:
        return None

    if subsystem.upper() == "PRIZ":
        for d in docs:
            dk = (d["det"].get("docKind") or "").strip()
            if dk in DOC_TYPES_44:
                return d

    # если не нашли по списку или другая подсистема — берем первый
    return docs[0]


def is_no_data(resp_bytes: bytes) -> bool:
    """
    Быстрая проверка на <noData>true</noData> в ответе.
    """
    txt = resp_bytes.decode("utf-8", "ignore")
    return "<noData>true</noData>" in txt


# ---------- main ----------

def main():
    ap = argparse.ArgumentParser(
        description="ЕИС: пакет по номеру (getDocsByReestrNumber) без скачивания вложений."
    )
    ap.add_argument("--token", required=True,
                    help="individualPerson_token из PMD (физлицо)")
    ap.add_argument("--number", required=True,
                    help="reestrNumber (номер закупки / контракта в ЕИС)")
    ap.add_argument("--subsystem", default="PRIZ",
                    help="subsystemType (по умолчанию PRIZ; для контрактов — RGK)")
    ap.add_argument("--out-dir", default="out",
                    help="корневая папка для выгрузки (по умолчанию out)")
    args = ap.parse_args()

    token = args.token
    reestr = args.number
    subsystem = args.subsystem

    sess = requests.Session()
    sess.trust_env = False

    # sanity check XSD — как в eis_fetch_all.py
    try:
        rx = sess.get(URL + "?xsd=getDocsIP-ws-api.xsd", timeout=20)
        print(f"[XSD] HTTP {rx.status_code}")
        rx.raise_for_status()
    except Exception as e:
        print(f"[WARN] Не удалось проверить XSD: {e}")

    print(f"[REQ] getDocsByReestrNumber reestrNumber={reestr} subsystem={subsystem}")
    xml_req = build_getDocsByReestrNumber(token, reestr, subsystem)

    try:
        resp = soap_post(sess, xml_req)
    except Exception as e:
        print(f"[ERR] SOAP/HTTP: {e}")
        sys.exit(1)

    # авто-фолбэк: если по PRIZ noData=true, пробуем RGK
    if is_no_data(resp):
        print(f"[INFO] noData=true для subsystem={subsystem}")
        if subsystem.upper() == "PRIZ":
            print("[INFO] Пытаемся тот же номер как RGK (реестр контрактов)...")
            subsystem2 = "RGK"
            xml_req2 = build_getDocsByReestrNumber(token, reestr, subsystem2)
            try:
                resp2 = soap_post(sess, xml_req2)
            except Exception as e:
                print(f"[ERR] SOAP/HTTP (RGK): {e}")
                sys.exit(1)

            if is_no_data(resp2):
                print("[ERR] noData=true и для PRIZ, и для RGK — документов нет в getDocsIP.")
                try:
                    print("----- SOAP RESPONSE (PRIZ) -----")
                    print(resp.decode("utf-8", "ignore"))
                    print("----- SOAP RESPONSE (RGK) ------")
                    print(resp2.decode("utf-8", "ignore"))
                    print("----------- END ---------------")
                except Exception:
                    pass
                sys.exit(1)

            subsystem = subsystem2
            resp = resp2
            print("[INFO] Найдены данные в подсистеме RGK.")
        else:
            print(f"[ERR] noData=true для subsystem={subsystem}, другие подсистемы автоматически не пробуем.")
            try:
                print("----- SOAP RESPONSE START -----")
                print(resp.decode("utf-8", "ignore"))
                print("----- SOAP RESPONSE END -------")
            except Exception:
                pass
            sys.exit(1)

    ok, arch_url, err = parse_archive_url(resp)
    if not ok and err:
        print(f"[ERR] SOAP Fault: {err}")
        sys.exit(1)
    if not arch_url:
        print("[ERR] archiveUrl не вернулся в ответе (пустой пакет?). Полный ответ ниже.")
        try:
            print("----- SOAP RESPONSE START -----")
            print(resp.decode("utf-8", "ignore"))
            print("----- SOAP RESPONSE END -------")
        except Exception:
            pass
        sys.exit(1)

    print(f"[ARCH] archiveUrl: {arch_url}")

    # качаем ZIP с XML с фолбэком
    zbytes = download_archive(sess, arch_url, token)
    if not zbytes:
        print("[ERR] Не удалось загрузить ZIP с XML (даже после фолбэка).")
        return

    date_str = fmt_date(dt.datetime.now())
    out_root = Path(args.out_dir)
    out_root.mkdir(exist_ok=True)

    # распарсим все XML, соберем документы, ссылки и мету
    docs: list[dict] = []
    all_links: list[dict] = []
    meta: dict = {}
    purchase_number_for_dir = reestr

    with zipfile.ZipFile(io.BytesIO(zbytes)) as zf:
        xml_index = 0
        for name in zf.namelist():
            if not name.lower().endswith(".xml"):
                continue
            xml_index += 1
            xb = zf.read(name)
            det = extract_details_and_links(xb)

            pn = (det.get("purchaseNumber") or "").strip()
            if pn:
                purchase_number_for_dir = pn

            if not meta and det:
                meta = det

            print(f"[XML {xml_index}] {name} docKind={det.get('docKind')} purchaseNumber={det.get('purchaseNumber')}")

            docs.append({"name": name, "xb": xb, "det": det})

            # ссылки из этого документа
            for j, lnk in enumerate(det.get("links", []) or [], start=1):
                url_j = (lnk.get("url") or "").strip()
                if not url_j:
                    continue
                base_name = lnk.get("name") or guess_filename_from_url(url_j)
                # ordinal формируем как pXXX_YYY, где XXX — номер XML в пакете, YYY — номер ссылки в нем
                ordinal = f"p{xml_index:03d}_{j:03d}"
                all_links.append({
                    "ordinal": ordinal,
                    "source": "package",
                    "url": url_j,
                    "base_name": base_name,
                })

    # если вообще ничего нет
    if not docs:
        print("[ERR] В ZIP нет XML-документов.")
        return

    # выбираем главный документ (уведомление / первый)
    main_doc = choose_main_doc(docs, subsystem)
    if not main_doc:
        print("[ERR] Не удалось выбрать главный документ из пакета.")
        return

    folder = out_root / purchase_number_for_dir
    folder.mkdir(exist_ok=True)
    (folder / "files").mkdir(exist_ok=True)  # как в старом скрипте

    # сохраняем только один XML как notice_<docKind>_<date>_<orig>.xml
    doc_kind = (main_doc["det"].get("docKind") or "document").strip()
    orig_name = os.path.basename(main_doc["name"]) or "doc.xml"
    notice_name = f"notice_{doc_kind}_{date_str}_{sanitize_name(orig_name)}"
    (folder / notice_name).write_bytes(main_doc["xb"])
    print(f"[NOTICE] сохранён главный XML: {notice_name}")

    # готовим строки файлов для manifest.tsv (только плановые имена, без скачивания)
    file_rows = []
    for link in all_links:
        planned = planned_name(link["base_name"], link["ordinal"])
        file_rows.append({
            "ordinal": link["ordinal"],
            "source": link["source"],
            "url": link["url"],
            "saved_as": planned,
            "content_type": "",
            "bytes": "",
        })

    # если почему-то мета пустая — заполним минимально
    if not meta:
        meta = {
            "purchaseNumber": purchase_number_for_dir,
            "docKind": doc_kind,
            "placingCode": "",
            "placingName": "",
            "customerName": "",
            "customerINN": "",
            "customerKPP": "",
            "maxPrice": "",
            "currency": "",
            "publishDate": "",
            "appStart": "",
            "appEnd": "",
            "platform": "",
            "okpd2": "",
            "name": "",
        }

    save_manifest_row(folder, meta, file_rows)

    print(f"\n[OK] Готово. Главный XML: {(folder / notice_name).resolve()}")
    print(f"[OK] manifest.tsv: {(folder / 'manifest.tsv').resolve()}")
    print(f"[INFO] ссылок на вложения (без скачивания): {len(file_rows)}")


if __name__ == "__main__":
    main()
