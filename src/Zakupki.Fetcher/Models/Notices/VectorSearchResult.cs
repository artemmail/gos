namespace Zakupki.Fetcher.Models.Notices;

public sealed record VectorSearchResult(
    VectorSearchError? Error,
    PagedResult<NoticeListItemDto>? Data,
    string? Message)
{
    public bool Success => Error is null;

    public static VectorSearchResult FromError(VectorSearchError error, string? message = null) =>
        new(error, null, message);

    public static VectorSearchResult FromData(PagedResult<NoticeListItemDto> data) =>
        new(null, data, null);
}

public enum VectorSearchError
{
    QueryNotFound,
    QueryNotReady,
    MissingRegions,
    Unknown
}
