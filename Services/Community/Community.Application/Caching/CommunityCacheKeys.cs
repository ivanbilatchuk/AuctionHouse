namespace Community.Application.Caching;

// Ключі й TTL кешу спільноти (префікс "community:").
public static class CommunityCacheKeys
{
    public static string OpinionsForItem(long itemId) => $"community:opinions:item:{itemId}";

    public static string OpinionsForItemVersion(long itemId) => $"community:opinions:item:{itemId}:version";

    public static readonly CacheEntrySettings OpinionsSettings =
        new CacheEntrySettings(L1Sliding: TimeSpan.FromSeconds(30), L1Absolute: TimeSpan.FromMinutes(1), L2Ttl: TimeSpan.FromMinutes(5)).Validate();

    // Версія живе довше за самі сторінки, щоб не "губити" їх раніше часу.
    public static readonly CacheEntrySettings VersionSettings =
        new CacheEntrySettings(L1Sliding: TimeSpan.FromMinutes(1), L1Absolute: TimeSpan.FromMinutes(5), L2Ttl: TimeSpan.FromMinutes(10)).Validate();
}
