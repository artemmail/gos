using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FabrikantGrabber.Models;
using FabrikantGrabber.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.EF2020;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Services;

public class FabrikantTenderSyncService
{
    private const string ViewPath = "/v2/trades/procedure/view/";
    private const string DocsPath = "/v2/trades/procedure/documentation/";
    private const string SearchPath = "/procedure/search/purchases";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly NoticeDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FabrikantTenderSyncService> _logger;
    private readonly IOptionsMonitor<FabrikantOptions> _optionsMonitor;
    private readonly ProcedurePageParser _procedurePageParser;
    private readonly DocumentationParser _documentationParser;
    private readonly SearchPageParser _searchPageParser;
    private readonly RegionDeterminationService _regionDeterminationService;

    public FabrikantTenderSyncService(
        NoticeDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<FabrikantTenderSyncService> logger,
        IOptionsMonitor<FabrikantOptions> optionsMonitor,
        ProcedurePageParser procedurePageParser,
        DocumentationParser documentationParser,
        SearchPageParser searchPageParser,
        RegionDeterminationService regionDeterminationService)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _procedurePageParser = procedurePageParser;
        _documentationParser = documentationParser;
        _searchPageParser = searchPageParser;
        _regionDeterminationService = regionDeterminationService;
    }

    public async Task<int> SyncAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        var httpClient = _httpClientFactory.CreateClient("Fabrikant");
        var now = DateTime.UtcNow;
        var since = now.AddDays(-options.LookbackDays);

        _logger.LogInformation(
            "Syncing Fabrikant tenders (base {BaseUrl}) with lookback {LookbackDays} days",
            options.BaseUrl,
            options.LookbackDays);

        var existingPurchaseNumbers = await _dbContext.Notices
            .AsNoTracking()
            .Where(n => n.Source == NoticeSource.Fabrikant)
            .Select(n => n.PurchaseNumber)
            .ToListAsync(cancellationToken);

        var existingSet = existingPurchaseNumbers
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.Ordinal);

        var procedures = await FetchProceduresWithContentAsync(httpClient, options, existingSet, cancellationToken);
        var created = 0;

        foreach (var procedure in procedures)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var purchaseNumber = !string.IsNullOrWhiteSpace(procedure.ProcedureNumber)
                ? procedure.ProcedureNumber
                : procedure.ExternalId;

            if (string.IsNullOrWhiteSpace(purchaseNumber))
                continue;

            if (procedure.PublishDate.HasValue && procedure.PublishDate < since)
                continue;

            if (existingSet.Contains(purchaseNumber))
                continue;

            try
            {
                var notice = await MapNotice(procedure, options, now, cancellationToken);
                _dbContext.Notices.Add(notice);
                existingSet.Add(purchaseNumber);
            }
            catch(Exception e)
            {

            }

            created++;
        }

        if (created == 0)
        {
            _logger.LogInformation("No new Fabrikant tenders found.");
            return 0;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsCompanyInnConflict(ex))
        {
            _logger.LogWarning(ex, "Duplicate company INN during Fabrikant sync, attempting to re-link notices.");
            await ResolveCompanyConflictsAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogWarning(ex, "Unique violation during Fabrikant sync, detaching conflicted graph and retrying.");
            await DetachAlreadyExistingFabrikantNoticesAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch(Exception ex)
        {

        }

        _logger.LogInformation("Synced {Count} new Fabrikant tenders", created);
        return created;
    }

    private async Task<List<FabrikantProcedure>> FetchProceduresWithContentAsync(
        HttpClient httpClient,
        FabrikantOptions options,
        ISet<string> existingPurchaseNumbers,
        CancellationToken cancellationToken)
    {
        var parameters = BuildSearchParameters(options, pageNumber: 1);
        var searchUri = BuildSearchUri(options.BaseUrl, parameters);

        var firstPageHtml = await httpClient.GetStringAsync(searchUri, cancellationToken);
        var searchResult = _searchPageParser.Parse(firstPageHtml, new Uri(options.BaseUrl));

        var pageSize = ExtractPageSize(parameters, searchResult.Procedures.Count, options.PageSize);
        var totalPages = CalculateTotalPages(searchResult.TotalCount, pageSize);
        var pagesToFetch = Math.Min(totalPages, Math.Max(1, options.MaxPages));

        _logger.LogInformation(
            "Found {Total} Fabrikant procedures on page 1 (page size {PageSize}, total pages {TotalPages}, capped to {PagesToFetch})",
            searchResult.TotalCount,
            pageSize,
            totalPages,
            pagesToFetch);

        var procedures = new List<FabrikantSearchItem>(searchResult.Procedures);
        var seen = new HashSet<string>(procedures.Select(p => p.ProcedureId), StringComparer.OrdinalIgnoreCase);

        if (pagesToFetch > 1)
        {
            for (var page = 2; page <= pagesToFetch; page++)
            {
                var pageUrl = BuildPageUrl(searchUri, parameters, page);
                var html = await httpClient.GetStringAsync(pageUrl, cancellationToken);
                var pageResult = _searchPageParser.Parse(html, new Uri(options.BaseUrl));

                foreach (var item in pageResult.Procedures)
                {
                    if (seen.Add(item.ProcedureId))
                        procedures.Add(item);
                }
            }
        }

        var result = new List<FabrikantProcedure>();

        foreach (var procedure in procedures)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var procedureId = procedure.ProcedureId;
            if (existingPurchaseNumbers.Contains(procedureId))
                continue;

            var viewUrl = procedure.Url ?? new Uri(options.BaseUrl + ViewPath + procedureId);
            FabrikantProcedure? parsed = null;

            try
            {
                var html = await httpClient.GetStringAsync(viewUrl, cancellationToken);
                parsed = _procedurePageParser.Parse(html, procedureId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Fabrikant procedure {ProcedureId}", procedureId);
            }

            if (parsed == null)
                continue;

            parsed.ExternalId = procedureId;
            parsed.Title = string.IsNullOrWhiteSpace(parsed.Title) ? procedure.Title : parsed.Title;

            var purchaseNumber = !string.IsNullOrWhiteSpace(parsed.ProcedureNumber)
                ? parsed.ProcedureNumber
                : parsed.ExternalId;

            if (!string.IsNullOrWhiteSpace(purchaseNumber) && existingPurchaseNumbers.Contains(purchaseNumber))
                continue;

            var docsUrl = new Uri(options.BaseUrl + DocsPath + procedureId);
            try
            {
                var docsHtml = await httpClient.GetStringAsync(docsUrl, cancellationToken);
                parsed.Documents = _documentationParser.ParseDocumentationLinks(docsHtml, docsUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Fabrikant documents for {ProcedureId}", procedureId);
            }

            result.Add(parsed);
        }

        return result;
    }

    private async Task<Notice> MapNotice(
        FabrikantProcedure procedure,
        FabrikantOptions options,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var raw = JsonSerializer.Serialize(procedure, SerializerOptions);
        var purchaseNumber = !string.IsNullOrWhiteSpace(procedure.ProcedureNumber)
            ? procedure.ProcedureNumber
            : procedure.ExternalId;



        var okpd =  (procedure.Okpd2??"").Trim();
        string okpd2 = okpd;
        string name = okpd2;
        var i = okpd2.IndexOf(' ');


        

        if (i<16 && i>0)
        {
            okpd2 = okpd.Substring(0, i).Trim();
            name = okpd.Substring(i).Trim();
        }
        else
        {
            okpd2 = "0000";            
        }

        

        var notice = new Notice
        {
            Id = Guid.NewGuid(),
            Source = NoticeSource.Fabrikant,
            ExternalId = procedure.ExternalId,
            PurchaseNumber = purchaseNumber,
            PublishDate = procedure.PublishDate,
            Href = options.BaseUrl.TrimEnd('/') + ViewPath + procedure.ExternalId,
            EtpName = "Фабрикант",
            EtpUrl = options.BaseUrl,
            PurchaseObjectInfo = procedure.Title,
            MaxPrice = procedure.Nmck,
            Okpd2Code = okpd2,
            Okpd2Name = name.Substring(0,Math.Min(510,name.Length)),
            RawJson = raw,
            Hash = HashUtilities.ComputeSha256Hex(Encoding.UTF8.GetBytes(raw)),
            VersionNumber = 1,
            VersionReceivedAt = now,
            InsertedAt = now,
            LastSeenAt = now,
            CollectingEnd = procedure.ApplyEndDate,
            Attachments = new List<NoticeAttachment>(),
            Region = DetermineRegion(procedure, options)
        };

        if (!string.IsNullOrWhiteSpace(procedure.OrganizerInn))
        {
            var normalizedInn = procedure.OrganizerInn.Trim();

            long a;
            if (long.TryParse(normalizedInn, out a))
            {

                var company = _dbContext.Companies.Local.FirstOrDefault(c => c.Inn == normalizedInn)
                              ?? await _dbContext.Companies.FirstOrDefaultAsync(
                                  c => c.Inn == normalizedInn,
                                  cancellationToken);

                if (company is null)
                {
                    company = new Company
                    {
                        Id = Guid.NewGuid(),
                        Inn = normalizedInn,
                        Name = procedure.OrganizerName,
                        Region = notice.Region,
                        Address = procedure.OrganizerAddress
                    };

                    _dbContext.Companies.Add(company);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(company.Name) && !string.IsNullOrWhiteSpace(procedure.OrganizerName))
                        company.Name = procedure.OrganizerName;

                    if (company.Region == default)
                        company.Region = notice.Region;

                    if (!string.IsNullOrWhiteSpace(procedure.OrganizerAddress))
                        company.Address = procedure.OrganizerAddress;
                }

                notice.CompanyId = company.Id;
                notice.Company = company;
            }
        }

        foreach (var attachment in MapAttachments(procedure, notice.Id, now))
            notice.Attachments.Add(attachment);

        return notice;
    }

    private byte DetermineRegion(FabrikantProcedure procedure, FabrikantOptions options)
    {
        var addresses = new[] { procedure.DeliveryAddress, procedure.DeliveryTerm };
        var inns = new[] { procedure.CustomerInn, procedure.OrganizerInn };

        return _regionDeterminationService.DetermineRegionCode(addresses, inns, options.DefaultRegion);
    }

    private static IEnumerable<NoticeAttachment> MapAttachments(FabrikantProcedure procedure, Guid noticeId, DateTime now)
    {
        if (procedure.Documents == null || procedure.Documents.Count == 0)
            return Enumerable.Empty<NoticeAttachment>();

        return procedure.Documents
            .Where(d => d?.Url != null)
            .Select((doc, index) => new NoticeAttachment
            {
                Id = Guid.NewGuid(),
                NoticeId = noticeId,
                PublishedContentId = !string.IsNullOrWhiteSpace(doc.FileName)
                    ? doc.FileName
                    : doc.Url!.ToString(),
                FileName = !string.IsNullOrWhiteSpace(doc.FileName)
                    ? doc.FileName
                    : doc.Url!.Segments.LastOrDefault()?.Trim('/') ?? $"document-{index}",
                Url = doc.Url!.ToString(),
                InsertedAt = now,
                LastSeenAt = now
            })
            .GroupBy(a => a.PublishedContentId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<KeyValuePair<string, string>> BuildSearchParameters(FabrikantOptions options, int pageNumber)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("_rsc", options.Rsc),
            new("page_limit", Math.Max(1, options.PageSize).ToString()),
            new("page_number", pageNumber.ToString())
        };

        foreach (var section in options.SectionIds)
            parameters.Add(new KeyValuePair<string, string>("section_ids[]", section));

        foreach (var status in options.Statuses)
            parameters.Add(new KeyValuePair<string, string>("statuses[]", status));

        return parameters;
    }

    private static Uri BuildSearchUri(string baseUrl, List<KeyValuePair<string, string>> parameters)
    {
        var builder = new UriBuilder(baseUrl)
        {
            Path = SearchPath,
            Query = BuildQueryString(parameters)
        };

        return builder.Uri;
    }

    private static string BuildPageUrl(Uri baseUri, List<KeyValuePair<string, string>> originalParams, int pageNumber)
    {
        var parameters = originalParams
            .Where(p => !p.Key.Equals("page_number", StringComparison.OrdinalIgnoreCase))
            .ToList();

        parameters.Add(new KeyValuePair<string, string>("page_number", pageNumber.ToString()));

        var query = BuildQueryString(parameters);

        var builder = new UriBuilder(baseUri)
        {
            Query = query
        };

        return builder.Uri.ToString();
    }

    private static int ExtractPageSize(
        IEnumerable<KeyValuePair<string, string>> parameters,
        int searchPageCount,
        int fallbackPageSize)
    {
        var param = parameters.LastOrDefault(p => p.Key.Equals("page_limit", StringComparison.OrdinalIgnoreCase));
        if (param.Key != null && int.TryParse(param.Value, out var pageSize) && pageSize > 0)
            return pageSize;

        return searchPageCount > 0 ? searchPageCount : fallbackPageSize;
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        if (pageSize <= 0 || totalCount <= 0)
            return 1;

        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join(
            "&",
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }

    private static bool IsCompanyInnConflict(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UX_Companies_Inn", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null)
            return false;

        var prop = inner.GetType().GetProperty("Number");
        if (prop?.PropertyType == typeof(int))
        {
            var number = (int)(prop.GetValue(inner) ?? 0);
            return number is 2601 or 2627;
        }

        var msg = inner.Message ?? string.Empty;
        return msg.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DetachAlreadyExistingFabrikantNoticesAsync(CancellationToken cancellationToken)
    {
        var addedPurchaseNumbers = _dbContext.ChangeTracker.Entries<Notice>()
            .Where(e => e.State == EntityState.Added && e.Entity.Source == NoticeSource.Fabrikant)
            .Select(e => e.Entity.PurchaseNumber)
            .Where(pn => !string.IsNullOrWhiteSpace(pn))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (addedPurchaseNumbers.Count == 0)
            return;

        var existing = await _dbContext.Notices
            .AsNoTracking()
            .Where(n => n.Source == NoticeSource.Fabrikant && addedPurchaseNumbers.Contains(n.PurchaseNumber))
            .Select(n => n.PurchaseNumber)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var entry in _dbContext.ChangeTracker.Entries<Notice>().Where(e => e.State == EntityState.Added).ToList())
        {
            if (entry.Entity.Source != NoticeSource.Fabrikant)
                continue;

            if (!existingSet.Contains(entry.Entity.PurchaseNumber))
                continue;

            entry.State = EntityState.Detached;
        }

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
