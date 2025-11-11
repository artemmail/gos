using System;

namespace Zakupki.Fetcher.Data.Entities;

public class AttachmentSignature
{
    public Guid Id { get; set; }

    public Guid AttachmentId { get; set; }

    public string SignatureType { get; set; } = null!;

    public string SignatureValue { get; set; } = null!;

    public NoticeAttachment Attachment { get; set; } = null!;
}
