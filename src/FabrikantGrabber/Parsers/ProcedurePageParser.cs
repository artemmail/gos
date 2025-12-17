using FabrikantGrabber.Models;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FabrikantGrabber.Parsers;


public static class PriceExtractor
{
    private static readonly Regex PriceRegex =
        new Regex(@"\d{1,3}(?:[\s\u00A0.,]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?",
            RegexOptions.Compiled);

    public static bool TryExtractPrice(HtmlNode root, out decimal price)
    {
        price = 0m;
        if (root == null) return false;

        // 1) Клонируем и вырезаем <script>...</script> (и <style>...</style>)
        var cleaned = root.CloneNode(true);
        RemoveScriptsAndStylesInPlace(cleaned);

        // 2) Далее анализируем уже очищенный DOM
        HtmlNode? context = cleaned
            .DescendantsAndSelf()
            .FirstOrDefault(n =>
                (n.InnerText?.IndexOf("цена", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);

        if (context?.ParentNode != null && TryFindInNode(context.ParentNode, out price))
            return true;

        return TryFindInNode(cleaned, out price);
    }

    private static void RemoveScriptsAndStylesInPlace(HtmlNode node)
    {
        // SelectNodes может вернуть null
        var junk = node.SelectNodes(".//script|.//style");
        if (junk == null) return;

        foreach (var n in junk.ToList())
            n.Remove(); // удаляет узел целиком вместе с содержимым
    }

    private static bool TryFindInNode(HtmlNode node, out decimal price)
    {
        price = 0m;

        foreach (var n in node.DescendantsAndSelf())
        {
            // после удаления script/style можно не фильтровать по Name,
            // но оставим на всякий случай
            var name = n.Name?.ToLowerInvariant();
            if (name is "script" or "style" or "#comment") continue;

            var text = HtmlEntity.DeEntitize(n.InnerText ?? string.Empty)
                .Replace('\u00A0', ' ');

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var m = PriceRegex.Match(text);
            if (!m.Success)
                continue;

            if (TryParseFlexibleDecimal(m.Value, out price))
                return true;
        }

        return false;
    }

    private static bool TryParseFlexibleDecimal(string raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Replace(" ", "").Replace("\u00A0", "");

        int lastComma = s.LastIndexOf(',');
        int lastDot = s.LastIndexOf('.');
        int decPos = Math.Max(lastComma, lastDot);

        string normalized;
        if (decPos >= 0)
        {
            var intPart = s.Substring(0, decPos).Replace(",", "").Replace(".", "");
            var fracPart = s.Substring(decPos + 1);

            if (fracPart.Length == 3 && fracPart.All(char.IsDigit))
                normalized = intPart + fracPart;     // это были тысячи
            else
                normalized = intPart + "." + fracPart; // это дробь
        }
        else
        {
            normalized = s.Replace(",", "").Replace(".", "");
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
public sealed class ProcedurePageParser
{
    public HtmlNode? ExtractColMd8Content(HtmlDocument doc, string panelGroupClass)
    {
        
        return ExtractColMd8Content(doc.DocumentNode, panelGroupClass);
    }

    private static HtmlNode? ExtractColMd8Content(HtmlNode root, string panelGroupClass)
    {
        var panel = root.SelectSingleNode($"//div[contains(@class, '{panelGroupClass}')]");
        var colMd8 = panel?.SelectSingleNode(".//div[contains(@class, 'col-md-8')]");

        return colMd8;
    }
    public FabrikantProcedure Parse(string html, string procedureId)
    {
        if(procedureId== "Y7IIaUyrdCUJvlot8E4ltw")
        {
            int a = 0;

        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var result = new FabrikantProcedure
        {
            ExternalId = procedureId,
            RawHtml = html
        };

        var fullText = doc.DocumentNode.InnerText ?? string.Empty;

        result.ProcedureNumber =
            ExtractBetween(fullText, "Процедурная часть (№", ")") ??
            ExtractBetween(fullText, "№", "\n") ??
            string.Empty;

        result.LawSection = GetValueAfterLabel(doc, "Секция на торговой площадке") ?? string.Empty;

        result.Title = GetValueAfterLabel(doc, "Общее наименование закупки") ??
                       GetValueAfterLabel(doc, "Предмет закупки") ??
                       string.Empty;

        var nn1 = ExtractColMd8Content(doc, "panel-group panel-group-element-lot_price");

        if (PriceExtractor.TryExtractPrice(nn1, out var price))
        {
            result.Nmck = price;
        }



        var nn = ExtractColMd8Content(doc, "panel-group panel-group-element-procedure_organizer");



        if (nn != null)
        {
            try
            {
                result.OrganizerName = HtmlEntity.DeEntitize(nn?.ChildNodes[1].InnerHtml).Replace("\t", "").Replace("\n", "");
            }
            catch { };
            result.OrganizerInn = GetValueAfterLabel(nn, "ИНН");
            result.OrganizerAddress = GetValueAfterLabel(nn, "Почтовый адрес");
            result.OrganizerKpp = GetValueAfterLabel(nn, "КПП");
        }

        result.ProcedureType = GetValueAfterLabel(doc, "Тип процедуры") ??
                                GetValueAfterLabel(doc, "Способ закупки") ??
                                GetValueAfterLabel(doc, "Способ проведения закупки") ??
                                string.Empty;

        result.Status = GetValueAfterLabel(doc, "Статус процедуры") ??
                        GetValueAfterLabel(doc, "Статус") ??
                        string.Empty;

        /*
        result.OrganizerName = GetValueAfterLabel(doc, "Информация об организаторе") ??
                               GetValueAfterLabel(doc, "Организатор") ??
                               string.Empty;
        */
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

        result.PublishDate = ParseRuDate(GetValueAfterLabel(doc, "Дата публикации"));
        result.ApplyStartDate = ParseRuDate(GetValueAfterLabel(doc, "Дата и время начала приема заявок"));
        result.ApplyEndDate = ParseRuDate(GetValueAfterLabel(doc, "Дата и время окончания приема заявок"));
        result.ResultDate = ParseRuDate(GetValueAfterLabel(doc, "Дата и время подведения итогов"));

  

        var currency = GetValueAfterLabel(doc, "Валюта") ?? "Рубль";
        result.Currency = currency.Contains("руб", StringComparison.OrdinalIgnoreCase) ? "RUB" : currency;

        result.ItemName = GetValueAfterLabel(doc, "Наименование позиции") ??
                          GetValueAfterLabel(doc, "Предмет договора") ??
                          string.Empty;

        result.Okpd2 = GetValueAfterLabel(doc, "ОКПД2") ?? string.Empty;
        result.Okved2 = GetValueAfterLabel(doc, "ОКВЭД2") ?? string.Empty;

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

        result.Lots = ExtractLots(doc);

        return result;
    }

    private static string? GetValueAfterLabel(HtmlDocument doc, string labelText) => GetValueAfterLabel(doc.DocumentNode, labelText);

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
}
