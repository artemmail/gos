using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace Zakupki.MosApi.V2
{
    public partial class MosSwaggerClientV2
    {
        public async Task<UndocumentedAuctionResult?> FetchUndocumentedAuctionAsync(
            int auctionId,
            CancellationToken cancellationToken = default,
            Action<Exception?, string, object?[]>? logWarning = null)
        {
            var auctionUrl = $"{_baseUrl}/newapi/api/Auction/Get?auctionId={auctionId}";
            const int maxAttempts = 6;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, auctionUrl);
                    req.Headers.TryAddWithoutValidation(
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123 Safari/537.36");
                    req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                    req.Headers.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en;q=0.8");
                    req.Headers.TryAddWithoutValidation("Referer", $"https://zakupki.mos.ru/auction/{auctionId}");

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

                    logWarning?.Invoke(
                        null,
                        "Undocumented MOS auction API failed. Status={StatusCode} AuctionId={AuctionId} Attempt={Attempt}/{MaxAttempts} ContentType={ContentType} BodySnippet={BodySnippet}",
                        new object?[]
                        {
                            status,
                            auctionId,
                            attempt + 1,
                            maxAttempts,
                            contentType,
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
                0 => 300,
                1 => 700,
                2 => 1500,
                3 => 3000,
                4 => 6000,
                _ => 10000
            };

            return TimeSpan.FromMilliseconds(ms);
        }
    }
}
