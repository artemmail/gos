using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace FabrikantGrabber
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            args = new string[] { "https://www.fabrikant.ru/v2/trades/procedure/view/C5b_EKEJiiyvLHpZij08zg", "c:/xml" };
            if (args.Length < 2)
            {
                Console.WriteLine("Использование:");
                Console.WriteLine("  FabrikantGrabber <procedureIdOrUrl> <outputFolder>");
                Console.WriteLine();
                Console.WriteLine("Пример:");
                Console.WriteLine("  FabrikantGrabber C5b_EKEJiiyvLHpZij08zg C:\\data\\fabrikant");
                Console.WriteLine("  FabrikantGrabber https://www.fabrikant.ru/v2/trades/procedure/view/C5b_EKEJiiyvLHpZij08zg C:\\data\\fabrikant");
                return;
            }

            var idOrUrl = args[0];
            var outputFolder = args[1];

            Directory.CreateDirectory(outputFolder);

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            using var httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(60);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FabrikantGrabber/1.0");

            var scraper = new FabrikantScraper(httpClient);

            try
            {
                var cts = new CancellationTokenSource();

                Console.WriteLine("[*] Начинаю обработку: " + idOrUrl);

                var result = await scraper.DownloadProcedureAndDocsAsync(
                    idOrUrl,
                    outputFolder,
                    cts.Token);

                Console.WriteLine();
                Console.WriteLine("[OK] Готово.");
                Console.WriteLine(" JSON: " + result.JsonPath);
                Console.WriteLine(" Документы: " + result.DocumentsFolder);
                Console.WriteLine(" Файлов: " + result.DownloadedFiles.Count);

                foreach (var f in result.DownloadedFiles)
                {
                    Console.WriteLine("   - " + f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    #region Public result DTO

    public sealed class DownloadResult
    {
        public string JsonPath { get; init; } = default!;
        public string DocumentsFolder { get; init; } = default!;
        public List<string> DownloadedFiles { get; init; } = new();
    }

    #endregion

    #region Scraper

    public sealed class FabrikantScraper
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // кириллица без \uXXXX
        };

        private const string BaseUrl = "https://www.fabrikant.ru";
        private const string ViewPath = "/v2/trades/procedure/view/";
        private const string DocsPath = "/v2/trades/procedure/documentation/";

        public FabrikantScraper(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<DownloadResult> DownloadProcedureAndDocsAsync(
            string idOrUrl,
            string outputFolder,
            CancellationToken cancellationToken = default)
        {
            var procedureId = ExtractId(idOrUrl);
            var viewUrl = BaseUrl + ViewPath + procedureId;
            var docsUrl = BaseUrl + DocsPath + procedureId;

            Console.WriteLine("[*] ProcedureId: " + procedureId);
            Console.WriteLine("[*] View URL: " + viewUrl);
            Console.WriteLine("[*] Docs URL: " + docsUrl);

            // 1. HTML карточки
            var htmlView = await _httpClient.GetStringAsync(viewUrl, cancellationToken);

            var procedure = ParseProcedurePage(htmlView, viewUrl);

            // 2. HTML вкладки документации + ссылки
            var docsHtml = await _httpClient.GetStringAsync(docsUrl, cancellationToken);
            var baseUri = new Uri(docsUrl);
            var docLinks = ExtractDocumentationLinks(docsHtml, baseUri);

            Console.WriteLine("[*] Найдено документов: " + docLinks.Count);

            // Сохраняем ссылки на документы в JSON
            procedure.Documents = docLinks;

            // 3. JSON (UTF-8, без BOM)
            var jsonFileName = SanitizeFileName(procedureId) + ".json";
            var jsonPath = Path.Combine(outputFolder, jsonFileName);
            var json = JsonSerializer.Serialize(procedure, JsonOptions);

            await using (var fs = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                await writer.WriteAsync(json);
            }

            Console.WriteLine("[*] JSON сохранён: " + jsonPath);

            // 4. Скачиваем документы
            var docsFolder = Path.Combine(outputFolder, SanitizeFileName(procedureId) + "_docs");
            Directory.CreateDirectory(docsFolder);

            var downloaded = new List<string>();

            foreach (var link in docLinks)
            {
                try
                {
                    var localName = string.IsNullOrWhiteSpace(link.FileName)
                        ? Path.GetFileName(link.Url.LocalPath)
                        : link.FileName;

                    if (string.IsNullOrWhiteSpace(localName))
                        localName = Guid.NewGuid().ToString("N");

                    var localPath = Path.Combine(docsFolder, SanitizeFileName(localName));
                    Console.WriteLine("[*] Скачиваю: " + link.Url + " -> " + localPath);

                    using var resp = await _httpClient.GetAsync(link.Url, cancellationToken);
                    resp.EnsureSuccessStatusCode();

                    await using var fs = File.Create(localPath);
                    await resp.Content.CopyToAsync(fs, cancellationToken);

                    downloaded.Add(localPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WARN] Не удалось скачать '" + link.Url + "': " + ex.Message);
                }
            }

            return new DownloadResult
            {
                JsonPath = jsonPath,
                DocumentsFolder = docsFolder,
                DownloadedFiles = downloaded
            };
        }

        private static string ExtractId(string idOrUrl)
        {
            if (idOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(idOrUrl);
                var last = uri.Segments.LastOrDefault();
                return last?.Trim('/') ?? idOrUrl;
            }
            return idOrUrl.Trim();
        }

        #endregion

        #region Parsing procedure page

        public FabrikantProcedure ParseProcedurePage(string html, string viewUrl)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new FabrikantProcedure
            {
                ExternalId = ExtractId(viewUrl),
                RawHtml = html
            };

            var fullText = doc.DocumentNode.InnerText ?? string.Empty;

            // Номер процедуры
            result.ProcedureNumber =
                ExtractBetween(fullText, "Процедурная часть (№", ")") ??
                ExtractBetween(fullText, "№", "\n") ??
                string.Empty;

            // Секция (223-ФЗ / 44-ФЗ / коммерция)
            result.LawSection = GetValueAfterLabel(doc, "Секция на торговой площадке") ?? string.Empty;

            // Общее наименование закупки
            result.Title = GetValueAfterLabel(doc, "Общее наименование закупки") ??
                           GetValueAfterLabel(doc, "Предмет закупки") ??
                           string.Empty;

            // Тип и статус процедуры
            result.ProcedureType = GetValueAfterLabel(doc, "Тип процедуры") ??
                                    GetValueAfterLabel(doc, "Способ закупки") ??
                                    GetValueAfterLabel(doc, "Способ проведения закупки") ??
                                    string.Empty;

            result.Status = GetValueAfterLabel(doc, "Статус процедуры") ??
                            GetValueAfterLabel(doc, "Статус") ??
                            string.Empty;

            // Организатор / заказчик
            result.OrganizerName = GetValueAfterLabel(doc, "Информация об организаторе") ??
                                   GetValueAfterLabel(doc, "Организатор") ??
                                   string.Empty;

            var inns = GetAllValuesAfterLabel(doc, "ИНН");
            var kpps = GetAllValuesAfterLabel(doc, "КПП");

            if (inns.Count > 0) result.OrganizerInn = inns[0];
            if (kpps.Count > 0) result.OrganizerKpp = kpps[0];
            if (inns.Count > 1) result.CustomerInn = inns[1];
            if (kpps.Count > 1) result.CustomerKpp = kpps[1];

            result.CustomerName = GetValueAfterLabel(doc, "Организация") ??
                                  GetValueAfterLabel(doc, "Заказчик") ??
                                  string.Empty;
            result.CustomerFullName = GetValueAfterLabel(doc, "Полное наименование") ?? string.Empty;

            // Даты
            result.PublishDate = ParseRuDate(GetValueAfterLabel(doc, "Дата публикации"));
            result.ApplyStartDate = ParseRuDate(GetValueAfterLabel(doc, "Дата и время начала приема заявок"));
            result.ApplyEndDate = ParseRuDate(GetValueAfterLabel(doc, "Дата и время окончания приема заявок"));
            result.ResultDate = ParseRuDate(GetValueAfterLabel(doc, "Дата и время подведения итогов"));

            // НМЦК
            var nmckText = GetValueAfterLabel(doc, "Начальная (максимальная) цена") ??
                           GetValueAfterLabel(doc, "Цена с НДС") ??
                           string.Empty;
            result.Nmck = ParseMoney(nmckText);

            var currency = GetValueAfterLabel(doc, "Валюта") ?? "Рубль";
            result.Currency = currency.Contains("руб", StringComparison.OrdinalIgnoreCase) ? "RUB" : currency;

            // Позиция
            result.ItemName = GetValueAfterLabel(doc, "Наименование позиции") ??
                              GetValueAfterLabel(doc, "Предмет договора") ??
                              string.Empty;

            result.Okpd2 = GetValueAfterLabel(doc, "ОКПД2") ?? string.Empty;
            result.Okved2 = GetValueAfterLabel(doc, "ОКВЭД2") ?? string.Empty;

            // Адрес
            result.DeliveryAddress = GetValueAfterLabel(doc, "Место поставки") ??
                                     GetValueAfterLabel(doc, "Адрес поставки") ??
                                     string.Empty;

            result.DeliveryTerm = GetValueAfterLabel(doc, "Срок поставки") ??
                                   GetValueAfterLabel(doc, "Сроки поставки") ??
                                   string.Empty;

            result.PaymentTerms = GetValueAfterLabel(doc, "Условия оплаты") ??
                                   GetValueAfterLabel(doc, "Порядок оплаты") ??
                                   string.Empty;

            result.ApplicationSecurity = ParseMoney(GetValueAfterLabel(doc, "Размер обеспечения заявки"));
            result.ContractSecurity = ParseMoney(
                GetValueAfterLabel(doc, "Размер обеспечения договора") ??
                GetValueAfterLabel(doc, "Размер обеспечения исполнения договора") ??
                GetValueAfterLabel(doc, "Размер обеспечения исполнения контракта"));

            // Количество + единица (очень грубо)
            var qtyNodeValue = GetValueAfterLabel(doc, "Количество по ОКЕИ") ??
                               GetValueAfterLabel(doc, "Количество");
            if (!string.IsNullOrWhiteSpace(qtyNodeValue))
            {
                var parts = qtyNodeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 &&
                    decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var qty))
                {
                    result.Quantity = qty;
                    if (parts.Length >= 2)
                        result.Unit = parts[1];
                }
            }

            // Лоты из таблицы
            result.Lots = ExtractLots(doc);

            return result;
        }

        private static string? GetValueAfterLabel(HtmlDocument doc, string labelText)
            => GetValueAfterLabel(doc.DocumentNode, labelText);

        private static string? GetValueAfterLabel(HtmlNode root, string labelText)
        {
            var nodes = root
                .SelectNodes(".//*[contains(normalize-space(text()), '" + labelText + "')]");
            if (nodes == null || nodes.Count == 0)
                return null;

            var node = nodes[0];

            HtmlNode? current = node;
            for (int i = 0; i < 12 && current != null; i++)
            {
                current = current.NextSibling ?? current.ParentNode?.NextSibling;
                if (current == null) break;

                var text = current.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) &&
                    !text.Contains(labelText, StringComparison.OrdinalIgnoreCase))
                {
                    return HtmlEntity.DeEntitize(text);
                }
            }

            return null;
        }

        private static List<string> GetAllValuesAfterLabel(HtmlDocument doc, string labelText)
        {
            var result = new List<string>();

            var nodes = doc.DocumentNode
                .SelectNodes("//*[contains(normalize-space(text()), '" + labelText + "')]");
            if (nodes == null || nodes.Count == 0)
                return result;

            foreach (var node in nodes)
            {
                HtmlNode? current = node;
                for (int i = 0; i < 8 && current != null; i++)
                {
                    current = current.NextSibling ?? current.ParentNode?.NextSibling;
                    if (current == null) break;

                    var text = current.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) &&
                        !text.Contains(labelText, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(HtmlEntity.DeEntitize(text));
                        break;
                    }
                }
            }

            return result;
        }

        private static DateTime? ParseRuDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            text = text.Trim();

            var formats = new[]
            {
                "dd.MM.yyyy HH:mm",
                "dd.MM.yyyy H:mm",
                "dd.MM.yyyy"
            };

            if (DateTime.TryParseExact(text, formats,
                    new CultureInfo("ru-RU"), DateTimeStyles.AssumeLocal, out var dt))
                return dt;

            if (DateTime.TryParse(text, new CultureInfo("ru-RU"),
                    DateTimeStyles.AssumeLocal, out dt))
                return dt;

            return null;
        }

        private static decimal? ParseMoney(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var digits = new string(text.Where(c => char.IsDigit(c) || c is ',' or '.').ToArray());
            if (string.IsNullOrWhiteSpace(digits)) return null;

            digits = digits.Replace(" ", "").Replace("\u00A0", "");

            if (decimal.TryParse(digits, NumberStyles.Any, new CultureInfo("ru-RU"), out var value))
                return value;

            return null;
        }

        private static string? ExtractBetween(string text, string start, string end)
        {
            var i1 = text.IndexOf(start, StringComparison.Ordinal);
            if (i1 < 0) return null;
            i1 += start.Length;
            var i2 = text.IndexOf(end, i1, StringComparison.Ordinal);
            if (i2 < 0) return null;
            return text.Substring(i1, i2 - i1).Trim();
        }

        #endregion

        #region Documentation links parsing

        private static List<DocumentationLink> ExtractDocumentationLinks(string docsHtml, Uri baseUri)
        {
            var result = new List<DocumentationLink>();

            var doc = new HtmlDocument();
            doc.LoadHtml(docsHtml);

            // Ищем таблицу, где есть заголовок "Файл"
            var table = doc.DocumentNode.SelectSingleNode("//table[.//th[contains(., 'Файл')]]");
            if (table == null)
                return result;

            // Берём все строки с ячейками <td> (пропускаем заголовок с <th>)
            var rows = table.SelectNodes(".//tr[td]");
            if (rows == null || rows.Count == 0)
                return result;

            var regexFile = new Regex(
                @"Файл\s+(.+?)\s+загружен",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

            foreach (var row in rows)
            {
                // 1) первая колонка — текст "Файл ... загружен ...", оттуда достаём имя
                var fileCell = row.SelectSingleNode(".//td[1]");
                if (fileCell == null) continue;

                var cellText = HtmlEntity.DeEntitize(fileCell.InnerText ?? string.Empty).Trim();

                string? fileName = null;

                var m = regexFile.Match(cellText);
                if (m.Success)
                {
                    fileName = m.Groups[1].Value;
                }
                else if (cellText.Contains("Файл "))
                {
                    // запасной вариант: всё после "Файл " до конца строки
                    var idx = cellText.IndexOf("Файл ", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        fileName = cellText[(idx + "Файл ".Length)..];
                    }
                }

                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                fileName = fileName.Trim('«', '»', '"', '\'', ' ', '\u00A0', '.');

                // 2) в этой же строке ищем ссылку "Скачать" с href download/single
                var linkNode = row.SelectSingleNode(".//a[@href[contains(.,'/documentation/download/single/')]]")
                              ?? row.SelectSingleNode(".//a[contains(., 'Скачать') and @href]");

                if (linkNode == null) continue;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) continue;

                if (!Uri.TryCreate(href, UriKind.Absolute, out var fileUri))
                    fileUri = new Uri(baseUri, href);

                result.Add(new DocumentationLink
                {
                    Url = fileUri,
                    FileName = fileName
                });
            }

            return result;
        }


        #endregion

        #region Lots parsing

        private static List<FabrikantLot> ExtractLots(HtmlDocument doc)
        {
            var result = ExtractLotsFromPanels(doc);
            if (result.Count > 0)
                return result;

            return ExtractLotsFromTables(doc);
        }

        private static List<FabrikantLot> ExtractLotsFromPanels(HtmlDocument doc)
        {
            var result = new List<FabrikantLot>();

            var headings = doc.DocumentNode
                .SelectNodes("//div[contains(@class,'panel-heading')][contains(normalize-space(.), 'Лот №')]");
            if (headings == null || headings.Count == 0)
                return result;

            foreach (var heading in headings)
            {
                var panel = heading.ParentNode;
                if (panel == null)
                    continue;

                var lot = new FabrikantLot();

                var headingText = CleanText(heading.InnerText);
                var numberMatch = Regex.Match(headingText, @"Лот\s*№\s*([\d]+)");
                if (numberMatch.Success)
                    lot.Number = numberMatch.Groups[1].Value;

                var nameFromHeading = headingText;
                var dotIndex = headingText.IndexOf('.');
                if (dotIndex >= 0 && dotIndex + 1 < headingText.Length)
                    nameFromHeading = headingText[(dotIndex + 1)..];

                var dashIndex = nameFromHeading.IndexOf('-');
                if (dashIndex > 0)
                    nameFromHeading = nameFromHeading[..dashIndex];

                lot.Name = CleanText(GetValueAfterLabel(panel, "Предмет договора") ?? nameFromHeading);

                lot.StartPrice = ParseMoney(
                    GetValueAfterLabel(panel, "Начальная (максимальная) цена") ??
                    GetValueAfterLabel(panel, "Цена") ??
                    GetValueAfterLabel(panel, "Цена позиции"));

                var currency = GetValueAfterLabel(panel, "Валюта");
                if (!string.IsNullOrWhiteSpace(currency))
                    lot.Currency = currency.Contains("руб", StringComparison.OrdinalIgnoreCase) ? "RUB" : currency;

                var qtyText = GetValueAfterLabel(panel, "Количество по ОКЕИ") ?? GetValueAfterLabel(panel, "Количество");
                if (!string.IsNullOrWhiteSpace(qtyText))
                    ParseLotQuantity(qtyText, lot);

                lot.DeliveryAddress = GetValueAfterLabel(panel, "Место поставки") ??
                                       GetValueAfterLabel(panel, "Адрес") ?? string.Empty;

                lot.DeliveryTerm = GetValueAfterLabel(panel, "Срок поставки") ?? string.Empty;
                lot.PaymentTerms = GetValueAfterLabel(panel, "Условия оплаты") ?? string.Empty;

                EnrichLotFromPositionsTable(panel, lot);
                if (lot.HasData)
                    result.Add(lot);
            }

            return result;
        }

        private static void EnrichLotFromPositionsTable(HtmlNode panel, FabrikantLot lot)
        {
            var positionsTable = panel.SelectSingleNode(
                ".//table[contains(@class, 'kim-positions') or contains(@class, 'table')][.//th[contains(., 'Количество по ОКЕИ')]]");
            if (positionsTable == null)
                return;

            var headers = positionsTable
                .SelectNodes(".//thead//th")
                ?.Select(h => NormalizeText(h.InnerText))
                .ToList();

            var firstRowCells = positionsTable.SelectSingleNode(".//tbody/tr")?.SelectNodes("./td");
            if (headers == null || headers.Count == 0 || firstRowCells == null || firstRowCells.Count == 0)
                return;

            for (int i = 0; i < Math.Min(headers.Count, firstRowCells.Count); i++)
            {
                var header = headers[i].ToLowerInvariant();
                var value = CleanText(firstRowCells[i].InnerText);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (header.Contains("наимен"))
                {
                    lot.Name = value;
                }
                else if (header.Contains("колич"))
                {
                    ParseLotQuantity(value, lot);
                }
                else if (header.Contains("валют"))
                {
                    lot.Currency = value.Contains("руб", StringComparison.OrdinalIgnoreCase) ? "RUB" : value;
                }
                else if (header.Contains("место") || header.Contains("адрес"))
                {
                    lot.DeliveryAddress = value;
                }
                else if (header.Contains("цена"))
                {
                    lot.StartPrice = ParseMoney(value);
                    if (!lot.StartPrice.HasValue)
                        lot.AdditionalFields["Цена позиции"] = value;
                }
                else if (header.Contains("единиц") || header.Contains("ед.") || header.Contains("оке"))
                {
                    if (string.IsNullOrWhiteSpace(lot.Unit))
                        lot.Unit = value;
                }
                else
                {
                    lot.AdditionalFields[headers[i]] = value;
                }
            }
        }
        private static List<FabrikantLot> ExtractLotsFromTables(HtmlDocument doc)
        {
            var result = new List<FabrikantLot>();

            var lotTables = doc.DocumentNode.SelectNodes("//table[.//th[contains(translate(normalize-space(.), 'Л', 'л'), 'лот')]]")
                            ?? doc.DocumentNode.SelectNodes("//table[.//th[contains(., 'Наименование лота')]]");

            if (lotTables == null)
                return result;

            foreach (var table in lotTables)
            {
                var headerCells = table.SelectNodes(".//tr[th][1]/th");
                if (headerCells == null || headerCells.Count == 0)
                    continue;

                var headers = headerCells.Select(h => NormalizeText(h.InnerText)).ToList();

                var rows = table.SelectNodes(".//tr[td]");
                if (rows == null || rows.Count == 0)
                    continue;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("./td");
                    if (cells == null || cells.Count == 0)
                        continue;

                    var lot = new FabrikantLot();

                    for (int i = 0; i < Math.Min(headers.Count, cells.Count); i++)
                    {
                        var header = headers[i];
                        var value = CleanText(cells[i].InnerText);

                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        var headerLower = header.ToLowerInvariant();

                        if (headerLower.Contains("номер") || headerLower.Contains("№"))
                        {
                            lot.Number = value;
                        }
                        else if (headerLower.Contains("наимен"))
                        {
                            lot.Name = value;
                        }
                        else if (headerLower.Contains("началь") || headerLower.Contains("цена"))
                        {
                            lot.StartPrice ??= ParseMoney(value);
                        }
                        else if (headerLower.Contains("валют"))
                        {
                            lot.Currency = value.Contains("руб", StringComparison.OrdinalIgnoreCase) ? "RUB" : value;
                        }
                        else if (headerLower.Contains("колич"))
                        {
                            ParseLotQuantity(value, lot);
                        }
                        else if (headerLower.Contains("единиц") || headerLower.Contains("ед.") || headerLower.Contains("оке"))
                        {
                            if (string.IsNullOrWhiteSpace(lot.Unit))
                                lot.Unit = value;
                        }
                        else if (headerLower.Contains("статус"))
                        {
                            lot.Status = value;
                        }
                        else if (headerLower.Contains("место") || headerLower.Contains("адрес"))
                        {
                            lot.DeliveryAddress = value;
                        }
                        else if (headerLower.Contains("срок"))
                        {
                            lot.DeliveryTerm = value;
                        }
                        else if (headerLower.Contains("оплат"))
                        {
                            lot.PaymentTerms = value;
                        }
                        else
                        {
                            lot.AdditionalFields[header] = value;
                        }
                    }

                    lot.RawRow = string.Join(" | ", cells
                        .Select(c => CleanText(c.InnerText))
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                    if (lot.HasData)
                        result.Add(lot);
                }
            }

            return result;
        }

        private static void ParseLotQuantity(string value, FabrikantLot lot)
        {
            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 1 && decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var qty))
            {
                lot.Quantity = qty;
                if (parts.Length >= 2 && string.IsNullOrWhiteSpace(lot.Unit))
                    lot.Unit = parts[1];
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                lot.AdditionalFields["Количество"] = value;
            }
        }

        private static string CleanText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return HtmlEntity.DeEntitize(text).Trim();
        }

        private static string NormalizeText(string? text)
        {
            var cleaned = CleanText(text);
            return Regex.Replace(cleaned, "\\s+", " ").Trim();
        }

        #endregion

        #region Helpers

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        #endregion
    }

    #region Data DTOs

    public sealed class FabrikantProcedure
    {
        public string ExternalId { get; set; } = default!;
        public string ProcedureNumber { get; set; } = default!;
        public string LawSection { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string ProcedureType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public string OrganizerName { get; set; } = string.Empty;
        public string OrganizerInn { get; set; } = string.Empty;
        public string OrganizerKpp { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerInn { get; set; } = string.Empty;
        public string CustomerKpp { get; set; } = string.Empty;

        public DateTime? PublishDate { get; set; }
        public DateTime? ApplyStartDate { get; set; }
        public DateTime? ApplyEndDate { get; set; }
        public DateTime? ResultDate { get; set; }

        public decimal? Nmck { get; set; }
        public string Currency { get; set; } = "RUB";

        public string Okpd2 { get; set; } = string.Empty;
        public string Okved2 { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryTerm { get; set; } = string.Empty;
        public string PaymentTerms { get; set; } = string.Empty;
        public decimal? ApplicationSecurity { get; set; }
        public decimal? ContractSecurity { get; set; }

        public List<FabrikantLot> Lots { get; set; } = new();
        public List<DocumentationLink> Documents { get; set; } = new();

        public string RawHtml { get; set; } = string.Empty;
    }

    public sealed class DocumentationLink
    {
        public Uri Url { get; set; } = default!;
        public string FileName { get; set; } = string.Empty;
    }

    public sealed class FabrikantLot
    {
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? StartPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryTerm { get; set; } = string.Empty;
        public string PaymentTerms { get; set; } = string.Empty;

        public Dictionary<string, string> AdditionalFields { get; set; } = new();

        [JsonIgnore]
        public bool HasData =>
            !string.IsNullOrWhiteSpace(Number) ||
            !string.IsNullOrWhiteSpace(Name) ||
            StartPrice.HasValue ||
            Quantity.HasValue ||
            !string.IsNullOrWhiteSpace(Status) ||
            AdditionalFields.Count > 0;

        public string RawRow { get; set; } = string.Empty;
    }

    #endregion
}
