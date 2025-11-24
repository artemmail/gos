using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeEmbeddingVectorizer : BackgroundService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly IEventBusPublisher _eventBusPublisher;
    private readonly NoticeEmbeddingOptions _options;
    private readonly EventBusOptions _eventBusOptions;
    private readonly ILogger<NoticeEmbeddingVectorizer> _logger;

    public NoticeEmbeddingVectorizer(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IEventBusPublisher eventBusPublisher,
        IOptions<EventBusOptions> eventBusOptions,
        IOptions<NoticeEmbeddingOptions> options,
        ILogger<NoticeEmbeddingVectorizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _eventBusPublisher = eventBusPublisher;
        _eventBusOptions = eventBusOptions.Value;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notice embedding vectorizer is disabled.");
            return;
        }

        await PurgeQueuesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var page = 0;

                while (!stoppingToken.IsCancellationRequested)
                {
                    var batch = await LoadBatchAsync(page, stoppingToken);

                    if (batch.Count == 0)
                    {
                        // Страницы кончились — всю таблицу прочитали.
                        _logger.LogInformation(
                            "No more notices to queue for vectorization. Processed {Pages} pages.",
                            page);
                        break;
                    }

                    var request = new QueryVectorBatchRequest
                    {
                        ServiceId = _options.ServiceId,
                        Items = batch
                    };

                    await _eventBusPublisher.PublishQueryVectorRequestAsync(request, stoppingToken);

                    _logger.LogInformation(
                        "Queued page {Page} with {Count} notices for vectorization with service id {ServiceId}",
                        page,
                        batch.Count,
                        _options.ServiceId);

                    page++;
                }

                // Если нужен всего один полный проход по таблице — вместо Delay можно просто выйти из цикла:
                // break;
                await Task.Delay(TimeSpan.FromSeconds(_options.IdleDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending notice embeddings for vectorization. Retrying...");
                await Task.Delay(TimeSpan.FromSeconds(_options.IdleDelaySeconds), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Читает очередную страницу таблицы Notices, без фильтра по Vector.
    /// </summary>
    private async Task<IReadOnlyList<QueryVectorRequestItem>> LoadBatchAsync(
        int pageIndex,
        CancellationToken cancellationToken)
    {
        var skip = pageIndex * _options.BatchSize;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var batch = await context.Notices
            .AsNoTracking()
            .Where(x=>x.Vector == null)
            .OrderBy(n => n.Id) // фиксируем порядок
            .Skip(skip)         // пропускаем предыдущие страницы
            .Take(_options.BatchSize)
            .Select(n => new QueryVectorRequestItem
            {
                UserId = "",
                Id = n.Id,
                String = n.PurchaseObjectInfo
            })
            .ToListAsync(cancellationToken);

        return batch;
    }

    private async Task PurgeQueuesAsync(CancellationToken cancellationToken)
    {
        if (!_eventBusOptions.Enabled)
        {
            _logger.LogWarning("Skipping queue purge because the event bus is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_eventBusOptions.QueryVectorRequestQueueName)
            && string.IsNullOrWhiteSpace(_eventBusOptions.QueryVectorResponseQueueName))
        {
            _logger.LogWarning("Skipping queue purge because vector queues are not configured.");
            return;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _eventBusOptions.BusAccess.Host,
                UserName = _eventBusOptions.BusAccess.UserName,
                Password = _eventBusOptions.BusAccess.Password,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            if (!string.IsNullOrWhiteSpace(_eventBusOptions.QueryVectorRequestQueueName))
            {
                channel.QueueDeclare(
                    queue: _eventBusOptions.QueryVectorRequestQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                channel.QueuePurge(_eventBusOptions.QueryVectorRequestQueueName);
                _logger.LogInformation(
                    "Purged query vector request queue {Queue}",
                    _eventBusOptions.QueryVectorRequestQueueName);
            }

            if (!string.IsNullOrWhiteSpace(_eventBusOptions.QueryVectorResponseQueueName))
            {
                channel.QueueDeclare(
                    queue: _eventBusOptions.QueryVectorResponseQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                channel.QueuePurge(_eventBusOptions.QueryVectorResponseQueueName);
                _logger.LogInformation(
                    "Purged query vector response queue {Queue}",
                    _eventBusOptions.QueryVectorResponseQueueName);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge vector queues on startup");
        }
    }
}
