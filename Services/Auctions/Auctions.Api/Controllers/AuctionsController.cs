using Auctions.Bll.DTO;
using Auctions.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auctions.Api.Controllers;

/// <summary>Аукціони: перегляд, створення, редагування, зміна статусу та видалення.</summary>
[ApiController]
[Route("api/auctions")]
[Produces("application/json")]
public class AuctionsController : ControllerBase
{
    private readonly IAuctionService _auctionService;

    public AuctionsController(IAuctionService auctionService)
    {
        _auctionService = auctionService;
    }

    /// <summary>Список аукціонів (необов'язковий фільтр за статусом).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuctionSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AuctionSummaryDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _auctionService.GetAllAsync(status, cancellationToken));

    /// <summary>Аукціон за ідентифікатором разом із параметрами торгів.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuctionDto>> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await _auctionService.GetByIdAsync(id, cancellationToken));

    /// <summary>Створює аукціон у статусі Draft.</summary>
    [HttpPost]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuctionDto>> Create(CreateAuctionDto dto, CancellationToken cancellationToken)
    {
        var created = await _auctionService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.AuctionId }, created);
    }

    /// <summary>Редагує аукціон (лише Draft/Scheduled без ставок). RowVersion береться з GET.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuctionDto>> Update(long id, UpdateAuctionDto dto, CancellationToken cancellationToken) =>
        Ok(await _auctionService.UpdateAsync(id, dto, cancellationToken));

    /// <summary>Змінює статус аукціону за матрицею дозволених переходів (збережена процедура).</summary>
    [HttpPost("{id:long}/status")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuctionDto>> ChangeStatus(
        long id,
        ChangeAuctionStatusDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _auctionService.ChangeStatusAsync(id, dto, cancellationToken));

    /// <summary>М'яко видаляє аукціон (лише Draft/Cancelled без ставок).</summary>
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
