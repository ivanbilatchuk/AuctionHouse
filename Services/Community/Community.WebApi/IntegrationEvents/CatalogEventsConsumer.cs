using Community.Domain.Exceptions;
using Community.WebApi.IntegrationEvents.EventHandling;
using Community.WebApi.IntegrationEvents.Events;
using Platform.EventBus.Diagnostics;
using Platform.EventBus.RabbitMq;
using ValidationException = Community.Application.Common.ValidationException;

namespace Community.WebApi.IntegrationEvents;

// Споживач подій Каталогу: черга community.catalog-sync <- catalog-events (item.*).
// BackgroundService з ручним підтвердженням (RabbitMqConsumerBase); реєстрація — AddHostedService у Program.cs.
public sealed class CatalogEventsConsumer : RabbitMqConsumerBase
{
    public CatalogEventsConsumer(IServiceProvider services, EventBusMetrics metrics, ILogger<CatalogEventsConsumer> logger)
        : base(services, metrics, logger)
    {
        Subscribe<ItemUpdatedIntegrationEvent, ItemUpdatedIntegrationEventHandler>();
    }

    protected override string Exchange => EventBusTopology.CatalogEvents;

    protected override string QueueName => CommunityEventBus.CatalogSyncQueue;

    protected override IReadOnlyCollection<string> BindingKeys => CommunityEventBus.CatalogBindingKeys;

    // Помилки валідації (ValidationBehavior MediatR) і доменні правила від повтору не зміняться — одразу в DLQ.
    // Недоступна MongoDB (MongoConnectionException, TimeoutException) — тимчасовий збій, повторюється.
    protected override bool IsPermanentFailure(Exception exception) =>
        base.IsPermanentFailure(exception) || exception is ValidationException or DomainException;
}
