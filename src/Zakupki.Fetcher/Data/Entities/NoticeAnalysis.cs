using System;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeAnalysis
{
    public Guid Id { get; set; }

    public Guid NoticeId { get; set; }

    public string UserId { get; set; } = null!;

    public string Status { get; set; } = NoticeAnalysisStatus.NotStarted;

    public string? Result { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Notice Notice { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
