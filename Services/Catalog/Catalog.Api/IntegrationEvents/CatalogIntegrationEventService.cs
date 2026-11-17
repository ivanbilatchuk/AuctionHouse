using Catalog.Api.IntegrationEvents.Events;
using Catalog.Bll.IntegrationEvents;
using Catalog.Domain.Entities;
using Platform.EventBus.Abstractions;
using Platform.ServiceDefaults;

namespace Catalog.Api.IntegrationEvents;

// Будує integration events Каталогу з доменних даних і публікує їх через IEventPublisher.
// CorrelationId — з X-Correlation-Id поточного HTTP-запиту (CorrelationIdMiddleware).
//
// Збій публікації не перетворюється на 500: зміни в БД уже збережено, і клієнт мусить це знати.
// Подію в такому разі втрачено — це логується як Error з усім бізнес-контекстом (ItemId, EventId, тип),
// копії назви оновляться наступною зміною предмета. Гарантію доставки дав би Transactional Outbox.
public sealed class CatalogIntegrationEventService(
    IEventPublisher publisher,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CatalogIntegrationEventService> logger) : ICatalogIntegrationEventService
{
    public async Task PublishItemUpdatedAsync(Item item, string previousTitle, CancellationToken ct = default)
    {
        var @event = new ItemUpdatedIntegrationEvent(
            item.Id, item.Title, previousTitle, item.Slug, item.CategoryId,
            item.EstimateLow, item.EstimateHigh, item.Currency, item.Status.ToString())
        {
            OccurredAt = item.UpdatedAt ?? DateTime.UtcNow,
            CorrelationId = CurrentCorrelationId()
        };

        try
        {
            await publisher.PublishAsync(@event, CatalogEventBus.Exchange, CatalogEventBus.ItemUpdatedRoutingKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Item {ItemId} was saved, but {EventType} {EventId} was NOT published (title {PreviousTitle} -> {Title}); " +
                "copies in Auctions/Community stay stale until the next change",
                item.Id, nameof(ItemUpdatedIntegrationEvent), @event.EventId, previousTitle, item.Title);
        }
    }

    private string? CurrentCorrelationId()
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
