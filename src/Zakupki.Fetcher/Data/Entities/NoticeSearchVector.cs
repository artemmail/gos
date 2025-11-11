using System;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeSearchVector
{
    public Guid Id { get; set; }

    public Guid NoticeVersionId { get; set; }

    public string AggregatedText { get; set; } = null!;

    public byte[]? EmbeddingVector { get; set; }

    public NoticeVersion NoticeVersion { get; set; } = null!;
}
