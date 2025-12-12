using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    private static readonly JsonSerializerOptions UndocumentedSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly NoticeDbContext _dbContext;
    private readonly MosSwaggerClientV2 _mosClient;
    private readonly ILogger<MosTenderSyncService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<MosApiOptions> _optionsMonitor;

    public MosTenderSyncService(
        NoticeDbContext dbContext,
        MosSwaggerClientV2 mosClient,
        ILogger<MosTenderSyncService> logger,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<MosApiOptions> optionsMonitor)
    {
        _dbContext = dbContext;
        _mosClient = mosClient;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsCompanyInnConflict(ex))
            {
                _logger.LogWarning(ex, "Detected duplicate company INN during MOS sync, attempting to re-link notices.");
                await ResolveCompanyConflictsAsync(cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

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

        var undocumentedDetails = await FetchUndocumentedAuctionAsync(auctionId, cancellationToken);
        if (undocumentedDetails == null)
        {
            _logger.LogWarning("Unable to fetch MOS tender details for {PurchaseNumber}", notice.PurchaseNumber);
            return false;
        }

        var now = DateTime.UtcNow;
        UpdateNoticeFromUndocumentedDetails(notice, activeVersion, undocumentedDetails, now);

        activeVersion.Attachments = MapAttachmentsFromUndocumented(undocumentedDetails, activeVersion.Id, now);
        await LinkCompanyAsync(notice, ConvertCompany(undocumentedDetails.customer), cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Notice {PurchaseNumber} was updated concurrently while fetching MOS tender details",
                notice.PurchaseNumber);
            return false;
        }

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

    private static List<NoticeAttachment> MapAttachmentsFromUndocumented(
        UndocumentedAuctionDto undocumentedDetails,
        Guid noticeVersionId,
        DateTime now)
    {
        var attachments = undocumentedDetails.files
            ?.Select(f => new NoticeAttachment
            {
                Id = Guid.NewGuid(),
                NoticeVersionId = noticeVersionId,
                PublishedContentId = f.id?.ToString() ?? string.Empty,
                FileName = f.name ?? string.Empty,
                Url = string.IsNullOrWhiteSpace(f.id?.ToString())
                    ? null
                    : $"https://zakupki.mos.ru/newapi/api/FileStorage/Download?id={f.id}",
                InsertedAt = now,
                LastSeenAt = now
            })
            .ToList();

        return attachments ?? new List<NoticeAttachment>();
    }

    private async Task<UndocumentedAuctionDto?> FetchUndocumentedAuctionAsync(
        int auctionId,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var auctionUrl = $"https://zakupki.mos.ru/newapi/api/Auction/Get?auctionId={auctionId}";

        try
        {
            using var response = await client.GetAsync(auctionUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Undocumented MOS auction API returned status code {StatusCode} for {AuctionId}",
                    response.StatusCode,
                    auctionId);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<UndocumentedAuctionDto>(
                stream,
                UndocumentedSerializerOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch undocumented MOS auction {AuctionId}", auctionId);
            return null;
        }
    }

    private static SearchQueryCompanyDto? ConvertCompany(UndocumentedCompanyDto? company)
    {
        if (company == null)
        {
            return null;
        }

        return new SearchQueryCompanyDto
        {
            inn = company.inn,
            name = company.name,
            id = company.id
        };
    }

    private static void UpdateNoticeFromUndocumentedDetails(
        Notice notice,
        NoticeVersion version,
        UndocumentedAuctionDto details,
        DateTime now)
    {
        notice.Region = ResolveRegion(details.auctionRegion?.FirstOrDefault()?.id);
        notice.PublishDate = ParseRussianDateTime(details.startDate) ?? notice.PublishDate;
        notice.PurchaseObjectInfo = details.name ?? notice.PurchaseObjectInfo;
        notice.MaxPrice = (decimal?)details.startCost ?? notice.MaxPrice;
        notice.RawJson = JsonSerializer.Serialize(details);
        notice.CollectingEnd = ParseRussianDateTime(details.endDate) ?? notice.CollectingEnd;

        version.RawJson = notice.RawJson;
        version.LastSeenAt = now;
    }

    private static DateTime? ParseRussianDateTime(string? value)
    {
        var offset = ParseRussianDateTimeOffset(value);
        return offset?.UtcDateTime;
    }

    private static DateTimeOffset? ParseRussianDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "dd.MM.yyyy HH:mm:ss" };
        if (DateTimeOffset.TryParseExact(
                value,
                formats,
                CultureInfo.GetCultureInfo("ru-RU"),
                DateTimeStyles.AssumeLocal,
                out var result))
        {
            return result.ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out result))
        {
            return result.ToUniversalTime();
        }

        return null;
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

        var companyEntity = _dbContext.Companies.Local.FirstOrDefault(c => c.Inn == inn)
            ?? await _dbContext.Companies.FirstOrDefaultAsync(c => c.Inn == inn, cancellationToken);

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

        if (companyEntity.Region == default)
        {
            companyEntity.Region = notice.Region;
        }

        notice.CompanyId = companyEntity.Id;
        notice.Company = companyEntity;
    }

    private static bool IsCompanyInnConflict(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("UX_Companies_Inn", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task ResolveCompanyConflictsAsync(CancellationToken cancellationToken)
    {
        var addedCompanies = _dbContext.ChangeTracker.Entries<Company>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        foreach (var entry in addedCompanies)
        {
            var inn = entry.Entity.Inn;
            var existing = await _dbContext.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Inn == inn, cancellationToken);

            if (existing is null)
            {
                continue;
            }

            RelinkNoticesToCompany(entry, existing);
        }
    }

    private void RelinkNoticesToCompany(EntityEntry<Company> newCompanyEntry, Company existingCompany)
    {
        var trackedCompany = _dbContext.Companies.Local.FirstOrDefault(c => c.Id == existingCompany.Id)
            ?? _dbContext.Attach(existingCompany).Entity;

        var newCompanyId = newCompanyEntry.Entity.Id;
        newCompanyEntry.State = EntityState.Detached;

        var noticesToUpdate = _dbContext.ChangeTracker.Entries<Notice>()
            .Where(n => n.Entity.CompanyId == newCompanyId)
            .ToList();

        foreach (var noticeEntry in noticesToUpdate)
        {
            noticeEntry.Entity.CompanyId = trackedCompany.Id;
            noticeEntry.Entity.Company = trackedCompany;
        }
    }
}

public class UndocumentedAuctionDto
{
    [JsonPropertyName("name")]
    public string? name { get; set; }

    [JsonPropertyName("startDate")]
    public string? startDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? endDate { get; set; }

    [JsonPropertyName("startCost")]
    public double? startCost { get; set; }

    [JsonPropertyName("federalLawName")]
    public string? federalLawName { get; set; }

    [JsonPropertyName("auctionRegion")]
    public List<UndocumentedAuctionRegionDto>? auctionRegion { get; set; }

    [JsonPropertyName("files")]
    public List<UndocumentedAuctionFileDto>? files { get; set; }

    [JsonPropertyName("customer")]
    public UndocumentedCompanyDto? customer { get; set; }

    [JsonPropertyName("id")]
    public int? id { get; set; }
}

public class UndocumentedAuctionFileDto
{
    [JsonPropertyName("id")]
    public long? id { get; set; }

    [JsonPropertyName("name")]
    public string? name { get; set; }
}

public class UndocumentedAuctionRegionDto
{
    [JsonPropertyName("id")]
    public int? id { get; set; }
}

public class UndocumentedCompanyDto
{
    [JsonPropertyName("inn")]
    public string? inn { get; set; }

    [JsonPropertyName("name")]
    public string? name { get; set; }

    [JsonPropertyName("id")]
    public int? id { get; set; }
}
