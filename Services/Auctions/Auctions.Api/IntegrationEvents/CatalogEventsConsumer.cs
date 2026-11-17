using Auctions.Api.IntegrationEvents.EventHandling;
using Auctions.Api.IntegrationEvents.Events;
using Auctions.Domain.Exceptions;
using Platform.EventBus.Diagnostics;
using Platform.EventBus.RabbitMq;

namespace Auctions.Api.IntegrationEvents;

// Споживач подій Каталогу: черга auctions.catalog-sync <- catalog-events (item.*).
// BackgroundService з ручним підтвердженням (RabbitMqConsumerBase); реєстрація — AddHostedService у Program.cs.
public sealed class CatalogEventsConsumer : RabbitMqConsumerBase
{
    public CatalogEventsConsumer(IServiceProvider services, EventBusMetrics metrics, ILogger<CatalogEventsConsumer> logger)
        : base(services, metrics, logger)
    {
        Subscribe<ItemUpdatedIntegrationEvent, ItemUpdatedIntegrationEventHandler>();
    }

    protected override string Exchange => EventBusTopology.CatalogEvents;

    protected override string QueueName => AuctionsEventBus.CatalogSyncQueue;

    protected override IReadOnlyCollection<string> BindingKeys => AuctionsEventBus.CatalogBindingKeys;

    // Помилки валідації даних події від повтору не зникнуть — одразу в DLQ.
    // Недоступна БД (DataAccessException) чи deadlock (BusinessConflictException 1205) — тимчасові, повторюються.
    protected override bool IsPermanentFailure(Exception exception) =>
        base.IsPermanentFailure(exception) || exception is ValidationException;
}
