using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FabrikantGrabber.Models;
using HtmlAgilityPack;

namespace FabrikantGrabber.Parsers;

public sealed class SearchPageParser
{
    private static readonly Regex ProcedureIdRegex = new("/v2/trades/procedure/view/([\\w\\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TotalFromJsonRegex = new("\"total(?:Count)?\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TotalFromTextRegex = new("(?i)(найдено|всего|показано)[^0-9]{0,30}(\\d{1,7})", RegexOptions.Compiled);

    public FabrikantSearchResult Parse(string html, Uri? baseUri = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML is empty", nameof(html));

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var result = new FabrikantSearchResult
        {
            RawHtml = html,
            TotalCount = ExtractTotalCount(doc, html)
        };

        result.Procedures = ExtractProcedures(doc, baseUri);

        return result;
    }

    private static int ExtractTotalCount(HtmlDocument doc, string html)
    {
        var attrTotal = ExtractTotalFromAttributes(doc);
        if (attrTotal.HasValue)
            return attrTotal.Value;

        var jsonTotal = ExtractTotalFromJson(html);
        if (jsonTotal.HasValue)
            return jsonTotal.Value;

        var textTotal = ExtractTotalFromText(doc);
        if (textTotal.HasValue)
            return textTotal.Value;

        return 0;
    }

    private static int? ExtractTotalFromAttributes(HtmlDocument doc)
    {
        var candidates = new[]
        {
            "data-total", "data-total-count", "data-totalcount", "data-count", "data-items-count", "data-results"
        };

        foreach (var attr in candidates)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//*[@{attr}]");
            if (nodes == null) continue;

            foreach (var node in nodes)
            {
                var value = node.GetAttributeValue(attr, string.Empty);
                if (int.TryParse(value, out var total) && total > 0)
                    return total;
            }
        }

        return null;
    }

    private static int? ExtractTotalFromJson(string html)
    {
        var match = TotalFromJsonRegex.Match(html);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var total))
            return total;

        return null;
    }

    private static int? ExtractTotalFromText(HtmlDocument doc)
    {
        var text = doc.DocumentNode.InnerText;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var matches = TotalFromTextRegex.Matches(text);
        var best = matches
            .Select(m => int.TryParse(m.Groups[2].Value, out var total) ? total : 0)
            .DefaultIfEmpty(0)
            .Max();

        return best > 0 ? best : null;
    }

    private static List<FabrikantSearchItem> ExtractProcedures(HtmlDocument doc, Uri? baseUri)
    {
        var cardNodes = doc.DocumentNode.SelectNodes("//div[@data-slot='card']");
        var anchorNodes = new List<HtmlNode>();

        if (cardNodes != null)
        {
            foreach (var card in cardNodes)
            {
                var link = card.SelectSingleNode(".//a[contains(@href, '/v2/trades/procedure/view/')]");
                if (link != null)
                    anchorNodes.Add(link);
            }
        }
        else
        {
            var fallbackNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, '/v2/trades/procedure/view/')]");
            if (fallbackNodes != null)
                anchorNodes.AddRange(fallbackNodes);
        }

        if (anchorNodes.Count == 0)
            return new List<FabrikantSearchItem>();

        var result = new List<FabrikantSearchItem>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in anchorNodes)
        {
            var href = a.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href))
                continue;

            var match = ProcedureIdRegex.Match(href);
            if (!match.Success)
                continue;

            var id = match.Groups[1].Value;
            if (seenIds.Contains(id))
                continue;

            var title = HtmlEntity.DeEntitize(a.InnerText?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(title))
                title = HtmlEntity.DeEntitize(a.GetAttributeValue("title", string.Empty).Trim());

            Uri? url = null;
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
            {
                url = abs;
            }
            else if (baseUri != null && Uri.TryCreate(baseUri, href, out var rel))
            {
                url = rel;
            }

            result.Add(new FabrikantSearchItem
            {
                ProcedureId = id,
                Title = title,
                Url = url
            });

            seenIds.Add(id);
        }

        return result;
    }
}
