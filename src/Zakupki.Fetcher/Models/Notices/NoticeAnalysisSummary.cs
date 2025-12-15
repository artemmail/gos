namespace Zakupki.Fetcher.Models.Notices;

public sealed class NoticeAnalysisSummary
{
    public string? Status { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool HasResult { get; init; }
    public bool? Recommended { get; init; }
    public double? DecisionScore { get; init; }
}
