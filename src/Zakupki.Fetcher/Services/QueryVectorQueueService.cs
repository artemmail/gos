using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QueryVectorOptions _vectorOptions;

    public QueryVectorQueueService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IEventBusPublisher eventBusPublisher,
        IOptions<EventBusOptions> options,
        IHttpClientFactory httpClientFactory,
        IOptions<QueryVectorOptions> vectorOptions)
    {
        _dbContextFactory = dbContextFactory;
        _eventBusPublisher = eventBusPublisher;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
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

        var vectorizerUrl = _vectorOptions.VectorizerUrl;
        if (!string.IsNullOrWhiteSpace(vectorizerUrl))
        {
            var vector = await RequestVectorAsync(vectorizerUrl, entity.Id, trimmedQuery, cancellationToken);
            if (vector?.Length > 0)
            {
                entity.Vector = new SqlVector<float>(vector);
                entity.UpdatedAt = DateTime.UtcNow;
                entity.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return entity;
            }

            throw new InvalidOperationException("Сервис векторизации вернул пустой ответ");
        }

        if (!IsQueueConfigured())
        {
            throw new InvalidOperationException("Очередь для генерации вектора не настроена");
        }

        var command = new QueryVectorCommand
        {
            Id = entity.Id,
            Query = entity.Query
        };

        await _eventBusPublisher.PublishQueryVectorRequestAsync(command, cancellationToken);

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

    private bool IsQueueConfigured()
    {
        return _options.Enabled &&
               !string.IsNullOrWhiteSpace(_options.QueryVectorRequestQueueName);
    }

    private async Task<float[]?> RequestVectorAsync(string endpoint, Guid id, string query, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(QueryVectorQueueService));
        var payload = new[] { new VectorRequestDto(id, query) };

        using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Не удалось обратиться к сервису векторизации (HTTP {(int)response.StatusCode})");
        }

        var result = await response.Content.ReadFromJsonAsync<VectorizationResponse>(cancellationToken: cancellationToken);
        if (result?.Results == null || result.Results.Count == 0)
        {
            return null;
        }

        var match = result.Results.FirstOrDefault(r => r.Id == id);
        return match?.Vector?.Select(v => (float)v).ToArray();
    }

    private sealed record VectorRequestDto(Guid Id, string @string);

    private sealed class VectorizationResponse
    {
        public List<VectorizationResult>? Results { get; init; }
    }

    private sealed class VectorizationResult
    {
        public Guid Id { get; init; }
        public string? String { get; init; }
        public IReadOnlyList<double>? Vector { get; init; }
    }
}
