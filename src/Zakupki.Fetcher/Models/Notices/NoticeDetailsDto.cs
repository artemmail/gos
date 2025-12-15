namespace Zakupki.Fetcher.Models.Notices;

public record NoticeDetailsDto(
    Guid Id,
    string PurchaseNumber,
    string? PurchaseObjectInfo,
    string? RawJson);
