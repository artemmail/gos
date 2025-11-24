using System;
using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<Guid, PendingQueryVectorRequest> PendingRequests = new();

    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(3);

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

        if (!TryGetQueueName(out _))
        {
            throw new InvalidOperationException("Очередь для генерации вектора не настроена");
        }

        var requestId = Guid.NewGuid();
        var pendingRequest = new PendingQueryVectorRequest(requestId, userId, trimmedQuery);
        PendingRequests.TryAdd(requestId, pendingRequest);

        var batch = new QueryVectorBatchRequest
        {
            ServiceId = GetServiceId(),
            Items = new[]
            {
                new QueryVectorRequestItem
                {
                    Id = requestId,
                    UserId = userId,
                    String = trimmedQuery
                }
            }
        };

        await _eventBusPublisher.PublishQueryVectorRequestAsync(batch, cancellationToken);

        var completedTask = await Task.WhenAny(pendingRequest.Completion.Task, Task.Delay(ResponseTimeout, cancellationToken));

        cancellationToken.ThrowIfCancellationRequested();

        if (completedTask == pendingRequest.Completion.Task)
        {
            return await pendingRequest.Completion.Task;
        }

        throw new QueryVectorPendingException(requestId);
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
            CompleteWithFailure(result.Id, new InvalidOperationException("Vector result is empty"));
            return;
        }

        PendingRequests.TryGetValue(result.Id, out var pendingRequest);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.UserQueryVectors
            .Where(q => q.Id == result.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            var userId = pendingRequest?.UserId ?? result.UserId;
            var query = pendingRequest?.Query ?? result.Query;

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(query))
            {
                CompleteWithFailure(result.Id, new InvalidOperationException("Не удалось определить пользователя или текст запроса для вектора"));
                return;
            }

            entity = new UserQueryVector
            {
                Id = result.Id,
                UserId = userId,
                Query = query.Trim(),
                CreatedAt = pendingRequest?.CreatedAt ?? DateTime.UtcNow
            };

            context.UserQueryVectors.Add(entity);
        }

        var vector = result.Vector?.Select(v => (float)v).ToArray();
        entity.Vector = vector != null ? new SqlVector<float>(vector) : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.CompletedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        if (pendingRequest != null)
        {
            pendingRequest.Completion.TrySetResult(entity);
            PendingRequests.TryRemove(result.Id, out _);
        }
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

    private string GetServiceId()
    {
        return string.IsNullOrWhiteSpace(_vectorOptions.ServiceId)
            ? "AddUserSemanticReq"
            : _vectorOptions.ServiceId;
    }

    private static void CompleteWithFailure(Guid id, Exception ex)
    {
        if (PendingRequests.TryRemove(id, out var pending))
        {
            pending.Completion.TrySetException(ex);
        }
    }

    private sealed class PendingQueryVectorRequest
    {
        public PendingQueryVectorRequest(Guid id, string userId, string query)
        {
            Id = id;
            UserId = userId;
            Query = query;
            CreatedAt = DateTime.UtcNow;
            Completion = new TaskCompletionSource<UserQueryVector>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Guid Id { get; }

        public string UserId { get; }

        public string Query { get; }

        public DateTime CreatedAt { get; }

        public TaskCompletionSource<UserQueryVector> Completion { get; }
    }
}
