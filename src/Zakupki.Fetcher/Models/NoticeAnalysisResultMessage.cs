using System;

namespace Zakupki.Fetcher.Models;

public sealed class NoticeAnalysisResultMessage
{
    public Guid AnalysisId { get; set; }

    public Guid NoticeId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool HasResult { get; set; }

    public string? Error { get; set; }

    public DateTime UpdatedAt { get; set; }
}
