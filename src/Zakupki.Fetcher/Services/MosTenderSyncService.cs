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

                var notice = MapNotice(registerNumber, item);
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

    private static MosNotice MapNotice(string registerNumber, SearchQueryListDto3 item)
    {
        return new MosNotice
        {
            Id = Guid.NewGuid(),
            ExternalId = item.id ?? 0,
            RegisterNumber = registerNumber,
            RegistrationNumber = registerNumber,
            Name = item.name,
            RegistrationDate = item.beginDate,
            SummingUpDate = item.endDate,
            EndFillingDate = item.endDate,
            PlanDate = item.endDate,
            InitialSum = item.startPrice,
            StateId = (int?)item.status,
            StateName = item.status?.ToString(),
            FederalLawName = item.federalLaw?.ToString(),
            CustomerInn = item.company?.inn,
            CustomerName = item.company?.name,
            RawJson = JsonSerializer.Serialize(item),
            InsertedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
    }
}
