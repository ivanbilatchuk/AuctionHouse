using Auctions.Bll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Auctions.Bll.Caching;

public static class CachingServiceCollectionExtensions
{
    // Реєструє дворівневий кеш і підміняє IAuctionService/IBidService кешуючими декораторами.
    // IMemoryCache (з SizeLimit) та IDistributedCache (Redis) реєструються в Program.cs.
    public static IServiceCollection AddAuctionsCaching(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<TwoLevelCache>();

        services.AddScoped<AuctionService>();
        services.AddScoped<IAuctionService, CachedAuctionService>();
        services.AddScoped<BidService>();
        services.AddScoped<IBidService, CachedBidService>();

        return services;
    }
}
