using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Services;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QueryVectorsController : ControllerBase
{
    private readonly IQueryVectorQueueService _queryVectorQueueService;

    public QueryVectorsController(IQueryVectorQueueService queryVectorQueueService)
    {
        _queryVectorQueueService = queryVectorQueueService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserQueryVectorDto>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var entries = await _queryVectorQueueService.GetAllAsync(userId, cancellationToken);
        return Ok(entries.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<UserQueryVectorDto>> Create([FromBody] CreateUserQueryVectorRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { message = "Укажите запрос" });
        }

        if (request.Query.Length > 4000)
        {
            return BadRequest(new { message = "Запрос слишком длинный (максимум 4000 символов)" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var entity = await _queryVectorQueueService.CreateAsync(userId, request, cancellationToken);
            return Ok(ToDto(entity));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var removed = await _queryVectorQueueService.DeleteAsync(userId, id, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static UserQueryVectorDto ToDto(UserQueryVector entity)
    {
        float[]? vector = null;
        
        if (entity.Vector != null)
            vector = entity.Vector.Value.Memory.ToArray();

        return new UserQueryVectorDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Query = entity.Query,
            Vector = vector,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CompletedAt = entity.CompletedAt
        };
    }
}
