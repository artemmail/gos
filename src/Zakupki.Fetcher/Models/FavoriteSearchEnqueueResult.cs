namespace Zakupki.Fetcher.Models;

public enum FavoriteSearchEnqueueError
{
    None,
    Disabled,
    Duplicate,
    Invalid
}

public sealed record FavoriteSearchEnqueueResult(
    bool Enqueued,
    FavoriteSearchEnqueueError Error,
    string? Message
);
