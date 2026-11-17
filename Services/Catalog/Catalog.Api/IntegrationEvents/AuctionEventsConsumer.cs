using Catalog.Api.IntegrationEvents.EventHandling;
using Catalog.Api.IntegrationEvents.Events;
using Catalog.Domain.Exceptions;
using Platform.EventBus.Diagnostics;
using Platform.EventBus.RabbitMq;

namespace Catalog.Api.IntegrationEvents;

// Споживач подій Auctions: черга catalog.auctions-sync <- auctions-events (auction.active/closed/unsold/cancelled).
// BackgroundService з ручним підтвердженням (RabbitMqConsumerBase); реєстрація — AddHostedService у Program.cs.
public sealed class AuctionEventsConsumer : RabbitMqConsumerBase
{
    public AuctionEventsConsumer(IServiceProvider services, EventBusMetrics metrics, ILogger<AuctionEventsConsumer> logger)
        : base(services, metrics, logger)
    {
        Subscribe<AuctionStatusChangedIntegrationEvent, AuctionStatusChangedIntegrationEventHandler>();
    }

    protected override string Exchange => EventBusTopology.AuctionsEvents;

    protected override string QueueName => CatalogEventBus.AuctionsSyncQueue;

    protected override IReadOnlyCollection<string> BindingKeys => CatalogEventBus.AuctionStatusBindingKeys;

    // Неможливий перехід статусу чи відсутній предмет від повтору не зміняться — одразу в DLQ.
    protected override bool IsPermanentFailure(Exception exception) =>
        base.IsPermanentFailure(exception) || exception is DomainException or EntityNotFoundException;
}
