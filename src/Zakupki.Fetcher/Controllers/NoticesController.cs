using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Services;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NoticesController : ControllerBase
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly AttachmentDownloadService _attachmentDownloadService;
    private readonly ILogger<NoticesController> _logger;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public NoticesController(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        AttachmentDownloadService attachmentDownloadService,
        ILogger<NoticesController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _attachmentDownloadService = attachmentDownloadService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NoticeListItemDto>>> GetNotices(
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
            .Select(n => new NoticeListItemDto(
                n.Id,
                n.PurchaseNumber,
                n.EntryName,
                n.PublishDate,
                n.EtpName,
                n.DocumentType,
                n.Source,
                n.UpdatedAt,
                n.Region,
                n.Period,
                n.PlacingWayName,
                n.PurchaseObjectInfo,
                n.MaxPrice,
                n.MaxPriceCurrencyCode,
                n.MaxPriceCurrencyName,
                n.Okpd2Code,
                n.Okpd2Name,
                n.KvrCode,
                n.KvrName,
                n.RawJson))
            .ToListAsync();

        var result = new PagedResult<NoticeListItemDto>(items, totalCount, page, pageSize);
        return Ok(result);
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
                a.BinaryContent != null))
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
            attachment.BinaryContent != null);
    }

    private static void UpdateAttachmentContent(NoticeAttachment attachment, byte[] content)
    {
        attachment.BinaryContent = content;
        attachment.FileSize = content.LongLength;
        attachment.ContentHash = HashUtilities.ComputeSha256Hex(content);
        attachment.LastSeenAt = DateTime.UtcNow;
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
    string? RawJson);

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
    bool HasBinaryContent);

public record AttachmentDownloadResultDto(int Total, int Downloaded, int Failed);

