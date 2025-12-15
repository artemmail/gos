namespace Zakupki.Fetcher.Models.Notices;

public record MosNoticeDetailsDto(
    Guid Id,
    string PurchaseNumber,
    string? RawJson,
    UndocumentedAuctionDto? Details);
