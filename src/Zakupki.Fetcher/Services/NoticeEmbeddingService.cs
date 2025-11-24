using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeEmbeddingService : INoticeEmbeddingService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly NoticeEmbeddingOptions _options;
    private readonly ILogger<NoticeEmbeddingService> _logger;

    public NoticeEmbeddingService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IOptions<NoticeEmbeddingOptions> options,
        ILogger<NoticeEmbeddingService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ApplyVectorAsync(QueryVectorResult result, CancellationToken cancellationToken)
    {
        if (result.Vector == null || result.Vector.Count == 0)
        {
            _logger.LogWarning("Received empty vector for notice embedding {Id}", result.Id);
            return;
        }

        if (!Guid.TryParse(result.UserId, out var noticeId))
        {
            _logger.LogWarning(
                "Skipping notice embedding {Id} because notice id is missing or invalid: {UserId}",
                result.Id,
                result.UserId);
            return;
        }

        var embeddingId = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
        var vector = new SqlVector<float>(result.Vector.Select(v => (float)v).ToArray());

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var embedding = await context.NoticeEmbeddings
            .Where(e => e.Id == embeddingId || (e.NoticeId == noticeId && e.Source == _options.Source))
            .FirstOrDefaultAsync(cancellationToken);

        if (embedding == null)
        {
            embedding = new NoticeEmbedding
            {
                Id = embeddingId,
                NoticeId = noticeId,
                Source = _options.Source,
                Vector = vector
            };

            context.NoticeEmbeddings.Add(embedding);
        }
        else
        {
            embedding.Vector = vector;
            embedding.Source = _options.Source;
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Stored notice embedding for notice {NoticeId} with id {EmbeddingId}", noticeId, embedding.Id);
    }
}
