using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
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
    private readonly NoticeAnalysisService _noticeAnalysisService;
    private readonly ILogger<NoticesController> _logger;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private static readonly char[] CodeSeparators = new[] { ',', ';', '\n', '\r', '\t', ' ' };

    public NoticesController(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        AttachmentDownloadService attachmentDownloadService,
        AttachmentMarkdownService attachmentMarkdownService,
        NoticeAnalysisService noticeAnalysisService,
        ILogger<NoticesController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _attachmentDownloadService = attachmentDownloadService;
        _attachmentMarkdownService = attachmentMarkdownService;
        _noticeAnalysisService = noticeAnalysisService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NoticeListItemDto>>> GetNotices(
        [FromQuery] string? search,
        [FromQuery] string? purchaseNumber,
        [FromQuery] string? okpd2Codes,
        [FromQuery] string? kvrCodes,
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
        var query = context.Notices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            var likeTerm = $"%{trimmedSearch}%";
            query = query.Where(n =>
                EF.Functions.Like(n.PurchaseNumber, likeTerm) ||
                EF.Functions.Like(n.EntryName, likeTerm) ||
                (n.EtpName != null && EF.Functions.Like(n.EtpName, likeTerm)) ||
                (n.DocumentNumber != null && EF.Functions.Like(n.DocumentNumber, likeTerm)) ||
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

        var normalizedSortField = string.IsNullOrWhiteSpace(sortField)
            ? "publishDate"
            : sortField.Trim().ToLowerInvariant();

        var normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? "desc"
            : sortDirection.Trim().ToLowerInvariant();

        query = ApplySorting(query, normalizedSortField, normalizedSortDirection);

        var totalCount = await query.CountAsync();
        var skip = (page - 1) * pageSize;

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

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
                    .FirstOrDefault()
            })
            .Select(x => new NoticeListItemDto(
                x.Notice.Id,
                x.Notice.PurchaseNumber,
                x.Notice.EntryName,
                x.Notice.PublishDate,
                x.Notice.EtpName,
                x.Notice.DocumentType,
                x.Notice.Source,
                x.Notice.UpdatedAt,
                x.Notice.Region,
                x.Notice.Period,
                x.Notice.PlacingWayName,
                x.Notice.PurchaseObjectInfo,
                x.Notice.MaxPrice,
                x.Notice.MaxPriceCurrencyCode,
                x.Notice.MaxPriceCurrencyName,
                x.Notice.Okpd2Code,
                x.Notice.Okpd2Name,
                x.Notice.KvrCode,
                x.Notice.KvrName,
                x.Notice.RawJson,
                x.Notice.CollectingEnd,
                x.ProcedureSubmissionDate,
                x.Analysis != null && x.Analysis.Status == NoticeAnalysisStatus.Completed && x.Analysis.HasResult,
                x.Analysis != null ? x.Analysis.Status : null,
                x.Analysis != null ? (DateTime?)x.Analysis.UpdatedAt : null))
            .ToListAsync();

        var result = new PagedResult<NoticeListItemDto>(items, totalCount, page, pageSize);
        return Ok(result);
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
            UpdateAttachmentContent(attachment, content);
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
                UpdateAttachmentContent(attachment, content);
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

            if (!_attachmentMarkdownService.IsSupported(attachment))
            {
                unsupported++;
                continue;
            }

            try
            {
                var markdown = await _attachmentMarkdownService.ConvertToMarkdownAsync(attachment, cancellationToken);
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

    private static void UpdateAttachmentContent(NoticeAttachment attachment, byte[] content)
    {
        attachment.BinaryContent = content;
        attachment.FileSize = content.LongLength;
        attachment.ContentHash = HashUtilities.ComputeSha256Hex(content);
        attachment.LastSeenAt = DateTime.UtcNow;
        attachment.MarkdownContent = null;
    }

    private static IQueryable<Notice> ApplySorting(IQueryable<Notice> query, string sortField, string sortDirection)
    {
        var descending = sortDirection == "desc";

        return sortField switch
        {
            "purchasenumber" => descending
                ? query.OrderByDescending(n => n.PurchaseNumber)
                : query.OrderBy(n => n.PurchaseNumber),
            "entryname" => descending
                ? query.OrderByDescending(n => n.EntryName)
                : query.OrderBy(n => n.EntryName),
            "etpname" => descending
                ? query.OrderByDescending(n => n.EtpName)
                : query.OrderBy(n => n.EtpName),
            "documenttype" => descending
                ? query.OrderByDescending(n => n.DocumentType)
                : query.OrderBy(n => n.DocumentType),
            "source" => descending
                ? query.OrderByDescending(n => n.Source)
                : query.OrderBy(n => n.Source),
            "updatedat" => descending
                ? query.OrderByDescending(n => n.UpdatedAt)
                : query.OrderBy(n => n.UpdatedAt),
            "region" => descending
                ? query.OrderByDescending(n => n.Region)
                : query.OrderBy(n => n.Region),
            "period" => descending
                ? query.OrderByDescending(n => n.Period)
                : query.OrderBy(n => n.Period),
            "placingwayname" => descending
                ? query.OrderByDescending(n => n.PlacingWayName)
                : query.OrderBy(n => n.PlacingWayName),
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
    string EntryName,
    DateTime? PublishDate,
    string? EtpName,
    string DocumentType,
    string Source,
    DateTime UpdatedAt,
    string? Region,
    string? Period,
    string? PlacingWayName,
    string? PurchaseObjectInfo,
    decimal? MaxPrice,
    string? MaxPriceCurrencyCode,
    string? MaxPriceCurrencyName,
    string? Okpd2Code,
    string? Okpd2Name,
    string? KvrCode,
    string? KvrName,
    string? RawJson,
    DateTime? CollectingEnd,
    string? SubmissionProcedureDateRaw,
    bool HasAnalysisAnswer,
    string? AnalysisStatus,
    DateTime? AnalysisUpdatedAt);

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

