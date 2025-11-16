using System;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeEmbedding
{
    public Guid Id { get; set; }

    public Guid NoticeId { get; set; }

    public string Model { get; set; } = null!;

    public int Dimensions { get; set; }

    public string Vector { get; set; } = null!;

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Notice Notice { get; set; } = null!;
}
