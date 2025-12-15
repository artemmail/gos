using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zakupki.MosApi.V2
{
    public partial class MosSwaggerClientV2
    {
        private readonly CookieContainer _cookieContainer;

        // ? новый ctor: добавили CookieContainer, но Ќ≈ ломаем старый Ч цепл€емс€ к нему
        public MosSwaggerClientV2(HttpClient httpClient, string baseUrl, string? token, CookieContainer cookieContainer)
            : this(httpClient, baseUrl, token)
        {
            _cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));

            // Ѕазовые заголовки один раз на HttpClient (если уже где-то выставлены Ч это безопасно)
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123 Safari/537.36");

            if (!httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            }

            if (httpClient.DefaultRequestHeaders.AcceptLanguage.Count == 0)
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");
        }

        public async Task<UndocumentedAuctionResult?> FetchUndocumentedAuctionAsync(
            int auctionId,
            CancellationToken cancellationToken = default,
            Action<Exception?, string, object?[]>? logWarning = null)
        {
            var auctionUrl = $"https://zakupki.mos.ru/newapi/api/Auction/Get?auctionId={auctionId}";
            const int maxAttempts = 6;

            // ? прогрев сессии/куков на домене zakupki.mos.ru (чтобы cookie по€вились, если сайт их выдаЄт)
            await EnsureSessionAsync(cancellationToken, logWarning);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, auctionUrl);
                    req.Headers.Referrer = new Uri($"https://zakupki.mos.ru/auction/{auctionId}");

                    // ? вместо Thread.Sleep
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

                    using var response = await _httpClient.SendAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var rawOk = await response.Content.ReadAsStringAsync(cancellationToken);
                        var auctionOk = JsonSerializer.Deserialize<UndocumentedAuctionDto>(rawOk, _serializerOptions);
                        return new UndocumentedAuctionResult(auctionOk, rawOk);
                    }

                    var status = (int)response.StatusCode;
                    var isRetryable = IsRetryableMosStatus(status);

                    var contentType = response.Content.Headers.ContentType?.ToString();

                    string bodySnippet = string.Empty;
                    try
                    {
                        var rawErr = await response.Content.ReadAsStringAsync(cancellationToken);
                        bodySnippet = rawErr.Length > 1500 ? rawErr[..1500] : rawErr;
                    }
                    catch
                    {
                        // ignore
                    }

                    // ? УMOS или прокладкаФ Ч ключевые headers + cookie-len
                    var diag = BuildResponseDiagnostics(response);

                    logWarning?.Invoke(
                        null,
                        "Undocumented MOS auction API failed. Status={StatusCode} AuctionId={AuctionId} Attempt={Attempt}/{MaxAttempts} ContentType={ContentType} Diag={Diag} BodySnippet={BodySnippet}",
                        new object?[]
                        {
                            status,
                            auctionId,
                            attempt + 1,
                            maxAttempts,
                            contentType,
                            diag,
                            bodySnippet
                        });

                    if (!isRetryable)
                        return null;

                    var delay = GetRetryDelay(response, attempt);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logWarning?.Invoke(
                        ex,
                        "Failed to fetch undocumented MOS auction {AuctionId} attempt {Attempt}/{MaxAttempts}",
                        new object?[] { auctionId, attempt + 1, maxAttempts });

                    await Task.Delay(TimeSpan.FromMilliseconds(200 + attempt * 200), cancellationToken);
                }
            }

            return null;
        }

        private static bool IsRetryableMosStatus(int statusCode)
            => statusCode is 402 or 403 or 429 or 500 or 502 or 503 or 504;

        private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
        {
            if (response.Headers.RetryAfter?.Delta is { } delta)
                return delta;

            var ms = attempt switch
            {
                0 => 500,
                1 => 1000,
                2 => 2000,
                3 => 4000,
                4 => 8000,
                _ => 15000
            };

            return TimeSpan.FromMilliseconds(ms);
        }

        private async Task EnsureSessionAsync(
            CancellationToken ct,
            Action<Exception?, string, object?[]>? logWarning)
        {
            // ≈сли куки уже есть Ч не трогаем
            var siteUri = new Uri("https://zakupki.mos.ru/");
            try
            {
                // _cookieContainer может быть еще не проинициализирован, если кто-то вызвал старый ctor
                if (_cookieContainer != null && _cookieContainer.GetCookies(siteUri).Count > 0)
                    return;

                using var warmReq = new HttpRequestMessage(HttpMethod.Get, siteUri);
                using var warmResp = await _httpClient.SendAsync(warmReq, HttpCompletionOption.ResponseHeadersRead, ct);

                // читаем контент, чтобы соединение корректно освободилось + куки применились
                _ = await warmResp.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                logWarning?.Invoke(ex, "MOS warm-up (session/cookies) failed", Array.Empty<object?>());
            }
        }

        private string BuildResponseDiagnostics(HttpResponseMessage response)
        {
            static string? GetHeader(HttpResponseMessage r, string name)
            {
                if (r.Headers.TryGetValues(name, out var v)) return string.Join(",", v);
                if (r.Content.Headers.TryGetValues(name, out var cv)) return string.Join(",", cv);
                return null;
            }

            var uri = response.RequestMessage?.RequestUri;

            var sb = new StringBuilder();
            sb.Append("Uri=").Append(uri).Append("; ");
            sb.Append("Reason=").Append(response.ReasonPhrase).Append("; ");

            try
            {
                if (uri != null && _cookieContainer != null)
                {
                    var cookieHeader = _cookieContainer.GetCookieHeader(uri) ?? string.Empty;
                    sb.Append("CookieHeaderLen=").Append(cookieHeader.Length).Append("; ");
                }
            }
            catch
            {
                // ignore
            }

            var interesting = new[]
            {
                "Server",
                "Via",
                "Date",
                "Retry-After",
                "Location",
                "Set-Cookie",

                // Упрокладки/антиботФ
                "cf-ray",
                "cf-cache-status",
                "x-amz-cf-id",
                "x-amz-cf-pop",
                "x-request-id",
                "x-correlation-id",
                "x-sucuri-id",
                "x-sucuri-cache",
                "x-akamai-transformed"
            };

            var pairs = new List<string>();
            foreach (var h in interesting)
            {
                var val = GetHeader(response, h);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    if (h.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) && val.Length > 500)
                        val = val[..500] + "...";
                    pairs.Add($"{h}={val}");
                }
            }

            if (pairs.Count > 0)
                sb.Append(string.Join(" | ", pairs));

            return sb.ToString();
        }
    }
}
