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
            .AsNoTracking()
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
                publishDate = new DateTimeFilter { start = since, end = now }
            },
            order = new List<OrderDto>
            {
                new() { field = "PublishDate", desc = true }
            },
            skip = 0,
            take = pageSize,
            withCount = true
        };

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _mosClient.AuctionSearchAsync(query, cancellationToken);
            var items = response?.items;
            if (items == null || items.Count == 0)
                break;

            // Batch check existing (avoid N+1)
            var pageRegisterNumbers = items
                .Select(i => i.id?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var existing = await _dbContext.Notices
                .AsNoTracking()
                .Where(n => n.Source == NoticeSource.Mos && pageRegisterNumbers.Contains(n.PurchaseNumber))
                .Select(n => n.PurchaseNumber)
                .ToListAsync(cancellationToken);

            var existingSet = existing.ToHashSet(StringComparer.Ordinal);

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var registerNumber = item.id?.ToString();
                if (string.IsNullOrWhiteSpace(registerNumber))
                    continue;

                if (existingSet.Contains(registerNumber))
                    continue;

                if (!int.TryParse(registerNumber, out var auctionId))
                {
                    _logger.LogWarning("Unable to parse MOS auction id from item.id={Id}", registerNumber);
                    continue;
                }

                // Load details immediately
                var details = await FetchUndocumentedAuctionAsync(auctionId, cancellationToken);

                var notice = MapNotice(registerNumber, item, details);

                // company: prefer customer from details
                await LinkCompanyAsync(
                    notice,
                    ConvertCompany(details?.customer) ?? item.company,
                    cancellationToken);

                _dbContext.Notices.Add(notice);
                created++;
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsCompanyInnConflict(ex))
            {
                _logger.LogWarning(ex, "Duplicate company INN during MOS sync, attempting to re-link notices.");
                await ResolveCompanyConflictsAsync(cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Another worker inserted the same purchases concurrently
                _logger.LogWarning(ex, "Unique violation during MOS sync, detaching conflicted graph and retrying.");
                await DetachAlreadyExistingMosNoticesAsync(cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var totalAvailable = response.count ?? 0;
            if (items.Count < pageSize || (query.skip ?? 0) + pageSize >= totalAvailable)
                break;

            query.skip = (query.skip ?? 0) + pageSize;
        }

        _logger.LogInformation("Synced {Count} new MOS tenders", created);
        return created;
    }

    /// <summary>
    /// Lightweight loader (no tracked-graph merge, no DbUpdateConcurrencyException retries).
    /// Updates Notice/Version via ExecuteUpdateAsync and inserts attachments; duplicates treated as success.
    /// </summary>
    public async Task<bool> EnsureNoticeDetailsLoadedAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        var noticeRow = await _dbContext.Notices
            .AsNoTracking()
            .Where(n => n.Id == noticeId && n.Source == NoticeSource.Mos)
            .Select(n => new { n.PurchaseNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (noticeRow == null)
            return false;

        var activeVersionId = await _dbContext.NoticeVersions
            .AsNoTracking()
            .Where(v => v.NoticeId == noticeId && v.IsActive)
            .Select(v => v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeVersionId == Guid.Empty)
            return false;

        var alreadyHas = await _dbContext.NoticeAttachments
            .AsNoTracking()
            .AnyAsync(a => a.NoticeVersionId == activeVersionId, cancellationToken);

        if (alreadyHas)
            return false;

        if (!int.TryParse(noticeRow.PurchaseNumber, out var auctionId))
        {
            _logger.LogWarning("Unable to parse MOS purchase number {PurchaseNumber}", noticeRow.PurchaseNumber);
            return false;
        }

        var details = await FetchUndocumentedAuctionAsync(auctionId, cancellationToken);
        if (details == null)
        {
            _logger.LogWarning("Unable to fetch MOS tender details for {PurchaseNumber}", noticeRow.PurchaseNumber);
            return false;
        }

        var now = DateTime.UtcNow;
        var raw = JsonSerializer.Serialize(details);

        var firstAuctionRegion = details.auctionRegion?.FirstOrDefault();
        var resolvedRegion = ResolveRegion(firstAuctionRegion?.id);

        // Update Notice without tracking => avoid concurrency exceptions
        await _dbContext.Notices
            .Where(n => n.Id == noticeId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.Region, _ => resolvedRegion)
                    .SetProperty(n => n.PublishDate, n => ParseRussianDateTime(details.startDate) ?? n.PublishDate)
                    .SetProperty(n => n.PurchaseObjectInfo, n => details.name ?? n.PurchaseObjectInfo)
                    .SetProperty(n => n.MaxPrice, n => (decimal?)details.startCost ?? n.MaxPrice)
                    .SetProperty(n => n.RawJson, _ => raw)
                    .SetProperty(n => n.CollectingEnd, n => ParseRussianDateTime(details.endDate) ?? n.CollectingEnd),
                cancellationToken);

        await _dbContext.NoticeVersions
            .Where(v => v.Id == activeVersionId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.RawJson, raw)
                    .SetProperty(v => v.LastSeenAt, now),
                cancellationToken);

        // Link company (needs tracked Notice, but tiny scope)
        var noticeTracked = await _dbContext.Notices.FirstOrDefaultAsync(n => n.Id == noticeId, cancellationToken);
        if (noticeTracked != null)
        {
            await LinkCompanyAsync(noticeTracked, ConvertCompany(details.customer), cancellationToken);
        }

        var attachments = MapAttachmentsFromUndocumented(details, activeVersionId, now)
            .GroupBy(a => a.PublishedContentId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (attachments.Count == 0)
        {
            _logger.LogWarning("No MOS attachments were mapped for notice {PurchaseNumber}", noticeRow.PurchaseNumber);
            return false;
        }

        _dbContext.NoticeAttachments.AddRange(attachments);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another process inserted attachments concurrently => treat as success if any exist now
            var existsNow = await _dbContext.NoticeAttachments
                .AsNoTracking()
                .AnyAsync(a => a.NoticeVersionId == activeVersionId, cancellationToken);

            return existsNow;
        }
    }

    // ============================
    // Mapping
    // ============================

    private static Notice MapNotice(string registerNumber, SearchQueryListDto3 item, UndocumentedAuctionDto? details)
    {
        var now = DateTime.UtcNow;
        var noticeId = Guid.NewGuid();

        var raw = details != null
            ? JsonSerializer.Serialize(details)
            : JsonSerializer.Serialize(item);

        var notice = new Notice
        {
            Id = noticeId,
            Source = NoticeSource.Mos,
            Region = ResolveRegion(details?.auctionRegion?.FirstOrDefault()?.id),
            PurchaseNumber = registerNumber,
            PublishDate = details != null
                ? ParseRussianDateTime(details.startDate) ?? item.beginDate?.UtcDateTime
                : item.beginDate?.UtcDateTime,
            PurchaseObjectInfo = details?.name ?? item.name,
            MaxPrice = details != null ? (decimal?)details.startCost : (decimal?)item.startPrice,
            FederalLaw = (int?)item.federalLaw,
            RawJson = raw,
            CollectingEnd = details != null
                ? ParseRussianDateTime(details.endDate) ?? item.endDate?.UtcDateTime
                : item.endDate?.UtcDateTime,
            Versions = new List<NoticeVersion>()
        };

        var version = new NoticeVersion
        {
            Id = Guid.NewGuid(),
            NoticeId = noticeId,
            ExternalId = registerNumber,
            VersionNumber = 1,
            IsActive = true,
            VersionReceivedAt = now,
            RawJson = raw,
            InsertedAt = now,
            LastSeenAt = now,
            Attachments = new List<NoticeAttachment>()
        };

        if (details != null)
        {
            var attachments = MapAttachmentsFromUndocumented(details, version.Id, now)
                .GroupBy(a => a.PublishedContentId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (attachments.Count > 0)
            {
                foreach (var attachment in attachments)
                    version.Attachments.Add(attachment);
            }
        }

        notice.Versions.Add(version);
        return notice;
    }

    private static List<NoticeAttachment> MapAttachmentsFromUndocumented(
        UndocumentedAuctionDto undocumentedDetails,
        Guid noticeVersionId,
        DateTime now)
    {
        return undocumentedDetails.files?
            .Select((f, index) => new NoticeAttachment
            {
                Id = Guid.NewGuid(),
                NoticeVersionId = noticeVersionId,
                PublishedContentId = !string.IsNullOrWhiteSpace(f.id?.ToString())
                    ? f.id!.ToString()!
                    : string.IsNullOrWhiteSpace(f.name)
                        ? $"auto-{noticeVersionId:N}-{index}"
                        : f.name!,
                FileName = f.name ?? string.Empty,
                Url = string.IsNullOrWhiteSpace(f.id?.ToString())
                    ? null
                    : $"https://zakupki.mos.ru/newapi/api/FileStorage/Download?id={f.id}",
                InsertedAt = now,
                LastSeenAt = now
            })
            .ToList()
            ?? new List<NoticeAttachment>();
    }

    // ============================
    // HTTP undocumented details
    // ============================

    private async Task<UndocumentedAuctionDto?> FetchUndocumentedAuctionAsync(int auctionId, CancellationToken cancellationToken)
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

    // ============================
    // Company mapping/linking (your logic)
    // ============================

    private static SearchQueryCompanyDto? ConvertCompany(UndocumentedCompanyDto? company)
    {
        if (company == null)
            return null;

        return new SearchQueryCompanyDto
        {
            inn = company.inn,
            name = company.name,
            id = company.id
        };
    }

    private async Task LinkCompanyAsync(Notice notice, SearchQueryCompanyDto? company, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(company?.inn))
            return;

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
            companyEntity.Name = company.name;

        if (companyEntity.Region == default)
            companyEntity.Region = notice.Region;

        notice.CompanyId = companyEntity.Id;
        notice.Company = companyEntity;
    }

    // ============================
    // Date/region helpers
    // ============================

    private static DateTime? ParseRussianDateTime(string? value)
    {
        var offset = ParseRussianDateTimeOffset(value);
        return offset?.UtcDateTime;
    }

    private static DateTimeOffset? ParseRussianDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

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
            return (byte)regionId.Value;

        return 77;
    }

    // ============================
    // Conflict detection / recovery
    // ============================

    private static bool IsCompanyInnConflict(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UX_Companies_Inn", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null)
            return false;

        // Try read SqlException.Number via reflection (no direct dependency)
        var prop = inner.GetType().GetProperty("Number");
        if (prop?.PropertyType == typeof(int))
        {
            var number = (int)(prop.GetValue(inner) ?? 0);
            return number is 2601 or 2627; // SQL Server unique constraint/index
        }

        var msg = inner.Message ?? string.Empty;
        return msg.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DetachAlreadyExistingMosNoticesAsync(CancellationToken cancellationToken)
    {
        var addedPurchaseNumbers = _dbContext.ChangeTracker.Entries<Notice>()
            .Where(e => e.State == EntityState.Added && e.Entity.Source == NoticeSource.Mos)
            .Select(e => e.Entity.PurchaseNumber)
            .Where(pn => !string.IsNullOrWhiteSpace(pn))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (addedPurchaseNumbers.Count == 0)
            return;

        var existing = await _dbContext.Notices
            .AsNoTracking()
            .Where(n => n.Source == NoticeSource.Mos && addedPurchaseNumbers.Contains(n.PurchaseNumber))
            .Select(n => n.PurchaseNumber)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var entry in _dbContext.ChangeTracker.Entries<Notice>().Where(e => e.State == EntityState.Added).ToList())
        {
            if (entry.Entity.Source != NoticeSource.Mos)
                continue;

            if (!existingSet.Contains(entry.Entity.PurchaseNumber))
                continue;

            entry.State = EntityState.Detached;
        }

        foreach (var v in _dbContext.ChangeTracker.Entries<NoticeVersion>().Where(e => e.State == EntityState.Added).ToList())
            v.State = EntityState.Detached;

        foreach (var a in _dbContext.ChangeTracker.Entries<NoticeAttachment>().Where(e => e.State == EntityState.Added).ToList())
            a.State = EntityState.Detached;
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
                continue;

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

// ============================
// Undocumented DTOs
// ============================

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
