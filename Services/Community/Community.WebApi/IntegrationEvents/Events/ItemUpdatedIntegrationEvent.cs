using Platform.EventBus.Abstractions;

namespace Community.WebApi.IntegrationEvents.Events;

// Контракт події Каталогу, яку споживає Community (власна копія record-а, як в eShop:
// спільний лише JSON-формат). Поля, яких тут немає, під час десеріалізації ігноруються.
public sealed record ItemUpdatedIntegrationEvent(
    long ItemId,
    string Title,
    string? PreviousTitle) : IntegrationEvent;
