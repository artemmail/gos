namespace Zakupki.Fetcher.Models.Notices;

public sealed class VectorSearchRequest
{
    public required string UserId { get; init; }

    public required Guid QueryVectorId { get; init; }

    public int SimilarityThresholdPercent { get; init; }

    public bool ExpiredOnly { get; init; }

    public bool FilterByUserRegions { get; init; }

    public bool FilterByUserOkpd2Codes { get; init; }

    public DateTimeOffset? CollectingEndLimit { get; init; }

    public string? SortField { get; init; }

    public string? SortDirection { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
