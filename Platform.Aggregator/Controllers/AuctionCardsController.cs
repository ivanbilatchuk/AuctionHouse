using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Dto;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

/// <summary>Композитні відповіді для клієнта: дані кількох сервісів в одному запиті.</summary>
[ApiController]
[Route("api/aggregator/auctions")]
[Produces("application/json")]
public sealed class AuctionCardsController(AuctionCardService cardService, ILogger<AuctionCardsController> logger) : ControllerBase
{
    /// <summary>
    /// Повна картка аукціону: аукціон, лот із каталогу, висновки експертів і останні ставки.
    /// Якщо Catalog або Community недоступні — 200 з попередженням у warnings.
    /// </summary>
    [HttpGet("{id:long}/full")]
    [ProducesResponseType(typeof(AuctionCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuctionCardDto>> GetFull(long id, CancellationToken ct)
    {
        logger.LogInformation("Auction card requested for auction {AuctionId}", id);

        var result = await cardService.GetAuctionCardAsync(id, ct);

        if (result.IsNotFound)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Auction not found",
                detail: $"Auction {id} does not exist.");

        if (result.UnavailableReason is not null)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service unavailable",
                detail: result.UnavailableReason);

        return Ok(result.Card);
    }
}
