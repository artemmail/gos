using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeEmbeddingService : INoticeEmbeddingService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly ILogger<NoticeEmbeddingService> _logger;

    public NoticeEmbeddingService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        ILogger<NoticeEmbeddingService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
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

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notice = await context.Notices
            .Where(n => n.Id == noticeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (notice == null)
        {
            _logger.LogWarning("Notice {NoticeId} not found for embedding update", noticeId);
            return;
        }

        notice.Vector = new SqlVector<float>(result.Vector.Select(v => (float)v).ToArray());

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Stored notice embedding for notice {NoticeId}", noticeId);
    }
}
