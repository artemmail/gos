using System;
using System.Collections.Generic;
using System.Linq;
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
            var cells = row.SelectNodes(".//td");
            if (cells == null || cells.Count == 0) continue;

            var fileCell = cells[0];

            string? extension = null;
            if (cells.Count > 1)
            {
                var extText = HtmlEntity.DeEntitize(cells[1].InnerText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(extText))
                {
                    var extMatch = Regex.Match(extText, @"\.([\p{L}\p{N}_-]+)");
                    if (extMatch.Success)
                    {
                        extension = extMatch.Value;
                    }
                }
            }

            var cellText = HtmlEntity.DeEntitize(fileCell.InnerText ?? string.Empty).Trim();
            var rowText = string.Join(" ", cells
                .Select(c => HtmlEntity.DeEntitize(c.InnerText ?? string.Empty).Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
            );

            string? fileName = null;

            if (!string.IsNullOrWhiteSpace(rowText))
            {
                var m = regexFile.Match(rowText);
                if (m.Success)
                {
                    fileName = m.Groups[1].Value;

                    var extMatch = Regex.Match(fileName, @"\.([\p{L}\p{N}_-]+)$");
                    if (extMatch.Success)
                    {
                        extension = extMatch.Value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(cellText))
            {
                fileName = cellText;
            }

            var linkNode = row.SelectSingleNode(".//a[@href[contains(.,'/documentation/download/single/')]]")
                          ?? row.SelectSingleNode(".//a[contains(., 'Скачать') and @href]");

            if (linkNode == null) continue;

            var href = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href)) continue;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var dataFileName = HtmlEntity.DeEntitize(linkNode.GetAttributeValue("data-file-name", string.Empty)).Trim();
                if (!string.IsNullOrWhiteSpace(dataFileName))
                {
                    fileName = dataFileName;
                }
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var linkText = HtmlEntity.DeEntitize(linkNode.InnerText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(linkText))
                {
                    fileName = linkText;
                }
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var hrefPath = href.Split('?')[0].TrimEnd('/');
                var lastSlashIndex = hrefPath.LastIndexOf('/');
                if (lastSlashIndex >= 0 && lastSlashIndex < hrefPath.Length - 1)
                {
                    fileName = hrefPath[(lastSlashIndex + 1)..];
                }
                else
                {
                    fileName = hrefPath;
                }

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = Uri.UnescapeDataString(fileName);
                }
            }

            fileName = fileName.Trim('«', '»', '"', '\'', ' ', '\u00A0', '.');

            if (!string.IsNullOrWhiteSpace(extension))
            {
                if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = $"{fileName}{extension}";
                }
            }

            if (string.IsNullOrWhiteSpace(fileName))
                continue;

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
