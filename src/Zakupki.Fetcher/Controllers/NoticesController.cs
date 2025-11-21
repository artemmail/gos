using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
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
using Zakupki.Fetcher.Services;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NoticesController : ControllerBase
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly AttachmentDownloadService _attachmentDownloadService;
    private readonly AttachmentMarkdownService _attachmentMarkdownService;
    private readonly AttachmentContentExtractor _attachmentContentExtractor;
    private readonly NoticeAnalysisService _noticeAnalysisService;
    private readonly NoticeAnalysisReportService _noticeAnalysisReportService;
    private readonly ILogger<NoticesController> _logger;
    private readonly IFavoriteSearchQueueService _favoriteSearchQueueService;
    private readonly UserCompanyService _userCompanyService;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private static readonly char[] CodeSeparators = new[] { ',', ';', '\n', '\r', '\t', ' ' };
    private const string DefaultEmbeddingModel = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2";

    public NoticesController(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        AttachmentDownloadService attachmentDownloadService,
        AttachmentMarkdownService attachmentMarkdownService,
        AttachmentContentExtractor attachmentContentExtractor,
        NoticeAnalysisService noticeAnalysisService,
        NoticeAnalysisReportService noticeAnalysisReportService,
        IFavoriteSearchQueueService favoriteSearchQueueService,
        ILogger<NoticesController> logger,
        UserCompanyService userCompanyService)
    {
        _dbContextFactory = dbContextFactory;
        _attachmentDownloadService = attachmentDownloadService;
        _attachmentMarkdownService = attachmentMarkdownService;
        _attachmentContentExtractor = attachmentContentExtractor;
        _noticeAnalysisService = noticeAnalysisService;
        _noticeAnalysisReportService = noticeAnalysisReportService;
        _favoriteSearchQueueService = favoriteSearchQueueService;
        _logger = logger;
        _userCompanyService = userCompanyService;
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
        [FromQuery] DateTimeOffset? collectingEndLimit = null,
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

        var similarityThreshold = Math.Clamp(similarityThresholdPercent, 40, 90) / 100.0;
        // VECTOR_DISTANCE('cosine', ...) = 1 - cosine_similarity
        // similarity >= T  <=>  distance <= 1 - T
        var distanceThreshold = 1.0 - similarityThreshold;

        var normalizedCollectingEnd = (collectingEndLimit ?? DateTimeOffset.UtcNow).UtcDateTime;
        var offset = (page - 1) * pageSize;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Берём сохранённый вектор запроса
        var queryVectorEntity = await context.UserQueryVectors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == queryVectorId && v.UserId == currentUserId, cancellationToken);

        if (queryVectorEntity is null)
            return NotFound(new { message = "Запрос не найден" });

        if (queryVectorEntity.Vector is null)
            return BadRequest(new { message = "Вектор запроса ещё не готов" });

        var queryVector = queryVectorEntity.Vector.Value; // SqlVector<float>

        string[]? userRegions = null;

        if (filterByUserRegions)
        {
            userRegions = await GetUserRegionCodesAsync(currentUserId, cancellationToken);

            if (userRegions.Length == 0)
            {
                return BadRequest(new { message = "В профиле не указаны регионы для фильтрации." });
            }
        }

        // 2. Базовый запрос по NoticeEmbeddings c векторной дистанцией
        //    Всё на LINQ + EF.Functions.VectorDistance
        var embeddingsQuery = context.NoticeEmbeddings
            .AsNoTracking()
            .Where(e => e.Model == DefaultEmbeddingModel);

        if (userRegions is not null)
        {
            embeddingsQuery = ApplyRegionFilter(embeddingsQuery, userRegions);
        }

        var matchesQuery =
            from e in embeddingsQuery
            let distance = EF.Functions.VectorDistance("cosine", e.Vector, queryVector)
            where distance <= distanceThreshold
            orderby distance, e.UpdatedAt descending
            select new
            {
                e.NoticeId,
                Distance = distance,
                e.Notice.CollectingEnd
            };

        if (!expiredOnly)
        {
            matchesQuery = matchesQuery
                .Where(m => m.CollectingEnd == null || m.CollectingEnd > normalizedCollectingEnd);
        }

        // 3. Общее количество
        var totalCount = await matchesQuery.LongCountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return Ok(new PagedResult<NoticeListItemDto>(
                Array.Empty<NoticeListItemDto>(),
                0,
                page,
                pageSize));
        }

        // 4. Пагинация + выборка нужных NoticeId и distance
        var pageMatches = await matchesQuery
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var noticeIds = pageMatches
            .Select(m => m.NoticeId)
            .ToList();

        // Считаем similarity в памяти: similarity = 1 - distance
        var orderLookup = pageMatches
            .Select((m, index) => new
            {
                m.NoticeId,
                Index = index,
                Similarity = (double?)(1.0 - m.Distance)
            })
            .ToDictionary(
                x => x.NoticeId,
                x => (x.Index, x.Similarity)
            );

        var includeFavorites = !string.IsNullOrEmpty(currentUserId);

        // 5. Дотягиваем все остальные поля Notice и аналитику, как у тебя было
        var noticeRows = await context.Notices
            .AsNoTracking()
            .Where(n => noticeIds.Contains(n.Id))
            .Select(n => new
            {
                Notice = n,
                ProcedureSubmissionDate = n.Versions
                    .Where(v => v.IsActive)
                    .Select(v => v.ProcedureWindow != null
                        ? (string?)v.ProcedureWindow.SubmissionProcedureDateRaw
                        : null)
                    .FirstOrDefault(),
                Analysis = n.Analyses
                    .Where(a => currentUserId != null && a.UserId == currentUserId)
                    .OrderByDescending(a => a.UpdatedAt)
                    .Select(a => new
                    {
                        a.Status,
                        a.UpdatedAt,
                        a.CompletedAt,
                        HasResult = a.Result != null && a.Result != ""
                    })
                    .FirstOrDefault(),
                IsFavorite = includeFavorites && n.Favorites.Any(f => f.UserId == currentUserId)
            })
            .ToListAsync(cancellationToken);

        // 6. Собираем DTO и восстанавливаем порядок по индексу из orderLookup
        var items = noticeRows
            .Select(x => new NoticeListItemDto(
                x.Notice.Id,
                x.Notice.PurchaseNumber,
                x.Notice.PublishDate,
                x.Notice.EtpName,
                x.Notice.Region,
                x.Notice.PurchaseObjectInfo,
                x.Notice.MaxPrice,
                x.Notice.Okpd2Code,
                x.Notice.Okpd2Name,
                x.Notice.KvrCode,
                x.Notice.KvrName,
                x.Notice.RawJson,
                x.Notice.CollectingEnd,
                x.ProcedureSubmissionDate,
                x.Analysis != null &&
                x.Analysis.Status == NoticeAnalysisStatus.Completed &&
                x.Analysis.HasResult,
                x.Analysis != null ? x.Analysis.Status : null,
                x.Analysis != null ? (DateTime?)x.Analysis.UpdatedAt : null,
                x.IsFavorite,
                orderLookup.TryGetValue(x.Notice.Id, out var ordering) ? ordering.Similarity : null
            ))
            .OrderBy(item => orderLookup[item.Id].Item1)
            .ToList();

        var total = (int)Math.Min(int.MaxValue, totalCount);
        var result = new PagedResult<NoticeListItemDto>(items, total, page, pageSize);

        return Ok(result);
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

        var normalizedSortField = string.IsNullOrWhiteSpace(sortField)
            ? "publishDate"
            : sortField.Trim().ToLowerInvariant();

        var normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? "desc"
            : sortDirection.Trim().ToLowerInvariant();

        query = ApplySorting(query, normalizedSortField, normalizedSortDirection);

        var totalCount = await query.CountAsync();
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(n => new
            {
                Notice = n,
                ProcedureSubmissionDate = n.Versions
                    .Where(v => v.IsActive)
                    .Select(v => v.ProcedureWindow != null
                        ? (string?)v.ProcedureWindow.SubmissionProcedureDateRaw
                        : null)
                    .FirstOrDefault(),
                Analysis = n.Analyses
                    .Where(a => currentUserId != null && a.UserId == currentUserId)
                    .OrderByDescending(a => a.UpdatedAt)
                    .Select(a => new
                    {
                        a.Status,
                        a.UpdatedAt,
                        a.CompletedAt,
                        HasResult = a.Result != null && a.Result != ""
                    })
                    .FirstOrDefault(),
                IsFavorite = includeFavorites && n.Favorites.Any(f => f.UserId == currentUserId)
            })
            .Select(x => new NoticeListItemDto(
                x.Notice.Id,
                x.Notice.PurchaseNumber,
                x.Notice.PublishDate,
                x.Notice.EtpName,
                x.Notice.Region,
                x.Notice.PurchaseObjectInfo,
                x.Notice.MaxPrice,
                x.Notice.Okpd2Code,
                x.Notice.Okpd2Name,
                x.Notice.KvrCode,
                x.Notice.KvrName,
                x.Notice.RawJson,
                x.Notice.CollectingEnd,
                x.ProcedureSubmissionDate,
                x.Analysis != null && x.Analysis.Status == NoticeAnalysisStatus.Completed && x.Analysis.HasResult,
                x.Analysis != null ? x.Analysis.Status : null,
                x.Analysis != null ? (DateTime?)x.Analysis.UpdatedAt : null,
                x.IsFavorite,
                null))
            .ToListAsync();

        var result = new PagedResult<NoticeListItemDto>(items, totalCount, page, pageSize);
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

        query = ApplySorting(query, normalizedSortField, normalizedSortDirection);

        var totalCount = await query.CountAsync();
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(n => new
            {
                Notice = n,
                ProcedureSubmissionDate = n.Versions
                    .Where(v => v.IsActive)
                    .Select(v => v.ProcedureWindow != null
                        ? (string?)v.ProcedureWindow.SubmissionProcedureDateRaw
                        : null)
                    .FirstOrDefault(),
                Analysis = n.Analyses
                    .Where(a => a.UserId == currentUserId)
                    .OrderByDescending(a => a.UpdatedAt)
                    .Select(a => new
                    {
                        a.Status,
                        a.UpdatedAt,
                        a.CompletedAt,
                        HasResult = a.Result != null && a.Result != ""
                    })
                    .FirstOrDefault()
            })
            .Select(x => new NoticeListItemDto(
                x.Notice.Id,
                x.Notice.PurchaseNumber,
                x.Notice.PublishDate,
                x.Notice.EtpName,
                x.Notice.Region,
                x.Notice.PurchaseObjectInfo,
                x.Notice.MaxPrice,
                x.Notice.Okpd2Code,
                x.Notice.Okpd2Name,
                x.Notice.KvrCode,
                x.Notice.KvrName,
                x.Notice.RawJson,
                x.Notice.CollectingEnd,
                x.ProcedureSubmissionDate,
                x.Analysis != null && x.Analysis.Status == NoticeAnalysisStatus.Completed && x.Analysis.HasResult,
                x.Analysis != null ? x.Analysis.Status : null,
                x.Analysis != null ? (DateTime?)x.Analysis.UpdatedAt : null,
                true,
                null))
            .ToListAsync();

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
            .Where(a => a.NoticeVersion.NoticeId == noticeId)
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
            .Where(a => a.NoticeVersion.NoticeId == noticeId && a.BinaryContent == null)
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
            .Where(a => a.NoticeVersion.NoticeId == noticeId)
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

    private static IQueryable<Notice> ApplyRegionFilter(IQueryable<Notice> query, IReadOnlyCollection<string> regions)
    {
        var regionCodes = NormalizeRegions(regions);

        if (regionCodes.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(n => n.Region != null && regionCodes.Contains(n.Region));
    }

    private static IQueryable<NoticeEmbedding> ApplyRegionFilter(
        IQueryable<NoticeEmbedding> query,
        IReadOnlyCollection<string> regions)
    {
        var regionCodes = NormalizeRegions(regions);

        if (regionCodes.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(e => e.Notice.Region != null && regionCodes.Contains(e.Notice.Region));
    }

    private static string[] NormalizeRegions(IReadOnlyCollection<string> regions) =>
        regions
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
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

    private static IQueryable<Notice> ApplySorting(IQueryable<Notice> query, string sortField, string sortDirection)
    {
        var descending = sortDirection == "desc";

        return sortField switch
        {
            "purchasenumber" => descending
                ? query.OrderByDescending(n => n.PurchaseNumber)
                : query.OrderBy(n => n.PurchaseNumber),
            "etpname" => descending
                ? query.OrderByDescending(n => n.EtpName)
                : query.OrderBy(n => n.EtpName),
            "region" => descending
                ? query.OrderByDescending(n => n.Region)
                : query.OrderBy(n => n.Region),
            "purchaseobjectinfo" => descending
                ? query.OrderByDescending(n => n.PurchaseObjectInfo)
                : query.OrderBy(n => n.PurchaseObjectInfo),
            "okpd2code" => descending
                ? query.OrderByDescending(n => n.Okpd2Code)
                : query.OrderBy(n => n.Okpd2Code),
            "okpd2name" => descending
                ? query.OrderByDescending(n => n.Okpd2Name)
                : query.OrderBy(n => n.Okpd2Name),
            "kvrcode" => descending
                ? query.OrderByDescending(n => n.KvrCode)
                : query.OrderBy(n => n.KvrCode),
            "kvrname" => descending
                ? query.OrderByDescending(n => n.KvrName)
                : query.OrderBy(n => n.KvrName),
            "maxprice" => descending
                ? query.OrderByDescending(n => n.MaxPrice)
                : query.OrderBy(n => n.MaxPrice),
            "collectingend" => descending
                ? query.OrderByDescending(n => n.CollectingEnd)
                : query.OrderBy(n => n.CollectingEnd),
            _ => descending
                ? query.OrderByDescending(n => n.PublishDate)
                    .ThenByDescending(n => n.Id)
                : query.OrderBy(n => n.PublishDate)
                    .ThenBy(n => n.Id)
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
}

public record NoticeListItemDto(
    Guid Id,
    string PurchaseNumber,
    DateTime? PublishDate,
    string? EtpName,
    string? Region,
    string? PurchaseObjectInfo,
    decimal? MaxPrice,
    string? Okpd2Code,
    string? Okpd2Name,
    string? KvrCode,
    string? KvrName,
    string? RawJson,
    DateTime? CollectingEnd,
    string? SubmissionProcedureDateRaw,
    bool HasAnalysisAnswer,
    string? AnalysisStatus,
    DateTime? AnalysisUpdatedAt,
    bool IsFavorite,
    double? Similarity);

public record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);

public record NoticeAttachmentDto(
    Guid Id,
    string PublishedContentId,
    string FileName,
    long? FileSize,
    string? Description,
    DateTime? DocumentDate,
    string? DocumentKindCode,
    string? DocumentKindName,
    string? Url,
    string? SourceFileName,
    DateTime InsertedAt,
    DateTime LastSeenAt,
    bool HasBinaryContent,
    bool HasMarkdownContent);

public record AttachmentDownloadResultDto(int Total, int Downloaded, int Failed);

public record AttachmentMarkdownConversionResultDto(
    int Total,
    int Converted,
    int MissingContent,
    int Unsupported,
    int Failed);

