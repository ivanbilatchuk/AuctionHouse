using Community.Application.Behaviors;
using Community.Application.Caching;
using Community.Application.Mapping;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Community.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Порядок behaviors = порядок обгортання: Logging (зовнішній) -> Validation -> Caching -> Performance -> обробник.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        // Лаба 6: дворівневий кеш (IMemoryCache + IDistributedCache реєструються в Program.cs).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<TwoLevelCache>();

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ApplicationProfile).Assembly));

        return services;
    }
}
