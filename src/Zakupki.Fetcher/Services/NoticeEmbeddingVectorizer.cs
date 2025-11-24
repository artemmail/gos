using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeEmbeddingVectorizer : BackgroundService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly IEventBusPublisher _eventBusPublisher;
    private readonly NoticeEmbeddingOptions _options;
    private readonly ILogger<NoticeEmbeddingVectorizer> _logger;

    public NoticeEmbeddingVectorizer(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IEventBusPublisher eventBusPublisher,
        IOptions<NoticeEmbeddingOptions> options,
        ILogger<NoticeEmbeddingVectorizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _eventBusPublisher = eventBusPublisher;
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await LoadBatchAsync(stoppingToken);

                if (batch.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.IdleDelaySeconds), stoppingToken);
                    continue;
                }

                var request = new QueryVectorBatchRequest
                {
                    ServiceId = _options.ServiceId,
                    Items = batch
                };

                await _eventBusPublisher.PublishQueryVectorRequestAsync(request, stoppingToken);
                _logger.LogInformation(
                    "Queued {Count} notices for vectorization with service id {ServiceId}",
                    batch.Count,
                    _options.ServiceId);
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

    private async Task<IReadOnlyList<QueryVectorRequestItem>> LoadBatchAsync(
    CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var batch = await context.Notices
            .AsNoTracking()
            .Where(n => n.Vector == null)              // выбираем только те, где нет вектора
            .OrderBy(n => n.Id)                        // желательно зафиксировать порядок
            .Select(n => new QueryVectorRequestItem    // в SELECT попадают только нужные поля
            {
                UserId = Guid.Empty.ToString(),
                Id = n.Id,
                String = n.PurchaseObjectInfo
            })
            .Take(_options.BatchSize)                  // читаем кусками по BatchSize
            .ToListAsync(cancellationToken);

        // Возвращаем список "блока" (батча). Если пусто — пустой массив.
        return batch.Count > 0
            ? batch
            : Array.Empty<QueryVectorRequestItem>();
    }

   
}
