using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FabrikantGrabber.Models;
using FabrikantGrabber.Parsers;

namespace FabrikantGrabber.Services;

public sealed class FabrikantScraper
{
    private readonly HttpClient _httpClient;
    private readonly ProcedurePageParser _procedurePageParser;
    private readonly DocumentationParser _documentationParser;
    private readonly SearchPageParser _searchPageParser;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string BaseUrl = "https://www.fabrikant.ru";
    private const string ViewPath = "/v2/trades/procedure/view/";
    private const string DocsPath = "/v2/trades/procedure/documentation/";

    public FabrikantScraper(
        HttpClient httpClient,
        ProcedurePageParser procedurePageParser,
        DocumentationParser documentationParser,
        SearchPageParser searchPageParser)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _procedurePageParser = procedurePageParser ?? throw new ArgumentNullException(nameof(procedurePageParser));
        _documentationParser = documentationParser ?? throw new ArgumentNullException(nameof(documentationParser));
        _searchPageParser = searchPageParser ?? throw new ArgumentNullException(nameof(searchPageParser));
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

        var htmlView = await _httpClient.GetStringAsync(viewUrl, cancellationToken);
        var procedure = _procedurePageParser.Parse(htmlView, procedureId);

        var docsHtml = await _httpClient.GetStringAsync(docsUrl, cancellationToken);
        var baseUri = new Uri(docsUrl);
        var docLinks = _documentationParser.ParseDocumentationLinks(docsHtml, baseUri);

        Console.WriteLine("[*] Найдено документов: " + docLinks.Count);

        procedure.Documents = docLinks;

        var jsonFileName = SanitizeFileName(procedureId) + ".json";
        var jsonPath = Path.Combine(outputFolder, jsonFileName);
        var json = JsonSerializer.Serialize(procedure, JsonOptions);

        await using (var fs = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            await writer.WriteAsync(json);
        }

        Console.WriteLine("[*] JSON сохранён: " + jsonPath);

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

    public async Task<SearchDownloadResult> DownloadSearchResultsAsync(
        string searchUrl,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[*] Search URL: " + searchUrl);

        var uri = new Uri(searchUrl);
        var queryParams = ParseQueryParameters(uri.Query);

        var firstPageHtml = await _httpClient.GetStringAsync(uri, cancellationToken);
        var searchResult = _searchPageParser.Parse(firstPageHtml, new Uri(BaseUrl));

        var pageSize = ExtractPageSize(queryParams, searchResult.Procedures.Count);
        var totalPages = CalculateTotalPages(searchResult.TotalCount, pageSize);

        Console.WriteLine("[*] Найдено заявок всего: " + searchResult.TotalCount);
        Console.WriteLine("[*] Размер страницы: " + pageSize);
        Console.WriteLine("[*] Количество страниц: " + totalPages);
        Console.WriteLine("[*] Найдено процедур на странице: " + searchResult.Procedures.Count);

        var allProcedures = new List<FabrikantSearchItem>(searchResult.Procedures);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in allProcedures)
            seenIds.Add(p.ProcedureId);

        if (totalPages > 1)
        {
            for (var page = 2; page <= totalPages; page++)
            {
                var pageUrl = BuildPageUrl(uri, queryParams, page);
                Console.WriteLine($"[*] Загружаю страницу {page}/{totalPages}: {pageUrl}");

                var html = await _httpClient.GetStringAsync(pageUrl, cancellationToken);
                var pageResult = _searchPageParser.Parse(html, new Uri(BaseUrl));

                Console.WriteLine($"    -> Найдено процедур на странице: {pageResult.Procedures.Count}");

                foreach (var p in pageResult.Procedures)
                {
                    if (seenIds.Add(p.ProcedureId))
                        allProcedures.Add(p);
                }
            }
        }

        searchResult.Procedures = allProcedures;
        searchResult.PageSize = pageSize;
        searchResult.TotalPages = totalPages;

        var jsonFileName = SanitizeFileName("search_results") + ".json";
        var jsonPath = Path.Combine(outputFolder, jsonFileName);
        var json = JsonSerializer.Serialize(searchResult, JsonOptions);

        await using (var fs = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            await writer.WriteAsync(json);
        }

        return new SearchDownloadResult
        {
            JsonPath = jsonPath,
            SearchResult = searchResult
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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static List<KeyValuePair<string, string>> ParseQueryParameters(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<KeyValuePair<string, string>>();

        if (query.StartsWith("?"))
            query = query[1..];

        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<KeyValuePair<string, string>>(parts.Length);

        foreach (var part in parts)
        {
            var split = part.Split('=', 2);
            var key = Uri.UnescapeDataString(split[0]);
            var value = split.Length > 1 ? Uri.UnescapeDataString(split[1]) : string.Empty;
            result.Add(new KeyValuePair<string, string>(key, value));
        }

        return result;
    }

    private static int ExtractPageSize(IEnumerable<KeyValuePair<string, string>> parameters, int fallback)
    {
        var param = parameters.LastOrDefault(p => p.Key.Equals("page_limit", StringComparison.OrdinalIgnoreCase));
        if (param.Key != null && int.TryParse(param.Value, out var pageSize) && pageSize > 0)
            return pageSize;

        return fallback > 0 ? fallback : 40;
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        if (pageSize <= 0)
            return 1;

        if (totalCount <= 0)
            return 1;

        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static string BuildPageUrl(Uri baseUri, List<KeyValuePair<string, string>> originalParams, int pageNumber)
    {
        var parameters = originalParams
            .Where(p => !p.Key.Equals("page_number", StringComparison.OrdinalIgnoreCase))
            .ToList();

        parameters.Add(new KeyValuePair<string, string>("page_number", pageNumber.ToString()));

        var query = string.Join(
            "&",
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var builder = new UriBuilder(baseUri)
        {
            Query = query
        };

        return builder.Uri.ToString();
    }
}
