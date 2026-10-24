using Auctions.Bll.DTO;
using Auctions.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auctions.Api.Controllers;

/// <summary>Учасники торгів та їх реєстрація на аукціони.</summary>
[ApiController]
[Route("api/bidders")]
[Produces("application/json")]
public class BiddersController : ControllerBase
{
    private readonly IBidderService _bidderService;

    public BiddersController(IBidderService bidderService)
    {
        _bidderService = bidderService;
    }

    /// <summary>Список учасників.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BidderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BidderDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _bidderService.GetAllAsync(cancellationToken));

    /// <summary>Учасник за ідентифікатором.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<BidderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BidderDto>> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await _bidderService.GetByIdAsync(id, cancellationToken));

    /// <summary>Створює учасника торгів.</summary>
    [HttpPost]
    [ProducesResponseType<BidderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BidderDto>> Create(CreateBidderDto dto, CancellationToken cancellationToken)
    {
        var created = await _bidderService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.BidderId }, created);
    }

    /// <summary>Реєструє учасника на аукціон і блокує депозит у його гаманці (одна транзакція).</summary>
    [HttpPost("{id:long}/registrations")]
    [ProducesResponseType<RegistrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistrationDto>> RegisterForAuction(
        long id,
        RegisterForAuctionDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _bidderService.RegisterForAuctionAsync(id, dto, cancellationToken));
}
