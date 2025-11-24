using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class QueryVectorQueueService : IQueryVectorQueueService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly IEventBusPublisher _eventBusPublisher;
    private readonly EventBusOptions _options;
    private readonly QueryVectorOptions _vectorOptions;

    public QueryVectorQueueService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IEventBusPublisher eventBusPublisher,
        IOptions<EventBusOptions> options,
        IOptions<QueryVectorOptions> vectorOptions)
    {
        _dbContextFactory = dbContextFactory;
        _eventBusPublisher = eventBusPublisher;
        _options = options.Value;
        _vectorOptions = vectorOptions.Value;
    }

    public async Task<UserQueryVector> CreateAsync(string userId, CreateUserQueryVectorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Query is required", nameof(request));
        }

        var trimmedQuery = request.Query.Trim();

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new UserQueryVector
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Query = trimmedQuery,
            CreatedAt = DateTime.UtcNow
        };

        context.UserQueryVectors.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        if (!TryGetQueueName(out _))
        {
            throw new InvalidOperationException("Очередь для генерации вектора не настроена");
        }

        var batch = new QueryVectorBatchRequest
        {
            ServiceId = _vectorOptions.ServiceId,
            Items = new[]
            {
                new QueryVectorRequestItem
                {
                    Id = entity.Id,
                    UserId = userId,
                    String = trimmedQuery
                }
            }
        };

        await _eventBusPublisher.PublishQueryVectorRequestAsync(batch, cancellationToken);

        return entity;
    }

    public async Task<IReadOnlyList<UserQueryVector>> GetAllAsync(string userId, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.UserQueryVectors
            .AsNoTracking()
            .Where(q => q.UserId == userId && q.CompletedAt != null)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.UserQueryVectors
            .Where(q => q.UserId == userId && q.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return false;
        }

        context.UserQueryVectors.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ApplyVectorAsync(QueryVectorResult result, CancellationToken cancellationToken)
    {
        if (result.Vector == null)
        {
            return;
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.UserQueryVectors
            .Where(q => q.Id == result.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return;
        }

        var vector = result.Vector?.Select(v => (float)v).ToArray();
        entity.Vector = vector != null ? new SqlVector<float>(vector) : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.CompletedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    private bool TryGetQueueName(out string queueName)
    {
        queueName = string.Empty;

        if (!_options.Enabled)
        {
            return false;
        }

        queueName = string.IsNullOrWhiteSpace(_options.QueryVectorRequestQueueName)
            ? _options.ResolveCommandQueueName()
            : _options.QueryVectorRequestQueueName;

        return !string.IsNullOrWhiteSpace(queueName);
    }
}
