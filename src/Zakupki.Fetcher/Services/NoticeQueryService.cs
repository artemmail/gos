using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models.Notices;

namespace Zakupki.Fetcher.Services;

public interface INoticeQueryService
{
    Task<IReadOnlyCollection<string>> GetMissingPurchaseNumbersAsync(
        byte regionCode,
        IReadOnlyCollection<string> purchaseNumbers,
        CancellationToken cancellationToken);

    Task<VectorSearchResult> SearchVectorAsync(VectorSearchRequest request, CancellationToken cancellationToken);
}

public sealed class NoticeQueryService : INoticeQueryService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly UserCompanyService _userCompanyService;
    private readonly ILogger<NoticeQueryService> _logger;

    public NoticeQueryService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        UserCompanyService userCompanyService,
        ILogger<NoticeQueryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _userCompanyService = userCompanyService;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<string>> GetMissingPurchaseNumbersAsync(
        byte regionCode,
        IReadOnlyCollection<string> purchaseNumbers,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingNumbers = await context.Notices
            .AsNoTracking()
            .Where(n => n.Region == regionCode && purchaseNumbers.Contains(n.PurchaseNumber))
            .Select(n => n.PurchaseNumber)
            .ToListAsync(cancellationToken);

        return purchaseNumbers
            .Except(existingNumbers, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<VectorSearchResult> SearchVectorAsync(VectorSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var similarityThreshold = Math.Clamp(request.SimilarityThresholdPercent, 40, 90) / 100.0;
            var distanceThreshold = 1.0 - similarityThreshold;
            var page = Math.Max(request.Page, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var normalizedCollectingEnd = (request.CollectingEndLimit ?? DateTimeOffset.UtcNow).UtcDateTime;
            var offset = (page - 1) * pageSize;

            var normalizedSortField = string.IsNullOrWhiteSpace(request.SortField)
                ? "similarity"
                : request.SortField.Trim().ToLowerInvariant();

            var normalizedSortDirection = string.IsNullOrWhiteSpace(request.SortDirection)
                ? "desc"
                : request.SortDirection.Trim().ToLowerInvariant();

            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var queryVectorEntity = await context.UserQueryVectors
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.Id == request.QueryVectorId && v.UserId == request.UserId,
                    cancellationToken);

            if (queryVectorEntity is null)
            {
                return VectorSearchResult.FromError(VectorSearchError.QueryNotFound, "Запрос не найден");
            }

            if (queryVectorEntity.Vector is null)
            {
                return VectorSearchResult.FromError(VectorSearchError.QueryNotReady, "Вектор запроса ещё не готов");
            }

            var queryVector = queryVectorEntity.Vector.Value;

            string[]? userRegions = null;
            string[]? userOkpd2Codes = null;

            if (request.FilterByUserRegions)
            {
                userRegions = await GetUserRegionCodesAsync(request.UserId, cancellationToken);

                if (userRegions.Length == 0)
                {
                    return VectorSearchResult.FromError(
                        VectorSearchError.MissingRegions,
                        "В профиле не указаны регионы для фильтрации.");
                }
            }

            if (request.FilterByUserOkpd2Codes)
            {
                userOkpd2Codes = await GetUserOkpd2CodesAsync(request.UserId, cancellationToken);
            }

            var noticesQuery = context.Notices.AsNoTracking();

            if (userRegions is not null)
            {
                noticesQuery = ApplyRegionFilter(noticesQuery, userRegions);
            }

            if (userOkpd2Codes is not null && userOkpd2Codes.Length > 0)
            {
                noticesQuery = ApplyOkpd2Filter(noticesQuery, userOkpd2Codes);
            }

            var matchesQuery = noticesQuery
                .Where(n => n.Vector != null)
                .Select(n => new NoticeVectorMatch
                {
                    Notice = n,
                    Distance = EF.Functions.VectorDistance("cosine", n.Vector!.Value, queryVector),
                    Analysis = n.Analyses
                        .Where(a => a.UserId == request.UserId)
                        .OrderByDescending(a => a.UpdatedAt)
                        .Select(a => new NoticeAnalysisSummary
                        {
                            Status = a.Status,
                            UpdatedAt = a.UpdatedAt,
                            HasResult = a.Result != null && a.Result != "",
                            Recommended = a.Recommended,
                            DecisionScore = a.DecisionScore
                        })
                        .FirstOrDefault()
                })
                .Where(m => m.Distance <= distanceThreshold);

            if (!request.ExpiredOnly)
            {
                matchesQuery = matchesQuery
                    .Where(m => m.Notice.CollectingEnd == null || m.Notice.CollectingEnd > normalizedCollectingEnd);
            }

            var sortedMatches = ApplyVectorSorting(matchesQuery, normalizedSortField, normalizedSortDirection);
            var totalCount = await sortedMatches.LongCountAsync(cancellationToken);

            if (totalCount == 0)
            {
                return VectorSearchResult.FromData(new PagedResult<NoticeListItemDto>(
                    Array.Empty<NoticeListItemDto>(),
                    0,
                    page,
                    pageSize));
            }

            var rows = await sortedMatches
                .Skip(offset)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Notice,
                    m.Distance,
                    m.Analysis,
                    ProcedureSubmissionDate = m.Notice.ProcedureWindow != null
                        ? (string?)m.Notice.ProcedureWindow.SubmissionProcedureDateRaw
                        : null
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(x => new NoticeListItemDto(
                    x.Notice.Id,
                    x.Notice.PurchaseNumber,
                    x.Notice.Source,
                    x.Notice.PublishDate,
                    x.Notice.EtpName,
                    x.Notice.Region,
                    x.Notice.PurchaseObjectInfo,
                    x.Notice.MaxPrice,
                    x.Notice.Okpd2Code,
                    x.Notice.Okpd2Name,
                    x.Notice.KvrCode,
                    BuildKvrNameWithRegionDebug(x.Notice),
                    x.Notice.RawJson,
                    x.Notice.CollectingEnd,
                    x.ProcedureSubmissionDate,
                    x.Analysis != null &&
                    x.Analysis.Status == NoticeAnalysisStatus.Completed &&
                    x.Analysis.HasResult,
                    x.Analysis?.Status,
                    x.Analysis?.UpdatedAt,
                    x.Analysis?.Recommended,
                    x.Analysis?.DecisionScore,
                    false,
                    1.0 - x.Distance))
                .ToList();

            var total = (int)Math.Min(int.MaxValue, totalCount);
            var result = new PagedResult<NoticeListItemDto>(items, total, page, pageSize);

            return VectorSearchResult.FromData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute vector search for user {UserId}", request.UserId);
            return VectorSearchResult.FromError(VectorSearchError.Unknown, "Не удалось выполнить поиск");
        }
    }

    private static IOrderedQueryable<NoticeVectorMatch> ApplyVectorSorting(
        IQueryable<NoticeVectorMatch> query,
        string sortField,
        string sortDirection)
    {
        var descending = sortDirection == "desc";

        return sortField switch
        {
            "similarity" => descending
                ? query.OrderBy(m => m.Distance).ThenByDescending(m => m.Notice.Id)
                : query.OrderByDescending(m => m.Distance).ThenByDescending(m => m.Notice.Id),
            "purchasenumber" => descending
                ? query.OrderByDescending(m => m.Notice.PurchaseNumber).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.PurchaseNumber).ThenBy(m => m.Distance),
            "etpname" => descending
                ? query.OrderByDescending(m => m.Notice.EtpName).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.EtpName).ThenBy(m => m.Distance),
            "region" => descending
                ? query.OrderByDescending(m => m.Notice.Region).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.Region).ThenBy(m => m.Distance),
            "purchaseobjectinfo" => descending
                ? query.OrderByDescending(m => m.Notice.PurchaseObjectInfo).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.PurchaseObjectInfo).ThenBy(m => m.Distance),
            "okpd2code" => descending
                ? query.OrderByDescending(m => m.Notice.Okpd2Code).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.Okpd2Code).ThenBy(m => m.Distance),
            "okpd2name" => descending
                ? query.OrderByDescending(m => m.Notice.Okpd2Name).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.Okpd2Name).ThenBy(m => m.Distance),
            "kvrcode" => descending
                ? query.OrderByDescending(m => m.Notice.KvrCode).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.KvrCode).ThenBy(m => m.Distance),
            "kvrname" => descending
                ? query.OrderByDescending(m => m.Notice.KvrName).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.KvrName).ThenBy(m => m.Distance),
            "maxprice" => descending
                ? query.OrderByDescending(m => m.Notice.MaxPrice).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.MaxPrice).ThenBy(m => m.Distance),
            "collectingend" => descending
                ? query.OrderByDescending(m => m.Notice.CollectingEnd).ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Notice.CollectingEnd).ThenBy(m => m.Distance),
            "analysisstatus" => descending
                ? query.OrderByDescending(m => m.Analysis != null ? m.Analysis.Status : null)
                    .ThenByDescending(m => m.Analysis != null ? m.Analysis.UpdatedAt : null)
                    .ThenBy(m => m.Distance)
                : query.OrderBy(m => m.Analysis != null ? m.Analysis.Status : null)
                    .ThenBy(m => m.Analysis != null ? m.Analysis.UpdatedAt : null)
                    .ThenBy(m => m.Distance),
            _ => descending
                ? query.OrderByDescending(m => m.Notice.PublishDate).ThenByDescending(m => m.Notice.Id)
                : query.OrderBy(m => m.Notice.PublishDate).ThenBy(m => m.Notice.Id)
        };
    }

    private async Task<string[]> GetUserRegionCodesAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await _userCompanyService.GetProfileAsync(userId, cancellationToken);

        return profile.Regions
            .Select(UserCompanyService.FormatRegionCode)
            .ToArray();
    }

    private async Task<string[]> GetUserOkpd2CodesAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await _userCompanyService.GetProfileAsync(userId, cancellationToken);

        return profile.Okpd2Codes
            .Select(code => code.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToArray();
    }

    private static IQueryable<Notice> ApplyRegionFilter(IQueryable<Notice> query, IReadOnlyCollection<string> regions)
    {
        var regionCodes = NormalizeRegions(regions);

        if (regionCodes.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(n => regionCodes.Contains(n.Region));
    }

    private static IQueryable<Notice> ApplyOkpd2Filter(IQueryable<Notice> query, IReadOnlyCollection<string> okpd2Codes)
    {
        var normalizedCodes = okpd2Codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return query;
        }

        return query.Where(n => n.Okpd2Code != null && normalizedCodes.Any(code => n.Okpd2Code!.StartsWith(code)));
    }

    private static byte[] NormalizeRegions(IReadOnlyCollection<string> regions) =>
        regions
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Select(r => byte.TryParse(r, out var code) ? (byte?)code : null)
            .Where(code => code.HasValue)
            .Select(code => code!.Value)
            .Distinct()
            .ToArray();

    private static string BuildKvrNameWithRegionDebug(Notice notice)
    {
        var baseName = string.IsNullOrWhiteSpace(notice.KvrName) ? null : notice.KvrName.Trim();
        var region = notice.Region.ToString("D2");
        var debugInfo = $"[db:{region}]";

        return string.IsNullOrWhiteSpace(baseName)
            ? debugInfo
            : $"{baseName} {debugInfo}";
    }
}
