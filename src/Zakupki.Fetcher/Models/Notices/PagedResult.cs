namespace Zakupki.Fetcher.Models.Notices;

public record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);
