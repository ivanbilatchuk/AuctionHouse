namespace Community.WebApi.IntegrationEvents;

// Community лише споживає події: черга community.catalog-sync <- catalog-events.
public static class CommunityEventBus
{
    public const string CatalogSyncQueue = "community.catalog-sync";

    // "item.*" — усі події предмета; та сама подія item.updated незалежно потрапляє і в auctions.catalog-sync (fan-out).
    public static readonly string[] CatalogBindingKeys = ["item.*"];
}
