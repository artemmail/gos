using System;

namespace Zakupki.Fetcher.Models;

public sealed record NoticeAnalysisResponse(
    Guid NoticeId,
    string Status,
    bool HasAnswer,
    string? Result,
    string? Error,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    string? Prompt,
    TenderAnalysisResult? StructuredResult,
    double? DecisionScore,
    bool? Recommended);
