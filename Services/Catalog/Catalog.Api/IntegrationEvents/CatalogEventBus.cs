using Platform.EventBus.RabbitMq;

namespace Catalog.Api.IntegrationEvents;

// Маршрутизація подій Каталогу: що публікує (exchange catalog-events) і що споживає (черга catalog.auctions-sync).
public static class CatalogEventBus
{
    public const string Exchange = EventBusTopology.CatalogEvents;

    public const string ItemUpdatedRoutingKey = "item.updated";

    // Каталогу потрібні лише статуси, що змінюють стан предмета (Scheduled/Draft сюди не потрапляють — topic exchange їх відфільтрує).
    public const string AuctionsSyncQueue = "catalog.auctions-sync";

    public static readonly string[] AuctionStatusBindingKeys =
        ["auction.active", "auction.closed", "auction.unsold", "auction.cancelled"];
}
