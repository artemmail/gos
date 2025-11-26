using System;

namespace Zakupki.Fetcher.Models;

public sealed class NoticeAnalysisQueueMessage
{
    public Guid AnalysisId { get; set; }

    public Guid NoticeId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool Force { get; set; }
}
