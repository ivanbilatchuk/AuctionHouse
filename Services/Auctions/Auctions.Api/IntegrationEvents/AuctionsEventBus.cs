using Platform.EventBus.RabbitMq;

namespace Auctions.Api.IntegrationEvents;

// Маршрутизація подій сервісу аукціонів: що публікує (auctions-events) і що споживає (черга auctions.catalog-sync).
public static class AuctionsEventBus
{
    public const string Exchange = EventBusTopology.AuctionsEvents;

    public static string AuctionStatusRoutingKey(string newStatus) => $"auction.{newStatus.ToLowerInvariant()}";

    public const string CatalogSyncQueue = "auctions.catalog-sync";

    // "item.*" — усі події предмета (одне слово після крапки): item.updated і майбутні item.<...>.
    public static readonly string[] CatalogBindingKeys = ["item.*"];
}
