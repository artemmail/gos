using System;
using System.Collections.Generic;
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

    public async Task ApplyVectorAsync(IReadOnlyList<QueryVectorResult> results, CancellationToken cancellationToken)
    {
        var validResults = results
            .Where(r => r.Vector != null && r.Vector.Count > 0)
        //    .Where(r => Guid.TryParse(r.UserId, out _))
            .ToArray();

        if (validResults.Length == 0)
        {
            return;
        }

        var noticeIds = validResults
            .Select(r => r.Id)
            //.Concat(validResults.Select(r => r.Id))
           
            .ToArray();

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notices = await context.Notices
            .Where(n => noticeIds.Contains(n.Id) && n.Vector == null)
            .ToListAsync(cancellationToken);

        if (notices.Any())
        {

            var dic = validResults.ToDictionary(key => key.Id, value => value.Vector);

            foreach (var notice in notices)
            {
                notice.Vector = new SqlVector<float>(dic[notice.Id]!.Select(v => (float)v).ToArray());
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}



