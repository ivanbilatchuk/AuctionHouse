using Community.Application.Commands.IntegrationEvents.SyncItemTitle;
using Community.WebApi.IntegrationEvents.Events;
using MediatR;
using Platform.EventBus.Abstractions;

namespace Community.WebApi.IntegrationEvents.EventHandling;

// Політика дублювання №2: expertOpinions.item.title і discussions.item.title — копії Catalog.Item.Title.
// Обробник лише перекладає integration event у команду Application-шару (CQRS): уся логіка — у SyncItemTitleCommandHandler
// (ідемпотентність через processedEvents, захист від out-of-order, інвалідація кешу), валідація — у pipeline MediatR.
public sealed class ItemUpdatedIntegrationEventHandler(ISender sender, ILogger<ItemUpdatedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ItemUpdatedIntegrationEvent>
{
    public async Task HandleAsync(ItemUpdatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SyncItemTitleCommand(
            @event.EventId, nameof(ItemUpdatedIntegrationEvent), @event.ItemId, @event.Title, @event.OccurredAt), cancellationToken);

        if (!result.IsDuplicate)
            logger.LogInformation(
                "Item {ItemId} title synced {PreviousTitle} -> {Title}: {OpinionsUpdated} expert opinions, {DiscussionsUpdated} discussions updated",
                @event.ItemId, @event.PreviousTitle, @event.Title, result.OpinionsUpdated, result.DiscussionsUpdated);
    }
}
