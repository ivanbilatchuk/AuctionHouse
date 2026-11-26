using Auctions.Api.Metrics;
using Auctions.Bll.DTO;
using Auctions.Bll.Services;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Security;

namespace Auctions.Api.Controllers;

/// <summary>Ставки на конкретний аукціон.</summary>
[ApiController]
[Route("api/auctions/{auctionId:long}/bids")]
[Produces("application/json")]
public class BidsController : ControllerBase
{
    private readonly IBidService _bidService;
    private readonly AuctionsMetrics _metrics;

    public BidsController(IBidService bidService, AuctionsMetrics metrics)
    {
        _bidService = bidService;
        _metrics = metrics;
    }

    /// <summary>Усі ставки аукціону від найвищої до найнижчої.</summary>
    [RequirePermission(Permissions.Auctions.Read)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BidDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> GetByAuction(long auctionId, CancellationToken cancellationToken) =>
        Ok(await _bidService.GetByAuctionAsync(auctionId, cancellationToken));

    /// <summary>Ставка за ідентифікатором.</summary>
    [RequirePermission(Permissions.Auctions.Read)]
    [HttpGet("{bidId:long}")]
    [ProducesResponseType<BidDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BidDto>> GetById(long auctionId, long bidId, CancellationToken cancellationToken) =>
        Ok(await _bidService.GetByIdAsync(auctionId, bidId, cancellationToken));

    /// <summary>Робить ставку (транзакція над Auctions + Bidders + Bids).</summary>
    [RequirePermission(Permissions.Auctions.Create)]
    [HttpPost]
    [ProducesResponseType<BidDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BidDto>> PlaceBid(long auctionId, PlaceBidDto dto, CancellationToken cancellationToken)
    {
        var bid = await _metrics.MeasureAsync("place_bid",
            () => _bidService.PlaceBidAsync(auctionId, dto, cancellationToken));
        _metrics.BidPlaced(bid.IsWinning, bid.Amount);
        return CreatedAtAction(nameof(GetById), new { auctionId, bidId = bid.BidId }, bid);
    }
}
