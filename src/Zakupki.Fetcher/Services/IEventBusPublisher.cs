using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public interface IEventBusPublisher
{
    Task PublishFavoriteSearchAsync(FavoriteSearchCommand command, CancellationToken cancellationToken);
}
