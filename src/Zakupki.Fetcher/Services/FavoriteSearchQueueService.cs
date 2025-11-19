using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class FavoriteSearchQueueService : IFavoriteSearchQueueService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IEventBusPublisher _eventBusPublisher;
    private readonly EventBusOptions _options;
    private readonly ILogger<FavoriteSearchQueueService> _logger;

    public FavoriteSearchQueueService(
        IMemoryCache memoryCache,
        IEventBusPublisher eventBusPublisher,
        IOptions<EventBusOptions> options,
        ILogger<FavoriteSearchQueueService> logger)
    {
        _memoryCache = memoryCache;
        _eventBusPublisher = eventBusPublisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FavoriteSearchEnqueueResult> EnqueueAsync(
        string userId,
        FavoriteSearchEnqueueRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Disabled, "Очередь недоступна");
        }

        if (string.IsNullOrWhiteSpace(request.Query) || request.CollectingEndLimit is null)
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Invalid, "Укажите запрос и дату CollectingEnd");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = request.Query.Trim();
        var collectingEndLimit = DateTime.SpecifyKind(request.CollectingEndLimit.Value, DateTimeKind.Utc).ToUniversalTime();
        var top = Math.Clamp(request.Top, 1, 100);
        var limit = Math.Clamp(request.Limit, top, top * 50);

        var dedupKey = FavoriteSearchCommand.CreateDeduplicationKey(userId, normalizedQuery, collectingEndLimit);

        if (_memoryCache.TryGetValue(dedupKey, out _))
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Duplicate, "Запрос уже в очереди");
        }

        using var entry = _memoryCache.CreateEntry(dedupKey);
        entry.Value = true;
        var ttlMinutes = Math.Max(1, _options.InFlightDeduplicationMinutes);
        entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(ttlMinutes));

        var command = new FavoriteSearchCommand
        {
            UserId = userId,
            Query = normalizedQuery,
            CollectingEndLimit = collectingEndLimit,
            Top = top,
            Limit = limit
        };

        try
        {
            await _eventBusPublisher.PublishFavoriteSearchAsync(command, cancellationToken);
            _logger.LogInformation("Добавлена задача избранного для пользователя {UserId}", userId);
            return new FavoriteSearchEnqueueResult(true, FavoriteSearchEnqueueError.None, null);
        }
        catch
        {
            _memoryCache.Remove(dedupKey);
            throw;
        }
    }
}
