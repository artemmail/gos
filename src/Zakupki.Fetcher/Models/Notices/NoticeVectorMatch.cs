using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Models.Notices;

public sealed class NoticeVectorMatch
{
    public Notice Notice { get; init; } = null!;

    public double Distance { get; init; }

    public NoticeAnalysisSummary? Analysis { get; init; }
}
