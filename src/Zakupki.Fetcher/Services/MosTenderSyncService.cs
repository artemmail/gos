using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Options;
using Zakupki.MosApi.V2;
using DateTime = System.DateTime;
using Guid = System.Guid;
using DateTimeFilter = Zakupki.MosApi.V2.DateTime2;

namespace Zakupki.Fetcher.Services;

public class MosTenderSyncService
{
    private readonly NoticeDbContext _dbContext;
    private readonly MosSwaggerClientV2 _mosClient;
    private readonly ILogger<MosTenderSyncService> _logger;
    private readonly IOptionsMonitor<MosApiOptions> _optionsMonitor;

    public MosTenderSyncService(
        NoticeDbContext dbContext,
        MosSwaggerClientV2 mosClient,
        ILogger<MosTenderSyncService> logger,
        IOptionsMonitor<MosApiOptions> optionsMonitor)
    {
        _dbContext = dbContext;
        _mosClient = mosClient;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<int> SyncAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.Token) || string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogWarning("Mos API is not configured. Set MosApi:BaseUrl and MosApi:Token to enable sync.");
            return 0;
        }

        _mosClient.ApiToken = options.Token;
        var now = DateTimeOffset.UtcNow;
        var latestDate = await _dbContext.MosNotices
            .OrderByDescending(n => n.RegistrationDate)
            .Select(n => n.RegistrationDate)
            .FirstOrDefaultAsync(cancellationToken);

        var since = latestDate ?? now.AddDays(-options.LookbackDays);
        _logger.LogInformation("Syncing MOS tenders from {Since} to {Now}", since, now);

        var pageSize = Math.Max(1, options.PageSize);
        var created = 0;

        var query = new SearchQuery
        {
            filter = new SearchQueryFilterDto
            {
                publishDate = new DateTimeFilter
                {
                    start = since,
                    end = now
                }
            },
            order = new List<OrderDto>
            {
                new()
                {
                    field = "PublishDate",
                    desc = true
                }
            },
            skip = 0,
            take = pageSize,
            withCount = true
        };

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _mosClient.AuctionSearchAsync(query, cancellationToken);
            if (response?.items == null || response.items.Count == 0)
            {
                break;
            }

            foreach (var item in response.items)
            {
                var registerNumber = item.id?.ToString();
                if (string.IsNullOrWhiteSpace(registerNumber))
                {
                    continue;
                }

                var exists = await _dbContext.MosNotices
                    .AnyAsync(n => n.RegisterNumber == registerNumber, cancellationToken);

                if (exists)
                {
                    continue;
                }

                var details = await _mosClient.AuctionGetAsync(
                    new GetQuery { id = item.id ?? 0 },
                    cancellationToken);

                if (details == null)
                {
                    _logger.LogWarning(
                        "Unable to fetch MOS tender details for {RegisterNumber}",
                        registerNumber);
                    continue;
                }

                var notice = MapNotice(registerNumber, item, details);
                _dbContext.MosNotices.Add(notice);
                created++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var totalAvailable = response.count ?? 0;
            if (response.items.Count < pageSize || (query.skip ?? 0) + pageSize >= totalAvailable)
            {
                break;
            }

            query.skip = (query.skip ?? 0) + pageSize;
        }

        _logger.LogInformation("Synced {Count} new MOS tenders", created);
        return created;
    }

    private static MosNotice MapNotice(string registerNumber, SearchQueryListDto3 item, GetQueryDataDto details)
    {
        var now = DateTime.UtcNow;
        var noticeId = Guid.NewGuid();

        return new MosNotice
        {
            Id = noticeId,
            ExternalId = details.id ?? item.id ?? 0,
            RegisterNumber = registerNumber,
            RegistrationNumber = registerNumber,
            Name = details.name ?? item.name,
            RegistrationDate = details.beginDate ?? item.beginDate,
            SummingUpDate = details.endDate ?? item.endDate,
            EndFillingDate = details.endDate ?? item.endDate,
            PlanDate = details.endDate ?? item.endDate,
            InitialSum = details.startPrice ?? item.startPrice,
            StateId = (int?)details.status ?? (int?)item.status,
            StateName = details.status?.ToString() ?? item.status?.ToString(),
            FederalLawName = details.federalLaw?.ToString() ?? item.federalLaw?.ToString(),
            CustomerInn = details.company?.inn ?? item.company?.inn,
            CustomerName = details.company?.name ?? item.company?.name,
            RawJson = JsonSerializer.Serialize(details),
            InsertedAt = now,
            LastUpdatedAt = now,
            Attachments = MapAttachments(details, noticeId)
        };
    }

    private static List<MosNoticeAttachment> MapAttachments(GetQueryDataDto details, Guid noticeId)
    {
        if (details.attachments == null || details.attachments.Count == 0)
        {
            return new List<MosNoticeAttachment>();
        }

        var now = DateTime.UtcNow;

        return details.attachments
            .Select(a => new MosNoticeAttachment
            {
                Id = Guid.NewGuid(),
                MosNoticeId = noticeId,
                PublishedContentId = a.publishedContentId,
                FileName = a.fileName ?? string.Empty,
                FileSize = a.fileSize,
                Description = a.description,
                DocumentDate = a.documentDate?.start?.UtcDateTime
                    ?? a.documentDate?.end?.UtcDateTime,
                DocumentKindCode = a.documentKindCode,
                DocumentKindName = a.documentKindName,
                Url = a.url,
                ContentHash = a.contentHash,
                InsertedAt = now,
                LastSeenAt = now
            })
            .ToList();
    }
}
