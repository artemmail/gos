using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public interface IFavoriteSearchQueueService
{
    Task<FavoriteSearchEnqueueResult> EnqueueAsync(
        string userId,
        FavoriteSearchEnqueueRequest request,
        CancellationToken cancellationToken);
}
