using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FabrikantGrabber.Models;
using HtmlAgilityPack;

namespace FabrikantGrabber.Parsers;

public sealed class DocumentationParser
{
    public List<DocumentationLink> ParseDocumentationLinks(string docsHtml, Uri baseUri)
    {
        var result = new List<DocumentationLink>();

        var doc = new HtmlDocument();
        doc.LoadHtml(docsHtml);

        var table = doc.DocumentNode.SelectSingleNode("//table[.//th[contains(., 'Файл')]]");
        if (table == null)
            return result;

        var rows = table.SelectNodes(".//tr[td]");
        if (rows == null || rows.Count == 0)
            return result;

        var regexFile = new Regex(
            @"Файл\s+(.+?)\s+загружен",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        foreach (var row in rows)
        {
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
                var idx = cellText.IndexOf("Файл ", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    fileName = cellText[(idx + "Файл ".Length)..];
                }
            }

            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            fileName = fileName.Trim('«', '»', '"', '\'', ' ', '\u00A0', '.');

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
}
