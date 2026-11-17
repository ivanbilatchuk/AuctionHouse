using Auctions.Api.IntegrationEvents.Events;
using Auctions.Bll.Caching;
using Auctions.Dal.Interfaces;
using Auctions.Domain.Exceptions;
using Platform.EventBus.Abstractions;

namespace Auctions.Api.IntegrationEvents.EventHandling;

// Політика дублювання №1: Auction.ItemTitle — копія Catalog.Item.Title (знімок під час створення + події ItemUpdated).
// Ідемпотентний обробник, одна транзакція Unit of Work:
//   1) INSERT у dbo.ProcessedEvents; EventId уже є -> дублікат, відкат і пропуск;
//   2) UPDATE dbo.Auction SET ItemTitle ... WHERE ItemId = @ItemId AND ItemTitleSyncedAt < OccurredAt (out-of-order);
//   3) COMMIT — EventId і зміна назви фіксуються разом; збій посередині — відкат обох, подію буде повторено.
// Ціни аукціонів і ставок подія не змінює.
public sealed class ItemUpdatedIntegrationEventHandler(
    IUnitOfWork uow,
    TwoLevelCache cache,
    ILogger<ItemUpdatedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ItemUpdatedIntegrationEvent>
{
    private const int MaxTitleLength = 200;

    public async Task HandleAsync(ItemUpdatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        if (@event.ItemId <= 0 || string.IsNullOrWhiteSpace(@event.Title) || @event.Title.Length > MaxTitleLength)
            throw new ValidationException(
                $"{nameof(ItemUpdatedIntegrationEvent)} {@event.EventId} has invalid data: itemId {@event.ItemId}, title length {@event.Title?.Length ?? 0}.");

        IReadOnlyList<long> updatedAuctionIds;

        await uow.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, nameof(ItemUpdatedIntegrationEvent), cancellationToken))
            {
                await uow.RollbackAsync(CancellationToken.None);
                logger.LogInformation("Duplicate {EventType} {EventId} for item {ItemId} skipped",
                    nameof(ItemUpdatedIntegrationEvent), @event.EventId, @event.ItemId);
                return;
            }

            updatedAuctionIds = await uow.Auctions.UpdateItemTitleAsync(@event.ItemId, @event.Title.Trim(), @event.OccurredAt, cancellationToken);
            await uow.CommitAsync(cancellationToken);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (updatedAuctionIds.Count > 0)
            await cache.InvalidateAsync(updatedAuctionIds.Select(AuctionsCacheKeys.Auction), $"item {@event.ItemId} renamed", cancellationToken);

        logger.LogInformation(
            "Item {ItemId} title synced {PreviousTitle} -> {Title}: {UpdatedCount} auctions updated {AuctionIds} (occurred at {OccurredAt:O})",
            @event.ItemId, @event.PreviousTitle, @event.Title, updatedAuctionIds.Count, updatedAuctionIds, @event.OccurredAt);
    }
}
