using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeVersion
{
    public Guid Id { get; set; }

    public Guid NoticeId { get; set; }

    public string ExternalId { get; set; } = null!;

    public int VersionNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime VersionReceivedAt { get; set; }

    public byte[]? RawXml { get; set; }

    public string? Hash { get; set; }

    public DateTime InsertedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public string? SourceFileName { get; set; }

    public Guid? ImportBatchId { get; set; }

    public Notice Notice { get; set; } = null!;

    public ImportBatch? ImportBatch { get; set; }

    public ProcedureWindow? ProcedureWindow { get; set; }

    public ICollection<NoticeAttachment> Attachments { get; set; } = new List<NoticeAttachment>();

    public NoticeSearchVector? SearchVector { get; set; }
}
