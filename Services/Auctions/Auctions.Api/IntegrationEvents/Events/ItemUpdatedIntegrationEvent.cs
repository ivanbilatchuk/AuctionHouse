using Platform.EventBus.Abstractions;

namespace Auctions.Api.IntegrationEvents.Events;

// Контракт події Каталогу, яку споживає сервіс аукціонів (власна копія record-а, як в eShop:
// спільний лише JSON-формат). Поля, яких тут немає, під час десеріалізації ігноруються.
public sealed record ItemUpdatedIntegrationEvent(
    long ItemId,
    string Title,
    string? PreviousTitle) : IntegrationEvent;
