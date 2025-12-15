namespace Zakupki.Fetcher.Models.Notices;

public record MosNoticeDetailsDto(
    Guid Id,
    string PurchaseNumber,
    string? RawJson,
    bool Uncompleted,
    UndocumentedAuctionDto? Details);
