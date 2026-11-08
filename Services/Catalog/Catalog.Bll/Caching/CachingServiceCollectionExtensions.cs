using Catalog.Bll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Catalog.Bll.Caching;

public static class CachingServiceCollectionExtensions
{
    // Реєструє дворівневий кеш і підміняє IItemService/ICategoryService кешуючими декораторами.
    // IMemoryCache (з SizeLimit) та IDistributedCache (Redis) реєструються в Program.cs.
    public static IServiceCollection AddCatalogCaching(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<TwoLevelCache>();

        services.AddScoped<ItemService>();
        services.AddScoped<IItemService, CachedItemService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ICategoryService, CachedCategoryService>();

        return services;
    }
}
