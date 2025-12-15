using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Models.Notices;

public record MosNoticeListItemDto(
    Guid Id,
    string PurchaseNumber,
    string? Name,
    DateTime? PublishDate,
    DateTime? CollectingEnd,
    decimal? MaxPrice,
    string? FederalLawName,
    byte Region,
    NoticeSource Source,
    bool Uncompleted,
    string? CustomerInn,
    string? CustomerName);
