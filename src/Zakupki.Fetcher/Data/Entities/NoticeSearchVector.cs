using System;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeSearchVector
{
    public Guid Id { get; set; }

    public Guid NoticeId { get; set; }

    public string AggregatedText { get; set; } = null!;

    public byte[]? EmbeddingVector { get; set; }

    public Notice Notice { get; set; } = null!;
}
