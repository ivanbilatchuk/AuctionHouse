namespace Platform.Aggregator.Caching;

public static class AggregatorCacheKeys
{
    // Транспорт входить у ключ, щоб порівняння gRPC vs HTTP не підміняли відповіді одна одної.
    public static string AuctionCard(long auctionId, string transport) => $"aggregator:auction:{auctionId}:full:{transport}";

    // Коротше за кеші сервісів (аукціон 30 с, лот 10 хв): картка — похідні дані з трьох джерел.
    public static readonly TimeSpan AuctionCardTtl = TimeSpan.FromSeconds(30);
}
