using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Clients;
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
    /// <param name="id">Ідентифікатор аукціону.</param>
    /// <param name="transport">grpc (за замовчуванням) або http — для порівняння затримок.</param>
    /// <param name="noCache">true — не читати й не писати кеш картки в Redis (кеші сервісів працюють як завжди).</param>
    /// <param name="ct">Скасування запиту клієнтом.</param>
    [HttpGet("{id:long}/full")]
    [ProducesResponseType(typeof(AuctionCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuctionCardDto>> GetFull(long id, CancellationToken ct,
        [FromQuery] string transport = GrpcCardDataSource.Name, [FromQuery] bool noCache = false)
    {
        transport = transport.Trim().ToLowerInvariant();
        if (transport is not (GrpcCardDataSource.Name or HttpCardDataSource.Name))
        {
            ModelState.AddModelError(nameof(transport), "transport must be 'grpc' or 'http'.");
            return ValidationProblem(ModelState);
        }

        logger.LogInformation("Auction card requested for auction {AuctionId} via {Transport}, cache {CacheMode}",
            id, transport, noCache ? "bypass" : "on");

        var result = await cardService.GetAuctionCardAsync(id, transport, useCache: !noCache, ct);

        // Для демонстрації: яким транспортом зібрано картку і чи прийшла вона з кешу.
        Response.Headers["X-Aggregator-Transport"] = result.Transport;
        Response.Headers["X-Aggregator-Cache"] = noCache ? "bypass" : result.FromCache ? "hit" : "miss";

        if (result.IsNotFound)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Auction not found",
                detail: $"Auction {id} does not exist.");

        if (result.UnavailableReason is not null)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service unavailable",
                detail: result.UnavailableReason);

        return Ok(result.Card);
    }
}
