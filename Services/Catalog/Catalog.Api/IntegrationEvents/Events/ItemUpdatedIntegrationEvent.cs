using Platform.EventBus.Abstractions;

namespace Catalog.Api.IntegrationEvents.Events;

// Каталог (власник) -> Auctions, Community: предмет змінено. Routing key "item.updated", exchange "catalog-events".
// Самодостатня: містить усі поля, які споживачі тримають у копіях, — по додаткові дані до Каталогу не звертаються.
// Ціни в уже створених аукціонах/ставках при цьому не змінюються — змінюється лише денормалізована назва.
public sealed record ItemUpdatedIntegrationEvent(
    long ItemId,
    string Title,
    string PreviousTitle,
    string Slug,
    long CategoryId,
    decimal EstimateLow,
    decimal EstimateHigh,
    string Currency,
    string Status) : IntegrationEvent;
