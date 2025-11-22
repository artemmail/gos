using Microsoft.Data.SqlTypes;
using System;

namespace Zakupki.Fetcher.Data.Entities;

public class NoticeEmbedding
{
    public Guid Id { get; set; }

    public Guid NoticeId { get; set; }

    public SqlVector<float> Vector { get; set; }

    public string? Source { get; set; }

    public Notice Notice { get; set; } = null!;
}
