using Platform.EventBus.Abstractions;

namespace Catalog.Api.IntegrationEvents.Events;

// Контракт події Auctions (власник статусу аукціону), яку споживає Каталог. Як в eShop, кожен сервіс має
// власну копію record-а: спільний лише JSON-формат (назва типу в BasicProperties.Type + поля в camelCase),
// а не збірка — сервіси розгортаються незалежно.
public sealed record AuctionStatusChangedIntegrationEvent(
    long AuctionId,
    long ItemId,
    string? OldStatus,
    string NewStatus) : IntegrationEvent;
