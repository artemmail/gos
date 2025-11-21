using System;

namespace Zakupki.Fetcher.Models;

public sealed class FavoriteSearchEnqueueRequest
{
    public string? Query { get; set; }

    public Guid QueryVectorId { get; set; }

    public DateTime? CollectingEndLimit { get; set; }

    public bool ExpiredOnly { get; set; }

    public int SimilarityThresholdPercent { get; set; } = 60;

    public int Top { get; set; } = 20;

    public int Limit { get; set; } = 500;
}
