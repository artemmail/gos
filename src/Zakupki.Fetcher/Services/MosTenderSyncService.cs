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
        var latestDate = await _dbContext.Notices
            .Where(n => n.Source == NoticeSource.Mos)
            .OrderByDescending(n => n.PublishDate)
            .Select(n => n.PublishDate)
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

                var exists = await _dbContext.Notices
                    .AnyAsync(
                        n => n.PurchaseNumber == registerNumber && n.Source == NoticeSource.Mos,
                        cancellationToken);

                if (exists)
                {
                    continue;
                }

                var notice = MapNotice(registerNumber, item);
                await LinkCompanyAsync(notice, item.company, cancellationToken);
                _dbContext.Notices.Add(notice);
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

    public async Task<bool> EnsureNoticeDetailsLoadedAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        var notice = await _dbContext.Notices
            .Include(n => n.Versions.Where(v => v.IsActive))
                .ThenInclude(v => v.Attachments)
            .FirstOrDefaultAsync(n => n.Id == noticeId, cancellationToken);

        if (notice == null || notice.Source != NoticeSource.Mos)
        {
            return false;
        }

        var activeVersion = notice.Versions.FirstOrDefault();
        if (activeVersion == null)
        {
            return false;
        }

        if (activeVersion.Attachments.Any())
        {
            return false;
        }

        if (!int.TryParse(notice.PurchaseNumber, out var auctionId))
        {
            _logger.LogWarning("Unable to parse MOS purchase number {PurchaseNumber}", notice.PurchaseNumber);
            return false;
        }

        var details = await _mosClient.AuctionGetAsync(new GetQuery { id = auctionId }, cancellationToken);
        if (details == null)
        {
            _logger.LogWarning("Unable to fetch MOS tender details for {PurchaseNumber}", notice.PurchaseNumber);
            return false;
        }

        var now = DateTime.UtcNow;
        UpdateNoticeFromDetails(notice, activeVersion, details, now);
        activeVersion.Attachments = MapAttachments(details, activeVersion.Id, now);
        await LinkCompanyAsync(notice, details.company, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Notice MapNotice(string registerNumber, SearchQueryListDto3 item)
    {
        var now = DateTime.UtcNow;
        var noticeId = Guid.NewGuid();
        var rawDetails = JsonSerializer.Serialize(item);

        var notice = new Notice
        {
            Id = noticeId,
            Source = NoticeSource.Mos,
            Region = ResolveRegion(null),
            PurchaseNumber = registerNumber,
            PublishDate = item.beginDate?.UtcDateTime,
            PurchaseObjectInfo = item.name,
            MaxPrice = (decimal?)item.startPrice,
            FederalLaw = (int?)item.federalLaw,
            RawJson = rawDetails,
            CollectingEnd = item.endDate?.UtcDateTime
        };

        var version = new NoticeVersion
        {
            Id = Guid.NewGuid(),
            NoticeId = noticeId,
            ExternalId = registerNumber,
            VersionNumber = 1,
            IsActive = true,
            VersionReceivedAt = now,
            RawJson = rawDetails,
            InsertedAt = now,
            LastSeenAt = now
        };

        notice.Versions.Add(version);

        return notice;
    }

    private static void UpdateNoticeFromDetails(Notice notice, NoticeVersion version, GetQueryDataDto details, DateTime now)
    {
        notice.Region = ResolveRegion(details.regionId);
        notice.PublishDate = details.beginDate?.UtcDateTime ?? notice.PublishDate;
        notice.PurchaseObjectInfo = details.name ?? notice.PurchaseObjectInfo;
        notice.MaxPrice = (decimal?)details.startPrice ?? notice.MaxPrice;
        notice.FederalLaw = details.federalLaw.HasValue ? (int?)details.federalLaw.Value : notice.FederalLaw;
        notice.Okpd2Code = details.items?.Select(i => i.okpdCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))
            ?? notice.Okpd2Code;
        notice.RawJson = JsonSerializer.Serialize(details);
        notice.CollectingEnd = details.endDate?.UtcDateTime ?? notice.CollectingEnd;

        version.RawJson = notice.RawJson;
        version.LastSeenAt = now;
    }

    private static List<NoticeAttachment> MapAttachments(GetQueryDataDto details, Guid noticeVersionId, DateTime now)
    {
        if (details.attachments == null || details.attachments.Count == 0)
        {
            return new List<NoticeAttachment>();
        }

        return details.attachments
            .Select(a => new NoticeAttachment
            {
                Id = Guid.NewGuid(),
                NoticeVersionId = noticeVersionId,
                PublishedContentId = a.publishedContentId ?? string.Empty,
                FileName = a.fileName ?? string.Empty,
                FileSize = a.fileSize,
                Description = a.description,
                DocumentDate = a.documentDate?.UtcDateTime,
                DocumentKindCode = a.documentKindCode,
                DocumentKindName = a.documentKindName,
                Url = a.url,
                ContentHash = a.contentHash,
                InsertedAt = now,
                LastSeenAt = now
            })
            .ToList();
    }

    private static byte ResolveRegion(int? regionId)
    {
        if (regionId.HasValue && regionId.Value is >= byte.MinValue and <= byte.MaxValue)
        {
            return (byte)regionId.Value;
        }

        return 77;
    }

    private async Task LinkCompanyAsync(
        Notice notice,
        SearchQueryCompanyDto? company,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(company?.inn))
        {
            return;
        }

        var inn = company.inn.Trim();
        var companyEntity = await _dbContext.Companies
            .FirstOrDefaultAsync(c => c.Inn == inn, cancellationToken);

        if (companyEntity is null)
        {
            companyEntity = new Company
            {
                Id = Guid.NewGuid(),
                Inn = inn,
                Region = notice.Region
            };
            _dbContext.Companies.Add(companyEntity);
        }

        if (!string.IsNullOrWhiteSpace(company.name) && string.IsNullOrWhiteSpace(companyEntity.Name))
        {
            companyEntity.Name = company.name;
        }

        notice.CompanyId = companyEntity.Id;
        notice.Company = companyEntity;
    }
}
