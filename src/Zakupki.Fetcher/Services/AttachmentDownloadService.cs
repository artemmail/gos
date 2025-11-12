using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Zakupki.Fetcher.Services;

public class AttachmentDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly ILogger<AttachmentDownloadService> _logger;

    public AttachmentDownloadService(HttpClient httpClient, CookieContainer cookieContainer, ILogger<AttachmentDownloadService> logger)
    {
        _httpClient = httpClient;
        _cookieContainer = cookieContainer;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        }

        if (!_httpClient.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        }

        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
    }

    public void AddCookie(Uri baseUri, string name, string value)
    {
        if (baseUri is null)
        {
            throw new ArgumentNullException(nameof(baseUri));
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Cookie name must be provided", nameof(name));
        }

        _cookieContainer.Add(baseUri, new Cookie(name, value));
    }

    public Task<byte[]> DownloadAsync(string url, string? referer = null, CancellationToken cancellationToken = default)
    {
        return DownloadAsyncInternal(url, referer, cancellationToken);
    }

    private async Task<byte[]> DownloadAsyncInternal(string url, string? referer, CancellationToken cancellationToken)
    {
        var uri = BuildUri(url);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            request.Headers.Referrer = refererUri;
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var bodyPreview = await TryReadPreviewAsync(response, cancellationToken);
            throw new HttpRequestException($"GET {uri} -> {(int)response.StatusCode} {response.ReasonPhrase}\nFinalUri: {response.RequestMessage?.RequestUri}\nHeaders: {string.Join("; ", response.Headers.Select(h => h.Key + '=' + string.Join(",", h.Value)))}\nBody[0..512]: {bodyPreview}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var memoryStream = new MemoryStream();
        await responseStream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private static Uri BuildUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL must be provided", nameof(url));
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        var safe = Uri.EscapeUriString(url);
        if (Uri.TryCreate(safe, UriKind.Absolute, out uri))
        {
            return uri;
        }

        throw new ArgumentException("Bad URL: " + url, nameof(url));
    }

    private async Task<string> TryReadPreviewAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var length = Math.Min(bytes.Length, 512);
            return Encoding.UTF8.GetString(bytes, 0, length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read response preview for {Uri}", response.RequestMessage?.RequestUri);
            return string.Empty;
        }
    }
}
