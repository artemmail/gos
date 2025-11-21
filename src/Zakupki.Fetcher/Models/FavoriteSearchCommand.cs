using System;
using System.Text;

namespace Zakupki.Fetcher.Models;

public sealed class FavoriteSearchCommand
{
    public required string UserId { get; init; }

    public required string Query { get; init; }

    public required Guid QueryVectorId { get; init; }

    public DateTime CollectingEndLimit { get; init; }

    public int Top { get; init; }

    public int Limit { get; init; }

    public bool ExpiredOnly { get; init; }

    public int SimilarityThresholdPercent { get; init; }

    public static string CreateDeduplicationKey(
        string userId,
        Guid queryVectorId,
        int similarityThresholdPercent,
        DateTime collectingEndLimit,
        bool expiredOnly)
    {
        var normalizedDate = collectingEndLimit.ToUniversalTime().ToString("O");
        var expiredSuffix = expiredOnly ? "all" : "active";
        return $"{userId}:{queryVectorId}:{similarityThresholdPercent}:{normalizedDate}:{expiredSuffix}";
    }

    public string GetDeduplicationKey() => CreateDeduplicationKey(UserId, QueryVectorId, SimilarityThresholdPercent, CollectingEndLimit, ExpiredOnly);

    public byte[] GetDeduplicationKeyBytes() => Encoding.UTF8.GetBytes(GetDeduplicationKey());
}
