using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Models.Notices;

public record NoticeListItemDto(
    Guid Id,
    string PurchaseNumber,
    NoticeSource Source,
    DateTime? PublishDate,
    string? EtpName,
    byte Region,
    string? PurchaseObjectInfo,
    decimal? MaxPrice,
    string? Okpd2Code,
    string? Okpd2Name,
    string? KvrCode,
    string? KvrName,
    string? RawJson,
    DateTime? CollectingEnd,
    string? SubmissionProcedureDateRaw,
    bool HasAnalysisAnswer,
    string? AnalysisStatus,
    DateTime? AnalysisUpdatedAt,
    bool? Recommended,
    double? DecisionScore,
    bool IsFavorite,
    double? Similarity);
