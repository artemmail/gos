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
        DocumentationParser documentationParser)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _procedurePageParser = procedurePageParser ?? throw new ArgumentNullException(nameof(procedurePageParser));
        _documentationParser = documentationParser ?? throw new ArgumentNullException(nameof(documentationParser));
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
}
