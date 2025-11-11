using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Data.Entities;

public class ImportBatch
{
    public Guid Id { get; set; }

    public string SourceFileName { get; set; } = null!;

    public string? Period { get; set; }

    public string? Checksum { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ICollection<NoticeVersion> NoticeVersions { get; set; } = new List<NoticeVersion>();
}
