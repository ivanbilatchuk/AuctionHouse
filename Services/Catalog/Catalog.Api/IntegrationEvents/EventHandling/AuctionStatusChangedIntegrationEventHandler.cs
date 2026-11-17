using Catalog.Api.IntegrationEvents.Events;
using Catalog.Bll.Caching;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Platform.EventBus.Abstractions;

namespace Catalog.Api.IntegrationEvents.EventHandling;

// Політика дублювання №4: Catalog.Item.Status (InAuction / Sold) — проєкція статусу аукціону (власник — Auctions).
// Ідемпотентний обробник:
//   1) EventId уже в dbo.ProcessedEvents -> дублікат, пропуск;
//   2) Item.ApplyAuctionStatus — доменне правило переходу + захист від out-of-order (AuctionStatusSyncedAt);
//   3) запис ProcessedEvents і зміна Item — ОДИН SaveChangesAsync, тобто одна транзакція;
//   4) після коміту — інвалідація кешу картки предмета.
public sealed class AuctionStatusChangedIntegrationEventHandler(
    IUnitOfWork uow,
    TwoLevelCache cache,
    TimeProvider timeProvider,
    ILogger<AuctionStatusChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<AuctionStatusChangedIntegrationEvent>
{
    public async Task HandleAsync(AuctionStatusChangedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        if (await uow.ProcessedEvents.ExistsAsync(@event.EventId, cancellationToken))
        {
            logger.LogInformation("Duplicate {EventType} {EventId} for auction {AuctionId} / item {ItemId} skipped",
                nameof(AuctionStatusChangedIntegrationEvent), @event.EventId, @event.AuctionId, @event.ItemId);
            return;
        }

        var item = await uow.Items.GetByIdAsync(@event.ItemId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Item), @event.ItemId);
        var previousStatus = item.Status;

        var applied = item.ApplyAuctionStatus(@event.NewStatus, @event.OccurredAt);
        uow.ProcessedEvents.Add(@event.EventId, nameof(AuctionStatusChangedIntegrationEvent), timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            await uow.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException ex) when (ex.InnerException is not DbUpdateConcurrencyException)
        {
            // PK ProcessedEvents: ту саму подію щойно обробив інший екземпляр сервісу (competing consumers).
            logger.LogInformation("Duplicate {EventType} {EventId} detected on save (already processed concurrently)",
                nameof(AuctionStatusChangedIntegrationEvent), @event.EventId);
            return;
        }

        if (!applied)
        {
            logger.LogInformation(
                "Stale {EventType} {EventId} ignored: auction {AuctionId} status {NewStatus} occurred at {OccurredAt:O}, item {ItemId} already synced at {SyncedAt:O}",
                nameof(AuctionStatusChangedIntegrationEvent), @event.EventId, @event.AuctionId, @event.NewStatus,
                @event.OccurredAt, @event.ItemId, item.AuctionStatusSyncedAt);
            return;
        }

        await cache.InvalidateAsync(CatalogCacheKeys.Item(item.Id), $"auction {@event.AuctionId} status {@event.NewStatus}", cancellationToken);

        logger.LogInformation(
            "Item {ItemId} status projected from auction {AuctionId} ({OldAuctionStatus} -> {NewAuctionStatus}): {PreviousStatus} -> {Status}",
            item.Id, @event.AuctionId, @event.OldStatus, @event.NewStatus, previousStatus, item.Status);
    }
}
