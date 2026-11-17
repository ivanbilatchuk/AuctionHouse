namespace Platform.EventBus.Abstractions;

// Публікація integration event у брокер. Викликається ЛИШЕ після успішного збереження змін у БД.
public interface IEventPublisher
{
    // exchange — topic exchange сервісу-власника (catalog-events, auctions-events);
    // routingKey — тип бізнес-факту (item.updated, auction.active, ...), за ним черги споживачів фільтрують події.
    Task PublishAsync<TEvent>(TEvent @event, string exchange, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}
