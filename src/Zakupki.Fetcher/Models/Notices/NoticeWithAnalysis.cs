using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Models.Notices;

public sealed class NoticeWithAnalysis
{
    public Notice Notice { get; init; } = null!;

    public NoticeAnalysisSummary? Analysis { get; init; }

    public bool IsFavorite { get; init; }
}
