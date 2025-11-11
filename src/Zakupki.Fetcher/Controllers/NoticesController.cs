using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NoticesController : ControllerBase
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    public NoticesController(IDbContextFactory<NoticeDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
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
                (n.DocumentNumber != null && EF.Functions.Like(n.DocumentNumber, likeTerm)));
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
                n.PlacingWayName))
            .ToListAsync();

        var result = new PagedResult<NoticeListItemDto>(items, totalCount, page, pageSize);
        return Ok(result);
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
            _ => descending
                ? query.OrderByDescending(n => n.PublishDate)
                    .ThenByDescending(n => n.Id)
                : query.OrderBy(n => n.PublishDate)
                    .ThenBy(n => n.Id)
        };
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
    string? PlacingWayName);

public record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);
