using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class FavoriteSearchQueueService : IFavoriteSearchQueueService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IEventBusPublisher _eventBusPublisher;
    private readonly EventBusOptions _options;
    private readonly ILogger<FavoriteSearchQueueService> _logger;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    public FavoriteSearchQueueService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IMemoryCache memoryCache,
        IEventBusPublisher eventBusPublisher,
        IOptions<EventBusOptions> options,
        ILogger<FavoriteSearchQueueService> logger)
    {
        _dbContextFactory = dbContextFactory;
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

        if (string.IsNullOrWhiteSpace(_options.ResolveCommandQueueName()))
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Disabled, "Очередь не сконфигурирована");
        }

        if (request.CollectingEndLimit is null || request.QueryVectorId == Guid.Empty)
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Invalid, "Укажите запрос и дату CollectingEnd");
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var queryVector = await context.UserQueryVectors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.QueryVectorId && v.UserId == userId, cancellationToken);

        if (queryVector == null)
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Invalid, "Запрос не найден");
        }

        if (queryVector.Vector == null)
        {
            return new FavoriteSearchEnqueueResult(false, FavoriteSearchEnqueueError.Invalid, "Вектор запроса ещё не готов");
        }

        var normalizedQuery = string.IsNullOrWhiteSpace(request.Query)
            ? queryVector.Query.Trim()
            : request.Query.Trim();
        var collectingEndLimit = DateTime.SpecifyKind(request.CollectingEndLimit.Value, DateTimeKind.Utc).ToUniversalTime();
        var top = Math.Clamp(request.Top, 1, 100);
        var limit = Math.Clamp(request.Limit, top, top * 50);
        var expiredOnly = request.ExpiredOnly;
        var similarityThresholdPercent = Math.Clamp(request.SimilarityThresholdPercent, 40, 90);

        var dedupKey = FavoriteSearchCommand.CreateDeduplicationKey(
            userId,
            queryVector.Id,
            similarityThresholdPercent,
            collectingEndLimit,
            expiredOnly);

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
            QueryVectorId = queryVector.Id,
            CollectingEndLimit = collectingEndLimit,
            ExpiredOnly = expiredOnly,
            SimilarityThresholdPercent = similarityThresholdPercent,
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
