using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data;
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

    private async Task<IReadOnlyList<QueryVectorRequestItem>> LoadBatchAsync(CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var notices = await context.Notices
            .AsNoTracking()
            .Where(n => !context.NoticeEmbeddings.Any(e => e.NoticeId == n.Id && e.Source == _options.Source))
            .OrderBy(n => n.PublishDate ?? DateTime.MinValue)
            .ThenBy(n => n.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var items = new List<QueryVectorRequestItem>(notices.Count);

        foreach (var notice in notices)
        {
            var text = BuildNoticeText(notice);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            items.Add(new QueryVectorRequestItem
            {
                Id = Guid.NewGuid(),
                UserId = notice.Id.ToString(),
                String = text
            });
        }

        return items;
    }

    private static string BuildNoticeText(Data.Entities.Notice notice)
    {
        var builder = new StringBuilder();

        void Append(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(label);
            builder.Append(':');
            builder.Append(value.Trim());
        }

        Append("Название", notice.PurchaseNumber);
        Append("Предмет закупки", notice.PurchaseObjectInfo);

        var okpd2 = CombineParts(notice.Okpd2Code, notice.Okpd2Name);
        Append("ОКПД2", okpd2);

        var kvr = CombineParts(notice.KvrCode, notice.KvrName);
        Append("КВР", kvr);

        return builder.ToString();
    }

    private static string? CombineParts(string? code, string? name)
    {
        var hasCode = !string.IsNullOrWhiteSpace(code);
        var hasName = !string.IsNullOrWhiteSpace(name);

        if (hasCode && hasName)
        {
            return $"{code!.Trim()} ({name!.Trim()})";
        }

        if (hasCode)
        {
            return code!.Trim();
        }

        if (hasName)
        {
            return name!.Trim();
        }

        return null;
    }
}
