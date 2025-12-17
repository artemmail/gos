using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Models.Notices;
using Zakupki.Fetcher.Services;
using Zakupki.Fetcher.Utilities;
using Zakupki.EF2020;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NoticesController : ControllerBase
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly AttachmentDownloadService _attachmentDownloadService;
    private readonly AttachmentMarkdownService _attachmentMarkdownService;
    private readonly AttachmentContentExtractor _attachmentContentExtractor;
    private readonly MosTenderSyncService _mosTenderSyncService;
    private readonly NoticeAnalysisService _noticeAnalysisService;
    private readonly NoticeAnalysisReportService _noticeAnalysisReportService;
    private readonly ILogger<NoticesController> _logger;
    private readonly IFavoriteSearchQueueService _favoriteSearchQueueService;
    private readonly UserCompanyService _userCompanyService;
    private readonly IXmlImportQueue _xmlImportQueue;
    private readonly INoticeQueryService _noticeQueryService;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private static readonly char[] CodeSeparators = new[] { ',', ';', '\n', '\r', '\t', ' ' };

    public NoticesController(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        AttachmentDownloadService attachmentDownloadService,
        AttachmentMarkdownService attachmentMarkdownService,
        AttachmentContentExtractor attachmentContentExtractor,
        MosTenderSyncService mosTenderSyncService,
        NoticeAnalysisService noticeAnalysisService,
        NoticeAnalysisReportService noticeAnalysisReportService,
        IFavoriteSearchQueueService favoriteSearchQueueService,
        ILogger<NoticesController> logger,
        UserCompanyService userCompanyService,
        IXmlImportQueue xmlImportQueue,
        INoticeQueryService noticeQueryService)
    {
        _dbContextFactory = dbContextFactory;
        _attachmentDownloadService = attachmentDownloadService;
        _attachmentMarkdownService = attachmentMarkdownService;
        _attachmentContentExtractor = attachmentContentExtractor;
        _mosTenderSyncService = mosTenderSyncService;
        _noticeAnalysisService = noticeAnalysisService;
        _noticeAnalysisReportService = noticeAnalysisReportService;
        _favoriteSearchQueueService = favoriteSearchQueueService;
        _logger = logger;
        _userCompanyService = userCompanyService;
        _xmlImportQueue = xmlImportQueue;
        _noticeQueryService = noticeQueryService;
    }

    [HttpPost("xml-import")]
    [HttpPost("/api/xml-import")]
    public async Task<IActionResult> EnqueueXmlImport([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Не передан архив для импорта" });
        }

        await using var stream = file.OpenReadStream();
        await _xmlImportQueue.EnqueueAsync(stream, file.FileName, cancellationToken);

        return Accepted(new { message = "Архив поставлен в очередь импорта" });
    }


    [HttpPost("missing-purchase-numbers")]
    [HttpPost("/api/missing-purchase-numbers")]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GetMissingPurchaseNumbers(
        [FromBody] MissingPurchaseNumbersRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Тело запроса пустое." });
        }

        if (string.IsNullOrWhiteSpace(request.Region))
        {
            return BadRequest(new { message = "Не указан регион." });
        }

        if (!byte.TryParse(request.Region, out var regionCode))
        {
            return BadRequest(new { message = "Некорректный код региона." });
        }

        if (request.PurchaseNumbers is null || request.PurchaseNumbers.Count == 0)
        {
            return Ok(Array.Empty<string>());
        }

        var normalizedNumbers = request.PurchaseNumbers
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedNumbers.Length == 0)
        {
            return Ok(Array.Empty<string>());
        }

        var missingNumbers = await _noticeQueryService
            .GetMissingPurchaseNumbersAsync(regionCode, normalizedNumbers, cancellationToken);

        return Ok(missingNumbers.ToArray());
    }


    [HttpPost("favorite-search")]
    [Authorize]
    public async Task<IActionResult> EnqueueFavoriteSearch(
        [FromBody] FavoriteSearchEnqueueRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Тело запроса пустое" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _favoriteSearchQueueService.EnqueueAsync(userId, request, cancellationToken);

        if (result.Enqueued)
        {
            return Accepted(new { message = "Задача добавлена" });
        }

        return result.Error switch
        {
            FavoriteSearchEnqueueError.Disabled => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Message ?? "Очередь недоступна" }),
            FavoriteSearchEnqueueError.Duplicate => Conflict(new { message = result.Message ?? "Запрос уже в очереди" }),
            FavoriteSearchEnqueueError.Invalid => BadRequest(new { message = result.Message ?? "Некорректные параметры" }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Message ?? "Не удалось поставить задачу" })
        };
    }

    
    [HttpGet("vector-search")]
    [Authorize]
    public async Task<ActionResult<PagedResult<NoticeListItemDto>>> VectorSearch(
        [FromQuery] Guid queryVectorId,
        [FromQuery] int similarityThresholdPercent = 60,
        [FromQuery] bool expiredOnly = false,
        [FromQuery] bool filterByUserRegions = false,
        [FromQuery] bool filterByUserOkpd2Codes = false,
        [FromQuery] DateTimeOffset? collectingEndLimit = null,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
            return Unauthorized();

        if (queryVectorId == Guid.Empty)
            return BadRequest(new { message = "Укажите сохранённый запрос" });

        if (page < 1)
            page = 1;

        if (pageSize < 1)
            pageSize = 20;

        pageSize = Math.Min(pageSize, 100);

        var request = new VectorSearchRequest
        {
            UserId = currentUserId,
            QueryVectorId = queryVectorId,
            SimilarityThresholdPercent = similarityThresholdPercent,
            ExpiredOnly = expiredOnly,
            FilterByUserRegions = filterByUserRegions,
            FilterByUserOkpd2Codes = filterByUserOkpd2Codes,
            CollectingEndLimit = collectingEndLimit,
            SortField = sortField,
            SortDirection = sortDirection,
            Page = page,
            PageSize = pageSize
        };

        var result = await _noticeQueryService.SearchVectorAsync(request, cancellationToken);

        if (result.Error == VectorSearchError.QueryNotFound)
        {
            return NotFound(new { message = result.Message ?? "Запрос не найден" });
        }

        if (result.Error == VectorSearchError.QueryNotReady)
        {
            return BadRequest(new { message = result.Message ?? "Вектор запроса ещё не готов" });
        }

        if (result.Error == VectorSearchError.MissingRegions)
        {
            return BadRequest(new { message = result.Message ?? "В профиле не указаны регионы для фильтрации." });
        }

        if (!result.Success || result.Data is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Message ?? "Не удалось выполнить поиск" });
        }

        return Ok(result.Data);
    }

    [HttpGet("by-number/{purchaseNumber}")]
    public async Task<ActionResult<NoticeDetailsDto>> GetNotice(string purchaseNumber)
    {
        if (string.IsNullOrWhiteSpace(purchaseNumber))
        {
            return BadRequest(new { message = "Не указан номер извещения." });
        }

        var normalizedNumber = purchaseNumber.Trim();

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var notice = await context.Notices
            .AsNoTracking()
            .Where(n => n.PurchaseNumber == normalizedNumber)
            .Select(n => new NoticeDetailsDto(
                n.Id,
                n.PurchaseNumber,
                n.PurchaseObjectInfo,
                n.RawJson))
            .FirstOrDefaultAsync();

        if (notice is null)
        {
            return NotFound();
        }

        return Ok(notice);
    }


    [HttpGet]
    public async Task<ActionResult<PagedResult<NoticeListItemDto>>> GetNotices(
        [FromQuery] string? search,
        [FromQuery] string? purchaseNumber,
        [FromQuery] string? okpd2Codes,
        [FromQuery] string? kvrCodes,
        [FromQuery] string? sortField,
        [FromQuery] string? sortDirection,
        [FromQuery] bool expiredOnly = false,
        [FromQuery] bool filterByUserRegions = false,
        [FromQuery] bool filterByUserOkpd2Codes = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        pageSize = Math.Min(pageSize, 100);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var query = context.Notices.AsNoTracking();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var includeFavorites = !string.IsNullOrEmpty(currentUserId);
        var normalizedCollectingEnd = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            var likeTerm = $"%{trimmedSearch}%";
            query = query.Where(n =>
                EF.Functions.Like(n.PurchaseNumber, likeTerm) ||
                (n.EtpName != null && EF.Functions.Like(n.EtpName, likeTerm)) ||
                (n.PurchaseObjectInfo != null && EF.Functions.Like(n.PurchaseObjectInfo, likeTerm)) ||
                (n.Okpd2Code != null && EF.Functions.Like(n.Okpd2Code, likeTerm)) ||
                (n.Okpd2Name != null && EF.Functions.Like(n.Okpd2Name, likeTerm)) ||
                (n.KvrCode != null && EF.Functions.Like(n.KvrCode, likeTerm)) ||
                (n.KvrName != null && EF.Functions.Like(n.KvrName, likeTerm)));
        }

        if (!string.IsNullOrWhiteSpace(purchaseNumber))
        {
            var trimmedNumber = purchaseNumber.Trim();
            query = query.Where(n => n.PurchaseNumber == trimmedNumber);
        }

        var okpd2CodeList = ParseCodeList(okpd2Codes);

        if (okpd2CodeList.Count > 0)
        {
            query = query.Where(n => n.Okpd2Code != null && okpd2CodeList.Contains(n.Okpd2Code));
        }

        var kvrCodeList = ParseCodeList(kvrCodes);

        if (kvrCodeList.Count > 0)
        {
            query = query.Where(n => n.KvrCode != null && kvrCodeList.Contains(n.KvrCode));
        }

        if (!expiredOnly)
        {
            query = query.Where(n => n.CollectingEnd == null || n.CollectingEnd > normalizedCollectingEnd);
        }

        if (filterByUserRegions)
        {
            if (currentUserId is null)
            {
                return Unauthorized(new { message = "Требуется авторизация для фильтра по регионам профиля." });
            }

            var regions = await GetUserRegionCodesAsync(currentUserId, HttpContext.RequestAborted);

            if (regions.Length == 0)
            {
                return BadRequest(new { message = "В профиле не указаны регионы для фильтрации." });
            }

            query = ApplyRegionFilter(query, regions);
        }

        if (filterByUserOkpd2Codes)
        {
            if (currentUserId is null)
            {
                return Unauthorized(new { message = "Требуется авторизация для фильтра по ОКПД2 профиля." });
            }

            var userOkpd2Codes = await GetUserOkpd2CodesAsync(currentUserId, HttpContext.RequestAborted);

            if (userOkpd2Codes.Length > 0)
            {
                query = ApplyOkpd2Filter(query, userOkpd2Codes);
            }
        }

        var normalizedSortField = string.IsNullOrWhiteSpace(sortField)
            ? "publishDate"
            : sortField.Trim().ToLowerInvariant();

        var normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? "desc"
            : sortDirection.Trim().ToLowerInvariant();

        var queryWithAnalysis = query.Select(n => new NoticeWithAnalysis
        {
            Notice = n,
            Analysis = currentUserId != null
                ? n.Analyses
                    .Where(a => a.UserId == currentUserId)
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
                : null,
            IsFavorite = includeFavorites && n.Favorites.Any(f => f.UserId == currentUserId)
        });

        var sortedQuery = ApplySorting(queryWithAnalysis, normalizedSortField, normalizedSortDirection);

        var totalCount = await sortedQuery.CountAsync();
        var skip = (page - 1) * pageSize;

        var rows = await sortedQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new
            {
                x.Notice,
                x.Analysis,
                x.IsFavorite,
                ProcedureSubmissionDate = x.Notice.ProcedureWindow != null
                    ? (string?)x.Notice.ProcedureWindow.SubmissionProcedureDateRaw
                    : null
            })
            .ToListAsync();

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
                x.Analysis != null && x.Analysis.Status == NoticeAnalysisStatus.Completed && x.Analysis.HasResult,
                x.Analysis != null ? x.Analysis.Status : null,
                x.Analysis != null ? (DateTime?)x.Analysis.UpdatedAt : null,
                x.Analysis != null ? x.Analysis.Recommended : null,
                x.Analysis != null ? x.Analysis.DecisionScore : null,
                x.IsFavorite,
                null))
            .ToList();

        var result = new PagedResult<NoticeListItemDto>(items, totalCount, page, pageSize);
        return Ok(result);
    }

    [HttpGet("mos")]
    public async Task<ActionResult<PagedResult<MosNoticeListItemDto>>> GetMosNotices(
        [FromQuery] string? search,
        [FromQuery] string? sortField,
        [FromQuery] string? sortDirection,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        pageSize = Math.Min(pageSize, 100);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var query = context.Notices
            .AsNoTracking()
            .Where(n => n.Source == NoticeSource.Mos);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            var likeTerm = $"%{trimmedSearch}%";
            query = query.Where(n =>
                EF.Functions.Like(n.PurchaseNumber, likeTerm) ||
                (n.PurchaseObjectInfo != null && EF.Functions.Like(n.PurchaseObjectInfo, likeTerm)));
        }

        var normalizedSortField = string.IsNullOrWhiteSpace(sortField)
            ? "publishdate"
            : sortField.Trim().ToLowerInvariant();

        var descending = string.IsNullOrWhiteSpace(sortDirection)
            ? true
            : sortDirection.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

        var sortedQuery = normalizedSortField switch
        {
            "maxprice" => descending
                ? query.OrderByDescending(n => n.MaxPrice)
                : query.OrderBy(n => n.MaxPrice),
            "collectingend" => descending
                ? query.OrderByDescending(n => n.CollectingEnd)
                : query.OrderBy(n => n.CollectingEnd),
            "name" => descending
                ? query.OrderByDescending(n => n.PurchaseObjectInfo).ThenByDescending(n => n.Id)
                : query.OrderBy(n => n.PurchaseObjectInfo).ThenBy(n => n.Id),
            "region" => descending
                ? query.OrderByDescending(n => n.Region).ThenByDescending(n => n.Id)
                : query.OrderBy(n => n.Region).ThenBy(n => n.Id),
            _ => descending
                ? query.OrderByDescending(n => n.PublishDate).ThenByDescending(n => n.Id)
                : query.OrderBy(n => n.PublishDate).ThenBy(n => n.Id)
        };

        var totalCount = await sortedQuery.LongCountAsync(HttpContext.RequestAborted);
        var offset = (page - 1) * pageSize;

        var rows = await sortedQuery
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(HttpContext.RequestAborted);

        var items = rows
            .Select(MapMosNotice)
            .ToList();

        var limitedTotal = (int)Math.Min(int.MaxValue, totalCount);
        var result = new PagedResult<MosNoticeListItemDto>(items, limitedTotal, page, pageSize);
        return Ok(result);
    }

    [HttpGet("mos/{purchaseNumber}")]
    public async Task<ActionResult<string>> GetMosNotice(string purchaseNumber)
    {
        if (string.IsNullOrWhiteSpace(purchaseNumber))
        {
            return BadRequest("Не указан номер закупки.");
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var trimmedPurchaseNumber = purchaseNumber.Trim();
        var notice = await context.Notices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                n => n.PurchaseNumber == trimmedPurchaseNumber && (n.Source == NoticeSource.Mos || n.Source == NoticeSource.Fabrikant),
                HttpContext.RequestAborted);

        if (notice == null)
        {
            return NotFound();
        }

        if (notice.Uncompleted)
        {
            var completed = await _mosTenderSyncService.EnsureNoticeCompletedAsync(
                notice.Id,
                HttpContext.RequestAborted);

            if (completed)
            {
                notice = await context.Notices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        n => n.PurchaseNumber == trimmedPurchaseNumber && n.Source == NoticeSource.Mos,
                        HttpContext.RequestAborted)
                    ?? notice;
            }
        }

        var details = TryParseUndocumentedAuction(notice.RawJson);
        var result = new MosNoticeDetailsDto(notice.Id, notice.PurchaseNumber, notice.RawJson, notice.Uncompleted, details);
        return Ok(result);
    }

    [HttpGet("favorites")]
    [Authorize]
    public async Task<ActionResult<PagedResult<NoticeListItemDto>>> GetFavoriteNotices(
        [FromQuery] string? search,
        [FromQuery] string? purchaseNumber,
        [FromQuery] string? okpd2Codes,
        [FromQuery] string? kvrCodes,
        [FromQuery] string? sortField,
        [FromQuery] string? sortDirection,
        [FromQuery] bool expiredOnly = false,
        [FromQuery] bool filterByUserRegions = false,
        [FromQuery] bool filterByUserOkpd2Codes = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        pageSize = Math.Min(pageSize, 100);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var query = context.Notices
            .AsNoTracking()
            .Where(n => n.Favorites.Any(f => f.UserId == currentUserId));
        var normalizedCollectingEnd = DateTime.UtcNow;

        if (filterByUserRegions)
        {
            var regions = await GetUserRegionCodesAsync(currentUserId, HttpContext.RequestAborted);

            if (regions.Length == 0)
            {
                return BadRequest(new { message = "В профиле не указаны регионы для фильтрации." });
            }

            query = ApplyRegionFilter(query, regions);
        }

        if (filterByUserOkpd2Codes)
        {
            var userOkpd2Codes = await GetUserOkpd2CodesAsync(currentUserId, HttpContext.RequestAborted);

            if (userOkpd2Codes.Length > 0)
            {
                query = ApplyOkpd2Filter(query, userOkpd2Codes);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            var likeTerm = $"%{trimmedSearch}%";
            query = query.Where(n =>
                EF.Functions.Like(n.PurchaseNumber, likeTerm) ||
                (n.EtpName != null && EF.Functions.Like(n.EtpName, likeTerm)) ||
                (n.PurchaseObjectInfo != null && EF.Functions.Like(n.PurchaseObjectInfo, likeTerm)) ||
                (n.Okpd2Code != null && EF.Functions.Like(n.Okpd2Code, likeTerm)) ||
                (n.Okpd2Name != null && EF.Functions.Like(n.Okpd2Name, likeTerm)) ||
                (n.KvrCode != null && EF.Functions.Like(n.KvrCode, likeTerm)) ||
                (n.KvrName != null && EF.Functions.Like(n.KvrName, likeTerm)));
        }

        if (!string.IsNullOrWhiteSpace(purchaseNumber))
        {
            var trimmedNumber = purchaseNumber.Trim();
            query = query.Where(n => n.PurchaseNumber == trimmedNumber);
        }

        var okpd2CodeList = ParseCodeList(okpd2Codes);

        if (okpd2CodeList.Count > 0)
        {
            query = query.Where(n => n.Okpd2Code != null && okpd2CodeList.Contains(n.Okpd2Code));
        }

        var kvrCodeList = ParseCodeList(kvrCodes);

        if (kvrCodeList.Count > 0)
        {
            query = query.Where(n => n.KvrCode != null && kvrCodeList.Contains(n.KvrCode));
        }

        if (!expiredOnly)
        {
            query = query.Where(n => n.CollectingEnd == null || n.CollectingEnd > normalizedCollectingEnd);
        }

        var normalizedSortField = string.IsNullOrWhiteSpace(sortField)
            ? "publishDate"
            : sortField.Trim().ToLowerInvariant();

        var normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? "desc"
            : sortDirection.Trim().ToLowerInvariant();

        var queryWithAnalysis = query.Select(n => new NoticeWithAnalysis
        {
            Notice = n,
            Analysis = n.Analyses
                .Where(a => a.UserId == currentUserId)
                .OrderByDescending(a => a.UpdatedAt)
                .Select(a => new NoticeAnalysisSummary
                {
                    Status = a.Status,
                    UpdatedAt = a.UpdatedAt,
                    HasResult = a.Result != null && a.Result != "",
                    Recommended = a.Recommended,
                    DecisionScore = a.DecisionScore
                })
                .FirstOrDefault(),
            IsFavorite = true
        });

        var sortedQuery = ApplySorting(queryWithAnalysis, normalizedSortField, normalizedSortDirection);

        var totalCount = await sortedQuery.CountAsync();
        var skip = (page - 1) * pageSize;

        var rows = await sortedQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new
            {
                x.Notice,
                x.Analysis,
                ProcedureSubmissionDate = x.Notice.ProcedureWindow != null
                    ? (string?)x.Notice.ProcedureWindow.SubmissionProcedureDateRaw
                    : null
            })
            .ToListAsync();

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
                x.Analysis != null && x.Analysis.Status == NoticeAnalysisStatus.Completed && x.Analysis.HasResult,
                x.Analysis != null ? x.Analysis.Status : null,
                x.Analysis != null ? (DateTime?)x.Analysis.UpdatedAt : null,
                x.Analysis != null ? x.Analysis.Recommended : null,
                x.Analysis != null ? x.Analysis.DecisionScore : null,
                true,
                null))
            .ToList();

        var result = new PagedResult<NoticeListItemDto>(items, totalCount, page, pageSize);
        return Ok(result);
    }

    [HttpPost("{noticeId:guid}/favorite")]
    [Authorize]
    public async Task<IActionResult> AddFavorite(Guid noticeId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var exists = await context.FavoriteNotices
            .AnyAsync(f => f.UserId == currentUserId && f.NoticeId == noticeId);

        if (exists)
        {
            return NoContent();
        }

        var noticeExists = await context.Notices.AnyAsync(n => n.Id == noticeId);
        if (!noticeExists)
        {
            return NotFound();
        }

        var favorite = new FavoriteNotice
        {
            Id = Guid.NewGuid(),
            NoticeId = noticeId,
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        context.FavoriteNotices.Add(favorite);
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{noticeId:guid}/favorite")]
    [Authorize]
    public async Task<IActionResult> RemoveFavorite(Guid noticeId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var favorite = await context.FavoriteNotices
            .FirstOrDefaultAsync(f => f.UserId == currentUserId && f.NoticeId == noticeId);

        if (favorite == null)
        {
            return NotFound();
        }

        context.FavoriteNotices.Remove(favorite);
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{noticeId:guid}/analysis")]
    [Authorize]
    public async Task<ActionResult<NoticeAnalysisResponse>> GetAnalysisStatus(Guid noticeId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var response = await _noticeAnalysisService.GetStatusAsync(noticeId, userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{noticeId:guid}/analysis")]
    [Authorize]
    public async Task<ActionResult<NoticeAnalysisResponse>> AnalyzeNotice(
        Guid noticeId,
        [FromQuery] bool force,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _noticeAnalysisService.AnalyzeAsync(noticeId, userId, force, cancellationToken);
            return Ok(response);
        }
        catch (NoticeAnalysisException ex) when (ex.IsValidation)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NoticeAnalysisException ex)
        {
            _logger.LogError(ex, "Failed to analyze notice {NoticeId}", noticeId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [HttpGet("{noticeId:guid}/analysis/report")]
    [Authorize]
    public async Task<IActionResult> DownloadAnalysisReport(Guid noticeId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var report = await _noticeAnalysisReportService.CreateAsync(noticeId, userId, cancellationToken);
            return File(report.Content, report.ContentType, report.FileName);
        }
        catch (NoticeAnalysisException ex) when (ex.IsValidation)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build notice analysis report for notice {NoticeId}", noticeId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Не удалось сформировать файл отчёта." });
        }
    }


    [HttpGet("attachments/{attachmentId:guid}/markdown")]
    public async Task<IActionResult> DownloadAttachmentMarkdown(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.NoticeAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(attachment.MarkdownContent))
        {
            return BadRequest(new { message = "Markdown-версия для этого вложения отсутствует. Используйте конвертацию." });
        }

        var markdownBytes = Encoding.UTF8.GetBytes(attachment.MarkdownContent);
        var sanitizedFileName = FileNameHelper.SanitizeFileName(attachment.FileName);
        var downloadFileName = Path.ChangeExtension(sanitizedFileName, ".md") ?? "attachment.md";

        return File(markdownBytes, "text/markdown", downloadFileName);
    }

    [HttpGet("{noticeId:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyCollection<NoticeAttachmentDto>>> GetAttachments(
        Guid noticeId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attachments = await context.NoticeAttachments
            .AsNoTracking()
            .Where(a => a.NoticeId == noticeId)
            .OrderBy(a => a.FileName)
            .Select(a => new NoticeAttachmentDto(
                a.Id,
                a.PublishedContentId,
                a.FileName,
                a.FileSize,
                a.Description,
                a.DocumentDate,
                a.DocumentKindCode,
                a.DocumentKindName,
                a.Url,
                a.SourceFileName,
                a.InsertedAt,
                a.LastSeenAt,
                a.BinaryContent != null,
                !string.IsNullOrEmpty(a.MarkdownContent)))
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
        {
            var noticeExists = await context.Notices
                .AsNoTracking()
                .AnyAsync(n => n.Id == noticeId, cancellationToken);

            if (!noticeExists)
            {
                return NotFound();
            }
        }

        return Ok(attachments);
    }

    [HttpPost("attachments/{attachmentId:guid}/download")]
    public async Task<ActionResult<NoticeAttachmentDto>> DownloadAttachment(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.NoticeAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(attachment.Url))
        {
            return BadRequest(new { message = "Для вложения отсутствует ссылка на скачивание." });
        }

        try
        {
            var content = await _attachmentDownloadService.DownloadAsync(attachment.Url!, cancellationToken: cancellationToken);
            UpdateAttachmentContent(attachment, content, null);
            await context.SaveChangesAsync(cancellationToken);
            return Ok(MapAttachment(attachment));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download attachment {AttachmentId} from {AttachmentUrl}", attachment.Id, attachment.Url);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Не удалось скачать файл с удаленного сервера." });
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read attachment content for {AttachmentId}", attachment.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Не удалось обработать содержимое файла." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while downloading attachment {AttachmentId}", attachment.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Произошла непредвиденная ошибка при скачивании файла." });
        }
    }

    [HttpGet("attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> GetAttachmentContent(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.NoticeAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
        {
            return BadRequest(new { message = "Файл отсутствует в базе данных. Используйте кнопку \"Скачать недостающие\"." });
        }

        var downloadFileName = FileNameHelper.SanitizeFileName(attachment.FileName);
        var contentType = GetContentType(downloadFileName);

        return File(attachment.BinaryContent, contentType, downloadFileName);
    }

    [HttpPost("{noticeId:guid}/attachments/download-missing")]
    public async Task<ActionResult<AttachmentDownloadResultDto>> DownloadMissingAttachments(
        Guid noticeId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attachments = await context.NoticeAttachments
            .Where(a => a.NoticeId == noticeId && a.BinaryContent == null)
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
        {
            var exists = await context.Notices
                .AsNoTracking()
                .AnyAsync(n => n.Id == noticeId, cancellationToken);

            if (!exists)
            {
                return NotFound();
            }

            return Ok(new AttachmentDownloadResultDto(0, 0, 0));
        }

        var downloaded = 0;
        var failed = 0;

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.Url))
            {
                failed++;
                continue;
            }

            try
            {
                var content = await _attachmentDownloadService.DownloadAsync(attachment.Url!, cancellationToken: cancellationToken);
                UpdateAttachmentContent(attachment, content, null);
                downloaded++;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                failed++;
                _logger.LogWarning(ex, "Failed to download attachment {AttachmentId} from {AttachmentUrl}", attachment.Id, attachment.Url);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Unexpected error while downloading attachment {AttachmentId}", attachment.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(new AttachmentDownloadResultDto(attachments.Count, downloaded, failed));
    }

    [HttpPost("{noticeId:guid}/attachments/convert-to-markdown")]
    public async Task<ActionResult<AttachmentMarkdownConversionResultDto>> ConvertAttachmentsToMarkdown(
        Guid noticeId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var attachments = await context.NoticeAttachments
            .Where(a => a.NoticeId == noticeId)
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
        {
            var noticeExists = await context.Notices
                .AsNoTracking()
                .AnyAsync(n => n.Id == noticeId, cancellationToken);

            if (!noticeExists)
            {
                return NotFound();
            }

            return Ok(new AttachmentMarkdownConversionResultDto(0, 0, 0, 0, 0));
        }

        var converted = 0;
        var missingContent = 0;
        var unsupported = 0;
        var failed = 0;

        foreach (var attachment in attachments)
        {
            if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
            {
                missingContent++;
                continue;
            }

            var attachmentForConversion = PrepareAttachmentForConversion(attachment);

            if (!_attachmentMarkdownService.IsSupported(attachmentForConversion))
            {
                unsupported++;
                continue;
            }

            try
            {
                var markdown = await _attachmentMarkdownService.ConvertToMarkdownAsync(attachmentForConversion, cancellationToken);
                if (string.IsNullOrWhiteSpace(markdown))
                {
                    attachment.MarkdownContent = null;
                    failed++;
                    _logger.LogWarning("Markdown conversion produced empty result for attachment {AttachmentId}", attachment.Id);
                }
                else
                {
                    attachment.MarkdownContent = markdown;
                    converted++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to convert attachment {AttachmentId} to Markdown", attachment.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var result = new AttachmentMarkdownConversionResultDto(
            attachments.Count,
            converted,
            missingContent,
            unsupported,
            failed);

        return Ok(result);
    }

    private static NoticeAttachmentDto MapAttachment(NoticeAttachment attachment)
    {
        return new NoticeAttachmentDto(
            attachment.Id,
            attachment.PublishedContentId,
            attachment.FileName,
            attachment.FileSize,
            attachment.Description,
            attachment.DocumentDate,
            attachment.DocumentKindCode,
            attachment.DocumentKindName,
            attachment.Url,
            attachment.SourceFileName,
            attachment.InsertedAt,
            attachment.LastSeenAt,
            attachment.BinaryContent != null,
            !string.IsNullOrWhiteSpace(attachment.MarkdownContent));
    }

    private string BuildKvrNameWithRegionDebug(Notice notice)
    {
        var baseName = string.IsNullOrWhiteSpace(notice.KvrName) ? null : notice.KvrName.Trim();
        var region = notice.Region.ToString("D2");
        var debugInfo = $"[db:{region}]";

        return string.IsNullOrWhiteSpace(baseName)
            ? debugInfo
            : $"{baseName} {debugInfo}";
    }

    private static List<string> ParseCodeList(string? rawCodes)
    {
        if (string.IsNullOrWhiteSpace(rawCodes))
        {
            return new List<string>();
        }

        return rawCodes
            .Split(CodeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
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

    private NoticeAttachment PrepareAttachmentForConversion(NoticeAttachment attachment)
    {
        if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
        {
            throw new InvalidOperationException("Attachment does not contain binary content for conversion.");
        }

        var processed = _attachmentContentExtractor.Process(attachment, attachment.BinaryContent);

        if (ReferenceEquals(processed.Content, attachment.BinaryContent) && string.IsNullOrWhiteSpace(processed.FileNameOverride))
        {
            return attachment;
        }

        return new NoticeAttachment
        {
            Id = attachment.Id,
            FileName = processed.FileNameOverride ?? attachment.FileName,
            BinaryContent = processed.Content
        };
    }

    private static void UpdateAttachmentContent(NoticeAttachment attachment, byte[] content, string? newFileName)
    {
        attachment.BinaryContent = content;
        attachment.FileSize = content.LongLength;
        attachment.ContentHash = HashUtilities.ComputeSha256Hex(content);
        attachment.LastSeenAt = DateTime.UtcNow;
        attachment.MarkdownContent = null;

        if (!string.IsNullOrWhiteSpace(newFileName))
        {
            attachment.FileName = newFileName;
        }
    }

    private static IQueryable<NoticeWithAnalysis> ApplySorting(
        IQueryable<NoticeWithAnalysis> query,
        string sortField,
        string sortDirection)
    {
        var descending = sortDirection == "desc";

        return sortField switch
        {
            "purchasenumber" => descending
                ? query.OrderByDescending(n => n.Notice.PurchaseNumber).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.PurchaseNumber).ThenByDescending(n => n.Notice.Id),
            "etpname" => descending
                ? query.OrderByDescending(n => n.Notice.EtpName).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.EtpName).ThenByDescending(n => n.Notice.Id),
            "region" => descending
                ? query.OrderByDescending(n => n.Notice.Region).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.Region).ThenByDescending(n => n.Notice.Id),
            "purchaseobjectinfo" => descending
                ? query.OrderByDescending(n => n.Notice.PurchaseObjectInfo).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.PurchaseObjectInfo).ThenByDescending(n => n.Notice.Id),
            "okpd2code" => descending
                ? query.OrderByDescending(n => n.Notice.Okpd2Code).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.Okpd2Code).ThenByDescending(n => n.Notice.Id),
            "okpd2name" => descending
                ? query.OrderByDescending(n => n.Notice.Okpd2Name).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.Okpd2Name).ThenByDescending(n => n.Notice.Id),
            "kvrcode" => descending
                ? query.OrderByDescending(n => n.Notice.KvrCode).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.KvrCode).ThenByDescending(n => n.Notice.Id),
            "kvrname" => descending
                ? query.OrderByDescending(n => n.Notice.KvrName).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.KvrName).ThenByDescending(n => n.Notice.Id),
            "maxprice" => descending
                ? query.OrderByDescending(n => n.Notice.MaxPrice).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.MaxPrice).ThenByDescending(n => n.Notice.Id),
            "collectingend" => descending
                ? query.OrderByDescending(n => n.Notice.CollectingEnd).ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.CollectingEnd).ThenByDescending(n => n.Notice.Id),
            "analysisstatus" => descending
                ? query.OrderByDescending(n => n.Analysis != null ? n.Analysis.Status : null)
                    .ThenByDescending(n => n.Analysis != null ? n.Analysis.UpdatedAt : null)
                    .ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Analysis != null ? n.Analysis.Status : null)
                    .ThenBy(n => n.Analysis != null ? n.Analysis.UpdatedAt : null)
                    .ThenByDescending(n => n.Notice.Id),
            _ => descending
                ? query.OrderByDescending(n => n.Notice.PublishDate)
                    .ThenByDescending(n => n.Notice.Id)
                : query.OrderBy(n => n.Notice.PublishDate)
                    .ThenBy(n => n.Notice.Id)
        };
    }

    private static string GetContentType(string fileName)
    {
        if (ContentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            return contentType;
        }

        return "application/octet-stream";
    }

    private static MosNoticeListItemDto MapMosNotice(Notice notice)
    {
        var details = TryParseUndocumentedAuction(notice.RawJson);

        return new MosNoticeListItemDto(
            notice.Id,
            notice.PurchaseNumber,
            details?.name ?? notice.PurchaseObjectInfo,
            notice.PublishDate,
            notice.CollectingEnd,
            notice.MaxPrice,
            details?.federalLawName,
            notice.Region,
            notice.Source,
            notice.Uncompleted,
            details?.customer?.inn,
            details?.customer?.name);
    }

    private static UndocumentedAuctionDto? TryParseUndocumentedAuction(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UndocumentedAuctionDto>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
