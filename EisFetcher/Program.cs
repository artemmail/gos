using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace EisFetcher;

internal static class Program
{
    private const string Url = "https://int44.zakupki.gov.ru/eis-integration/services/getDocsIP";
    private const string NsSoap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly Regex[] KeywordRegexes = AppData.KeywordPatterns
        .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToArray();

    public static async Task Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            PrintHelp();
            return;
        }

        Options options;
        try
        {
            options = ParseArgs(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ARGS] {ex.Message}");
            Console.Error.WriteLine();
            PrintHelp();
            return;
        }

        var regions = options.Regions.Count > 0 ? options.Regions : AppData.RegionsAll;
        var now = DateTime.Now;
        var start = now.AddDays(-options.Days);
        long? maxBytes = options.MaxFileMb > 0 ? (long)options.MaxFileMb * 1024 * 1024 : null;

        using var http = CreateHttpClient();

        // sanity check
        using (var xsdResponse = await http.GetAsync(Url + "?xsd=getDocsIP-ws-api.xsd"))
        {
            Console.WriteLine($"[XSD] HTTP {(int)xsdResponse.StatusCode}");
            xsdResponse.EnsureSuccessStatusCode();
        }

        var outputRoot = Path.Combine("out");
        Directory.CreateDirectory(outputRoot);
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalRows = 0;

        foreach (var region in regions)
        {
            Console.WriteLine($"\n=== Регион {region:00} ===");
            for (var day = start.Date; day <= now.Date; day = day.AddDays(1))
            {
                var dateString = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var stop = await ScanDayAsync(region, dateString, "PRIZ", AppData.DocTypes44);
                if (stop)
                {
                    goto End;
                }

                if (options.Include223)
                {
                    stop = await ScanDayAsync(region, dateString, "RI223", AppData.DocTypes223);
                    if (stop)
                    {
                        goto End;
                    }
                }

                if (options.Limit > 0 && totalRows >= options.Limit)
                {
                    goto End;
                }

                if (options.SleepSeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.SleepSeconds));
                }
            }

            if (options.Limit > 0 && totalRows >= options.Limit)
            {
                break;
            }
        }

    End:
        if (totalRows == 0)
        {
            Console.WriteLine("\nИтог: совпадений не найдено.");
        }
        else
        {
            Console.WriteLine($"\nИтог: обработано закупок: {totalRows}. Смотри папку: {Path.GetFullPath(outputRoot)}");
        }

        async Task<bool> ScanDayAsync(int region, string dateStr, string subsystem, IReadOnlyList<string> docTypes)
        {
            foreach (var docType in docTypes)
            {
                var xml = BuildGetDocsByOrgRegion(options.Token, region, subsystem, docType, dateStr);
                byte[] soapResponse;
                try
                {
                    soapResponse = await SoapPostAsync(http, xml);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{region:00}] {dateStr} {subsystem}:{docType} HTTP/SOAP: {ex.Message}");
                    continue;
                }

                var parse = ParseArchiveUrl(soapResponse);
                if (!parse.Success)
                {
                    if (!string.IsNullOrWhiteSpace(parse.Error) && parse.Error!.Contains("token", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[AUTH] {parse.Error}");
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(parse.Error))
                    {
                        Console.WriteLine($"[{region:00}] {dateStr} {subsystem}:{docType} ERR: {parse.Error}");
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(parse.ArchiveUrl))
                {
                    continue;
                }

                byte[] archiveBytes;
                try
                {
                    archiveBytes = await DownloadArchiveAsync(http, parse.ArchiveUrl!, options.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{region:00}] {dateStr} download: {ex.Message}");
                    continue;
                }

                using var zip = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read, leaveOpen: false);
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    var text = XmlText(bytes);
                    if (!AnyKeyword(text))
                    {
                        continue;
                    }

                    var details = ExtractDetailsAndLinks(bytes);
                    if (string.IsNullOrWhiteSpace(details.PurchaseNumber))
                    {
                        continue;
                    }

                    if (!seenNumbers.Add(details.PurchaseNumber))
                    {
                        continue;
                    }

                    totalRows += 1;
                    Console.WriteLine($"  • [{region:00}] {dateStr} {details.PurchaseNumber} | {details.PlacingName ?? "—"} | {details.MaxPrice ?? "—"} | {details.Name ?? "—"}");

                    var folder = Path.Combine(outputRoot, details.PurchaseNumber);
                    Directory.CreateDirectory(Path.Combine(folder, "files"));

                    var noticeFileName = $"notice_{docType}_{dateStr}_{SanitizeName(Path.GetFileName(entry.FullName))}";
                    await File.WriteAllBytesAsync(Path.Combine(folder, noticeFileName), bytes);

                    var fileRows = new List<ManifestFileRow>();
                    if (options.DownloadAttachments && details.Links.Count > 0)
                    {
                        for (var i = 0; i < details.Links.Count; i++)
                        {
                            var link = details.Links[i];
                            var baseName = !string.IsNullOrWhiteSpace(link.Name) ? link.Name : GuessFilenameFromUrl(link.Url);
                            baseName = SanitizeName(baseName);
                            if (string.IsNullOrWhiteSpace(baseName))
                            {
                                baseName = "file";
                            }

                            var dst = Path.Combine(folder, "files", $"{i + 1:000}__{baseName}");
                            try
                            {
                                var result = await DownloadWithTokenAsync(http, link.Url, options.Token, dst, maxBytes);
                                fileRows.Add(new ManifestFileRow
                                {
                                    Ordinal = (i + 1).ToString(CultureInfo.InvariantCulture),
                                    Source = "notice",
                                    Url = link.Url,
                                    SavedAs = Path.GetFileName(result.FinalPath),
                                    ContentType = result.ContentType ?? string.Empty,
                                    Bytes = result.BytesWritten.ToString(CultureInfo.InvariantCulture)
                                });
                                await Task.Delay(100);
                            }
                            catch (Exception ex)
                            {
                                fileRows.Add(new ManifestFileRow
                                {
                                    Ordinal = (i + 1).ToString(CultureInfo.InvariantCulture),
                                    Source = "notice",
                                    Url = link.Url,
                                    SavedAs = $"ERROR: {ex.Message}",
                                    ContentType = string.Empty,
                                    Bytes = string.Empty
                                });
                            }
                        }
                    }

                    if (options.FetchByPurchase)
                    {
                        var packageRows = await FetchByPurchaseAsync(details.PurchaseNumber, dateStr, folder);
                        if (options.DownloadAttachments)
                        {
                            fileRows.AddRange(packageRows);
                        }
                    }

                    SaveManifestRow(folder, details, fileRows);

                    if (options.Limit > 0 && totalRows >= options.Limit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task<List<ManifestFileRow>> FetchByPurchaseAsync(string purchaseNumber, string dateStr, string folder)
        {
            var rows = new List<ManifestFileRow>();
            var xml = BuildGetDocsByReestrNumber(options.Token, purchaseNumber);
            try
            {
                var response = await SoapPostAsync(http, xml);
                var parse = ParseArchiveUrl(response);
                if (!parse.Success || string.IsNullOrWhiteSpace(parse.ArchiveUrl))
                {
                    return rows;
                }

                var archiveBytes = await DownloadArchiveAsync(http, parse.ArchiveUrl!, options.Token);
                using var zip = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read, leaveOpen: false);
                var k = 0;
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    k += 1;
                    var packageName = Path.Combine(folder, $"package_{dateStr}_{k:000}.xml");
                    await File.WriteAllBytesAsync(packageName, bytes);

                    var details = ExtractDetailsAndLinks(bytes);
                    if (options.DownloadAttachments && details.Links.Count > 0)
                    {
                        for (var j = 0; j < details.Links.Count; j++)
                        {
                            var link = details.Links[j];
                            var baseName = !string.IsNullOrWhiteSpace(link.Name) ? link.Name : GuessFilenameFromUrl(link.Url);
                            baseName = SanitizeName(baseName);
                            if (string.IsNullOrWhiteSpace(baseName))
                            {
                                baseName = "file";
                            }

                            var dst = Path.Combine(folder, "files", $"p{k:000}_{j + 1:000}__{baseName}");
                            try
                            {
                                var result = await DownloadWithTokenAsync(http, link.Url, options.Token, dst, maxBytes);
                                rows.Add(new ManifestFileRow
                                {
                                    Ordinal = $"p{k:000}_{j + 1:000}",
                                    Source = "package",
                                    Url = link.Url,
                                    SavedAs = Path.GetFileName(result.FinalPath),
                                    ContentType = result.ContentType ?? string.Empty,
                                    Bytes = result.BytesWritten.ToString(CultureInfo.InvariantCulture)
                                });
                                await Task.Delay(100);
                            }
                            catch (Exception ex)
                            {
                                rows.Add(new ManifestFileRow
                                {
                                    Ordinal = $"p{k:000}_{j + 1:000}",
                                    Source = "package",
                                    Url = link.Url,
                                    SavedAs = $"ERROR: {ex.Message}",
                                    ContentType = string.Empty,
                                    Bytes = string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore errors fetching package
            }

            return rows;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            UseProxy = false
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    private static async Task<byte[]> SoapPostAsync(HttpClient httpClient, string xml)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "text/xml");
        using var response = await httpClient.PostAsync(Url, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task<byte[]> DownloadArchiveAsync(HttpClient httpClient, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("individualPerson_token", token);
        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task<DownloadResult> DownloadWithTokenAsync(HttpClient httpClient, string url, string token, string destination, long? maxBytesLimit)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("individualPerson_token", token);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var cd = response.Content.Headers.ContentDisposition;
        var ct = response.Content.Headers.ContentType?.ToString();
        var cdName = ParseDispositionFilename(cd?.ToString());
        var finalPath = destination;
        if (!string.IsNullOrWhiteSpace(cdName))
        {
            finalPath = Path.Combine(Path.GetDirectoryName(destination)!, SanitizeName(cdName));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        await using (var file = File.Create(finalPath))
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[128 * 1024];
            long total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    break;
                }

                await file.WriteAsync(buffer.AsMemory(0, read));
                total += read;
                if (maxBytesLimit.HasValue && total > maxBytesLimit.Value)
                {
                    throw new InvalidOperationException($"File too large (> {maxBytesLimit.Value} bytes): {url}");
                }
            }

            await file.FlushAsync();
            if (total == 0)
            {
                throw new InvalidOperationException("Empty response");
            }

            if (string.IsNullOrWhiteSpace(Path.GetExtension(finalPath)) && !string.IsNullOrWhiteSpace(ct))
            {
                var extCandidate = EnsureExt(Path.GetFileName(finalPath), ct);
                if (!string.Equals(extCandidate, Path.GetFileName(finalPath), StringComparison.Ordinal))
                {
                    var newPath = Path.Combine(Path.GetDirectoryName(finalPath)!, extCandidate);
                    try
                    {
                        if (File.Exists(newPath))
                        {
                            File.Delete(newPath);
                        }

                        File.Move(finalPath, newPath);
                        finalPath = newPath;
                    }
                    catch
                    {
                        // ignore rename errors
                    }
                }
            }

            return new DownloadResult(finalPath, ct, total);
        }
    }

    private static (bool Success, string? ArchiveUrl, string? Error) ParseArchiveUrl(byte[] xmlBytes)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(xmlBytes));
        var fault = doc.Root?.XPathSelectElement($".//{{{NsSoap}}}Fault");
        if (fault != null)
        {
            var faultString = fault.Element("faultstring")?.Value?.Trim();
            return (false, null, faultString);
        }

        var archive = doc.Root?.XPathSelectElement(".//*[local-name()='archiveUrl']");
        if (archive != null && !string.IsNullOrWhiteSpace(archive.Value))
        {
            return (true, archive.Value.Trim(), null);
        }

        return (true, null, null);
    }

    private static NoticeDetails ExtractDetailsAndLinks(byte[] xmlBytes)
    {
        var doc = XDocument.Parse(XmlText(xmlBytes));
        var root = doc.Root ?? throw new InvalidDataException("XML root is missing");

        var details = new NoticeDetails
        {
            DocKind = root.Name.LocalName,
            PurchaseNumber = Val(root, ".//{*}purchaseNumber", ".//{*}notificationNumber"),
            Ikz = Val(root, ".//{*}IKZ", ".//{*}ikz"),
            PlacingCode = Val(root, ".//{*}placingWay/{*}code"),
            PlacingName = Val(root, ".//{*}placingWay/{*}name"),
            CustomerName = Val(root, ".//{*}customer/{*}fullName", ".//{*}customer/{*}shortName", ".//{*}organizationName"),
            CustomerInn = Val(root, ".//{*}customer/{*}INN", ".//{*}customer/{*}inn"),
            CustomerKpp = Val(root, ".//{*}customer/{*}KPP", ".//{*}customer/{*}kpp"),
            MaxPrice = Val(root, ".//{*}maxPrice", ".//{*}initialSum", ".//{*}contractMaxPrice"),
            Currency = Val(root, ".//{*}currency/{*}code", ".//{*}currency"),
            Name = Val(root, ".//{*}purchaseObjectInfo", ".//{*}subject", ".//{*}purchaseName", ".//{*}fullName"),
            PublishDate = Val(root, ".//{*}publishDate", ".//{*}docPublishDate", ".//{*}placementDate"),
            ApplicationsStart = Val(root, ".//{*}applicationsStartDate", ".//{*}applicationStartDate"),
            ApplicationsEnd = Val(root, ".//{*}applicationsEndDate", ".//{*}applicationEndDate", ".//{*}endDate"),
            Platform = Val(root, ".//{*}electronicPlace/{*}name", ".//{*}electronicPlatformName", ".//{*}platformName", ".//{*}oosElectronicPlace/{*}name")
        };

        var okpd2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.XPathSelectElements(".//*[local-name()='OKPD2']/*[local-name()='code']"))
        {
            if (!string.IsNullOrWhiteSpace(el.Value))
            {
                okpd2.Add(el.Value.Trim());
            }
        }

        if (okpd2.Count == 0)
        {
            foreach (var el in root.XPathSelectElements(".//*[local-name()='OKPD']/*[local-name()='code']"))
            {
                if (!string.IsNullOrWhiteSpace(el.Value))
                {
                    okpd2.Add(el.Value.Trim());
                }
            }
        }

        details.Okpd2 = okpd2.Count > 0
            ? string.Join(",", okpd2.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        var links = new List<AttachmentLink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.Descendants())
        {
            var local = el.Name.LocalName.ToLowerInvariant();
            if (AppData.AttachmentLocalNames.Contains(local) && !string.IsNullOrWhiteSpace(el.Value))
            {
                var url = el.Value.Trim();
                if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && seen.Add(url))
                {
                    string? name = null;
                    var parent = el.Parent;
                    if (parent != null)
                    {
                        foreach (var tag in new[] { "fileName", "documentName", "name", "docName" })
                        {
                            var cand = parent.Descendants().FirstOrDefault(d => string.Equals(d.Name.LocalName, tag, StringComparison.OrdinalIgnoreCase));
                            if (cand != null && !string.IsNullOrWhiteSpace(cand.Value))
                            {
                                name = cand.Value.Trim();
                                break;
                            }
                        }
                    }

                    links.Add(new AttachmentLink(url, name ?? string.Empty));
                }
            }

            foreach (var attr in el.Attributes())
            {
                if (AppData.AttributeNames.Contains(attr.Name.LocalName) && attr.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase) && seen.Add(attr.Value))
                {
                    links.Add(new AttachmentLink(attr.Value, string.Empty));
                }
            }
        }

        details.Links = links;
        return details;
    }

    private static bool AnyKeyword(string text) => KeywordRegexes.Any(r => r.IsMatch(text));

    private static string XmlText(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static string Val(XElement root, params string[] paths)
    {
        foreach (var p in paths)
        {
            var xp = ConvertPath(p);
            var el = root.XPathSelectElement(xp);
            if (el != null)
            {
                var value = el.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static string ConvertPath(string path)
    {
        return Regex.Replace(path, "\\{[^}]*\\}([A-Za-z0-9_]+)", m => $"*[local-name()='{m.Groups[1].Value}']");
    }

    private static string SanitizeName(string name, int maxLength = 180)
    {
        var replaced = Regex.Replace(name, "[/\\\\?%*:|\"<>]", "_");
        replaced = Regex.Replace(replaced, "\\s+", " ").Trim();
        if (replaced.Length > maxLength)
        {
            replaced = replaced[..maxLength];
        }

        return replaced.TrimEnd('.', '_', ' ');
    }

    private static string GuessFilenameFromUrl(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var file = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrEmpty(file))
        {
            file = "file";
        }

        file = Uri.UnescapeDataString(file);

        if (string.IsNullOrWhiteSpace(Path.GetExtension(file)) && !string.IsNullOrEmpty(uri.Query))
        {
            var query = ParseQuery(uri.Query);
            foreach (var key in new[] { "filename", "fileName", "name" })
            {
                if (query.TryGetValue(key, out var value) && value.Count > 0 && !string.IsNullOrWhiteSpace(value[0]))
                {
                    file = Uri.UnescapeDataString(value[0]);
                    break;
                }
            }
        }

        return SanitizeName(file);
    }

    private static Dictionary<string, List<string>> ParseQuery(string query)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (query.StartsWith("?", StringComparison.Ordinal))
        {
            query = query[1..];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<string>();
                dict[key] = list;
            }

            list.Add(value);
        }

        return dict;
    }

    private static string EnsureExt(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(fileName)) || string.IsNullOrWhiteSpace(contentType))
        {
            return fileName;
        }

        var mapping = AppData.ContentTypeExtensions;
        var ct = contentType.Split(';')[0].Trim();
        if (mapping.TryGetValue(ct, out var ext))
        {
            return fileName + ext;
        }

        return fileName;
    }

    private static string? ParseDispositionFilename(string? contentDisposition)
    {
        if (string.IsNullOrWhiteSpace(contentDisposition))
        {
            return null;
        }

        var matchStar = Regex.Match(contentDisposition, "filename\\*=(?:UTF-8''|)([^;]+)", RegexOptions.IgnoreCase);
        if (matchStar.Success)
        {
            return SanitizeName(Uri.UnescapeDataString(matchStar.Groups[1].Value.Trim().Trim('"')));
        }

        var match = Regex.Match(contentDisposition, "filename=\"?([^\";]+)\"?", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return SanitizeName(Uri.UnescapeDataString(match.Groups[1].Value.Trim()));
        }

        return null;
    }

    private static void SaveManifestRow(string folder, NoticeDetails details, List<ManifestFileRow> files)
    {
        var manifest = Path.Combine(folder, "manifest.tsv");
        var metaHeaders = new[] { "purchaseNumber", "docKind", "placingCode", "placingName", "customerName", "customerINN", "customerKPP", "maxPrice", "currency", "publishDate", "appStart", "appEnd", "platform", "okpd2", "name" };
        var fileHeaders = new[] { "ordinal", "source", "url", "saved_as", "content_type", "bytes" };

        var firstWrite = !File.Exists(manifest);
        using var writer = new StreamWriter(manifest, append: true, Encoding.UTF8);
        if (firstWrite)
        {
            writer.WriteLine("# meta");
            writer.WriteLine(string.Join('\t', metaHeaders));
        }

        writer.WriteLine(string.Join('\t', metaHeaders.Select(h => details.GetValue(h) ?? string.Empty)));

        if (files.Count > 0)
        {
            if (firstWrite)
            {
                writer.WriteLine("# files");
                writer.WriteLine(string.Join('\t', fileHeaders));
            }

            foreach (var row in files)
            {
                writer.WriteLine(string.Join('\t', new[]
                {
                    row.Ordinal ?? string.Empty,
                    row.Source ?? string.Empty,
                    row.Url ?? string.Empty,
                    row.SavedAs ?? string.Empty,
                    row.ContentType ?? string.Empty,
                    row.Bytes ?? string.Empty
                }));
            }
        }
    }

    private static string BuildGetDocsByOrgRegion(string token, int region, string subsystem, string docType, string exactDate)
    {
        return $"""<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ws=\"http://zakupki.gov.ru/fz44/get-docs-ip/ws\">
  <soapenv:Header>
    <individualPerson_token>{SecurityElement.Escape(token)}</individualPerson_token>
  </soapenv:Header>
  <soapenv:Body>
    <ws:getDocsByOrgRegionRequest>
      <index>
        <id>{Guid.NewGuid()}</id>
        <createDateTime>{DateTime.Now:yyyy-MM-ddTHH:mm:ss}</createDateTime>
        <mode>PROD</mode>
      </index>
      <selectionParams>
        <orgRegion>{region.ToString("00", CultureInfo.InvariantCulture)}</orgRegion>
        <subsystemType>{subsystem}</subsystemType>
        <documentType44>{docType}</documentType44>
        <periodInfo><exactDate>{exactDate}</exactDate></periodInfo>
      </selectionParams>
    </ws:getDocsByOrgRegionRequest>
  </soapenv:Body>
</soapenv:Envelope>""";
    }

    private static string BuildGetDocsByReestrNumber(string token, string reestr)
    {
        return $"""<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ws=\"http://zakupki.gov.ru/fz44/get-docs-ip/ws\">
  <soapenv:Header>
    <individualPerson_token>{SecurityElement.Escape(token)}</individualPerson_token>
  </soapenv:Header>
  <soapenv:Body>
    <ws:getDocsByReestrNumberRequest>
      <index>
        <id>{Guid.NewGuid()}</id>
        <createDateTime>{DateTime.Now:yyyy-MM-ddTHH:mm:ss}</createDateTime>
        <mode>PROD</mode>
      </index>
      <selectionParams>
        <subsystemType>PRIZ</subsystemType>
        <reestrNumber>{SecurityElement.Escape(reestr)}</reestrNumber>
      </selectionParams>
    </ws:getDocsByReestrNumberRequest>
  </soapenv:Body>
</soapenv:Envelope>""";
    }

    private static Options ParseArgs(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--token":
                    options.Token = Next(args, ref i, "--token");
                    break;
                case "--days":
                    options.Days = int.Parse(Next(args, ref i, "--days"), CultureInfo.InvariantCulture);
                    break;
                case "--regions":
                    var regionValue = Next(args, ref i, "--regions");
                    options.Regions = regionValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => int.Parse(r, CultureInfo.InvariantCulture)).ToList();
                    break;
                case "--include223":
                    options.Include223 = true;
                    break;
                case "--sleep":
                    options.SleepSeconds = double.Parse(Next(args, ref i, "--sleep"), CultureInfo.InvariantCulture);
                    break;
                case "--limit":
                    options.Limit = int.Parse(Next(args, ref i, "--limit"), CultureInfo.InvariantCulture);
                    break;
                case "--download-attachments":
                    options.DownloadAttachments = true;
                    break;
                case "--fetch-by-purchase":
                    options.FetchByPurchase = true;
                    break;
                case "--max-file-mb":
                    options.MaxFileMb = int.Parse(Next(args, ref i, "--max-file-mb"), CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentException($"Неизвестный аргумент: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new ArgumentException("Требуется указать --token");
        }

        return options;
    }

    private static string Next(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Опция {name} требует значение");
        }

        index += 1;
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Использование: dotnet run -- --token <TOKEN> [опции]");
        Console.WriteLine("Опции:");
        Console.WriteLine("  --token VALUE              individualPerson_token из PMD (обязательно)");
        Console.WriteLine("  --days N                   Сколько последних дней просматривать (по умолчанию 7)");
        Console.WriteLine("  --regions LIST             Перечень регионов через запятую (по умолчанию все)");
        Console.WriteLine("  --include223               Включить поиск RI223/223-ФЗ");
        Console.WriteLine("  --sleep SECONDS            Пауза между днями (по умолчанию 0.4)");
        Console.WriteLine("  --limit N                  Ограничение по числу найденных закупок");
        Console.WriteLine("  --download-attachments     Скачивать вложения");
        Console.WriteLine("  --fetch-by-purchase        Запрашивать пакет по номеру закупки");
        Console.WriteLine("  --max-file-mb N            Ограничить размер одного файла (0 = без ограничения, по умолчанию 200)");
    }
}

internal sealed class Options
{
    public string Token { get; set; } = string.Empty;
    public int Days { get; set; } = 7;
    public List<int> Regions { get; set; } = new();
    public bool Include223 { get; set; }
    public double SleepSeconds { get; set; } = 0.4;
    public int Limit { get; set; }
    public bool DownloadAttachments { get; set; }
    public bool FetchByPurchase { get; set; }
    public int MaxFileMb { get; set; } = 200;
}

internal sealed class NoticeDetails
{
    public string DocKind { get; set; } = string.Empty;
    public string? PurchaseNumber { get; set; }
    public string? Ikz { get; set; }
    public string? PlacingCode { get; set; }
    public string? PlacingName { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerInn { get; set; }
    public string? CustomerKpp { get; set; }
    public string? MaxPrice { get; set; }
    public string? Currency { get; set; }
    public string? Name { get; set; }
    public string? PublishDate { get; set; }
    public string? ApplicationsStart { get; set; }
    public string? ApplicationsEnd { get; set; }
    public string? Platform { get; set; }
    public string? Okpd2 { get; set; }
    public List<AttachmentLink> Links { get; set; } = new();

    public string? GetValue(string key) => key switch
    {
        "purchaseNumber" => PurchaseNumber,
        "docKind" => DocKind,
        "placingCode" => PlacingCode,
        "placingName" => PlacingName,
        "customerName" => CustomerName,
        "customerINN" => CustomerInn,
        "customerKPP" => CustomerKpp,
        "maxPrice" => MaxPrice,
        "currency" => Currency,
        "publishDate" => PublishDate,
        "appStart" => ApplicationsStart,
        "appEnd" => ApplicationsEnd,
        "platform" => Platform,
        "okpd2" => Okpd2,
        "name" => Name,
        _ => string.Empty
    };
}

internal sealed record AttachmentLink(string Url, string Name);

internal sealed class ManifestFileRow
{
    public string? Ordinal { get; set; }
    public string? Source { get; set; }
    public string? Url { get; set; }
    public string? SavedAs { get; set; }
    public string? ContentType { get; set; }
    public string? Bytes { get; set; }
}

internal sealed record DownloadResult(string FinalPath, string? ContentType, long BytesWritten);

internal static class AppData
{
    public static readonly IReadOnlyList<string> DocTypes44 = new[]
    {
        "epNotificationEF2020",
        "epNotificationEOK2020",
        "epNotificationOK2020",
        "epNotificationEZK2020"
    };

    public static readonly IReadOnlyList<string> DocTypes223 = new[] { "purchaseNotice" };

    public static readonly IReadOnlyList<int> RegionsAll = new List<int>
    {
        1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,
        31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,
        58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,83,86,87,89,90,91,92
    };

    public static readonly string[] KeywordPatterns =
    {
        "разработ", "доработ", "модерниз", "создан", "внедрени",
        "программ", "\\bПО\\b", "\\bИС\\b", "software",
        "информационн", "автоматизац", "портал", "веб[- ]?разработ", "сайт", "мобильн"
    };

    public static readonly HashSet<string> AttachmentLocalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "url", "href", "docurl", "documenturl", "fileurl", "downloadurl"
    };

    public static readonly HashSet<string> AttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "url", "link"
    };

    public static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["application/zip"] = ".zip",
        ["application/xml"] = ".xml",
        ["text/xml"] = ".xml",
        ["text/plain"] = ".txt",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.ms-powerpoint"] = ".ppt",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/tiff"] = ".tif"
    };
}
