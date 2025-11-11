using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeAttachment
{
    public Guid Id { get; set; }

    public Guid NoticeVersionId { get; set; }

    public string PublishedContentId { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public long? FileSize { get; set; }

    public string? Description { get; set; }

    public DateTime? DocumentDate { get; set; }

    public string? DocumentKindCode { get; set; }

    public string? DocumentKindName { get; set; }

    public string? Url { get; set; }

    public string? ContentHash { get; set; }

    public byte[]? BinaryContent { get; set; }

    public DateTime InsertedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public string? SourceFileName { get; set; }

    public NoticeVersion NoticeVersion { get; set; } = null!;

    public ICollection<AttachmentSignature> Signatures { get; set; } = new List<AttachmentSignature>();
}
