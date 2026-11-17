using Auctions.Api.IntegrationEvents.Events;
using Auctions.Bll.DTO;
using Auctions.Bll.IntegrationEvents;
using Platform.EventBus.Abstractions;
using Platform.ServiceDefaults;

namespace Auctions.Api.IntegrationEvents;

// Будує integration events сервісу аукціонів і публікує їх через IEventPublisher.
// Зміну вже закомічено — збій публікації не повертається клієнту як 500, а логується як Error
// з бізнес-контекстом (подію втрачено; гарантію дав би Transactional Outbox).
public sealed class AuctionsIntegrationEventService(
    IEventPublisher publisher,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuctionsIntegrationEventService> logger) : IAuctionsIntegrationEventService
{
    public async Task PublishAuctionStatusChangedAsync(AuctionDto auction, string? oldStatus, DateTime changedAt, CancellationToken cancellationToken = default)
    {
        var @event = new AuctionStatusChangedIntegrationEvent(
            auction.AuctionId, auction.ItemId, auction.ItemTitle, oldStatus, auction.Status,
            auction.CurrentPrice, auction.BidCount, auction.LeadingBidderId)
        {
            OccurredAt = changedAt,
            CorrelationId = CurrentCorrelationId()
        };

        try
        {
            await publisher.PublishAsync(@event, AuctionsEventBus.Exchange,
                AuctionsEventBus.AuctionStatusRoutingKey(auction.Status), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Auction {AuctionId} status {OldStatus} -> {NewStatus} was saved, but {EventType} {EventId} was NOT published; " +
                "item {ItemId} status in Catalog stays stale",
                auction.AuctionId, oldStatus, auction.Status, nameof(AuctionStatusChangedIntegrationEvent), @event.EventId, auction.ItemId);
        }
    }

    private string? CurrentCorrelationId()
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
