using System.Text;

namespace Zakupki.Fetcher.Models;

public sealed class FavoriteSearchCommand
{
    public required string UserId { get; init; }

    public required string Query { get; init; }

    public DateTime CollectingEndLimit { get; init; }

    public int Top { get; init; }

    public int Limit { get; init; }

    public bool ExpiredOnly { get; init; }

    public static string CreateDeduplicationKey(
        string userId,
        string query,
        DateTime collectingEndLimit,
        bool expiredOnly)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var normalizedDate = collectingEndLimit.ToUniversalTime().ToString("O");
        var expiredSuffix = expiredOnly ? "expired" : "active";
        return $"{userId}:{normalizedQuery}:{normalizedDate}:{expiredSuffix}";
    }

    public string GetDeduplicationKey() => CreateDeduplicationKey(UserId, Query, CollectingEndLimit, ExpiredOnly);

    public byte[] GetDeduplicationKeyBytes() => Encoding.UTF8.GetBytes(GetDeduplicationKey());
}
