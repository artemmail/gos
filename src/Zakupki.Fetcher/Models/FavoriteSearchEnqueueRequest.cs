namespace Zakupki.Fetcher.Models;

public sealed class FavoriteSearchEnqueueRequest
{
    public string? Query { get; set; }

    public DateTime? CollectingEndLimit { get; set; }

    public int Top { get; set; } = 20;

    public int Limit { get; set; } = 500;
}
