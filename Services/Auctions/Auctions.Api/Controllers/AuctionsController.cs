using Auctions.Api.Metrics;
using Auctions.Bll.DTO;
using Auctions.Bll.Services;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Security;

namespace Auctions.Api.Controllers;

/// <summary>Аукціони: перегляд, створення, редагування, зміна статусу та видалення.</summary>
[ApiController]
[Route("api/auctions")]
[Produces("application/json")]
public class AuctionsController : ControllerBase
{
    private readonly IAuctionService _auctionService;
    private readonly AuctionsMetrics _metrics;

    public AuctionsController(IAuctionService auctionService, AuctionsMetrics metrics)
    {
        _auctionService = auctionService;
        _metrics = metrics;
    }

    /// <summary>Список аукціонів (необов'язковий фільтр за статусом).</summary>
    [RequirePermission(Permissions.Auctions.Read)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuctionSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AuctionSummaryDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _auctionService.GetAllAsync(status, cancellationToken));

    /// <summary>Аукціон за ідентифікатором разом із параметрами торгів.</summary>
    [RequirePermission(Permissions.Auctions.Read)]
    [HttpGet("{id:long}")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuctionDto>> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await _auctionService.GetByIdAsync(id, cancellationToken));

    /// <summary>Створює аукціон у статусі Draft.</summary>
    [RequirePermission(Permissions.Auctions.Create)]
    [HttpPost]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuctionDto>> Create(CreateAuctionDto dto, CancellationToken cancellationToken)
    {
        var created = await _metrics.MeasureAsync("create_auction",
            () => _auctionService.CreateAsync(dto, cancellationToken));
        _metrics.AuctionCreated(created.Status);
        return CreatedAtAction(nameof(GetById), new { id = created.AuctionId }, created);
    }

    /// <summary>Редагує аукціон (лише Draft/Scheduled без ставок). RowVersion береться з GET.</summary>
    [RequirePermission(Permissions.Auctions.Update)]
    [HttpPut("{id:long}")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuctionDto>> Update(long id, UpdateAuctionDto dto, CancellationToken cancellationToken) =>
        Ok(await _auctionService.UpdateAsync(id, dto, cancellationToken));

    /// <summary>Змінює статус аукціону за матрицею дозволених переходів (збережена процедура).</summary>
    [RequirePermission(Permissions.Auctions.Approve)]
    [HttpPost("{id:long}/status")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuctionDto>> ChangeStatus(
        long id,
        ChangeAuctionStatusDto dto,
        CancellationToken cancellationToken)
    {
        var changed = await _metrics.MeasureAsync("change_status",
            () => _auctionService.ChangeStatusAsync(id, dto, cancellationToken));
        _metrics.AuctionStatusChanged(changed.Status);
        return Ok(changed);
    }

    /// <summary>М'яко видаляє аукціон (лише Draft/Cancelled без ставок).</summary>
    [RequirePermission(Permissions.Auctions.Delete)]
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _auctionService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
