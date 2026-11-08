namespace Catalog.Bll.Caching;

// Ключі й TTL кешу каталогу (префікс "catalog:" спільний для L1 і L2).
public static class CatalogCacheKeys
{
    public const string CategoriesAll = "catalog:categories:all";

    public static string Item(long id) => $"catalog:item:{id}";

    // Картка лота: читається часто, змінюється рідко.
    public static readonly CacheEntrySettings ItemSettings =
        new CacheEntrySettings(L1Sliding: TimeSpan.FromMinutes(1), L1Absolute: TimeSpan.FromMinutes(5), L2Ttl: TimeSpan.FromMinutes(10)).Validate();

    // Довідник категорій змінюється вкрай рідко.
    public static readonly CacheEntrySettings CategoriesSettings =
        new CacheEntrySettings(L1Sliding: TimeSpan.FromMinutes(5), L1Absolute: TimeSpan.FromMinutes(10), L2Ttl: TimeSpan.FromHours(1)).Validate();
}
