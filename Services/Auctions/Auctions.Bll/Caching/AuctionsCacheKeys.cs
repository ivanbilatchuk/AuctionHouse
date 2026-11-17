namespace Auctions.Bll.Caching;

// Ключі й TTL кешу аукціонів (префікс "auctions:"). Ціна й кількість ставок змінюються часто,
// тому TTL короткі, а після кожної ставки/зміни статусу записи видаляються одразу.
public static class AuctionsCacheKeys
{
    public static string Auction(long id) => $"auctions:auction:{id}";

    public static string Bids(long auctionId) => $"auctions:auction:{auctionId}:bids";

    public static readonly CacheEntrySettings AuctionSettings =
        new CacheEntrySettings(L1Sliding: TimeSpan.FromSeconds(5), L1Absolute: TimeSpan.FromSeconds(10), L2Ttl: TimeSpan.FromSeconds(30)).Validate();

    public static readonly CacheEntrySettings BidsSettings =
        new CacheEntrySettings(L1Sliding: TimeSpan.FromSeconds(5), L1Absolute: TimeSpan.FromSeconds(10), L2Ttl: TimeSpan.FromSeconds(30)).Validate();
}
