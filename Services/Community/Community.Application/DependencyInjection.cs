using Community.Application.Behaviors;
using Community.Application.Mapping;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Community.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Порядок behaviors = порядок обгортання: Logging (зовнішній) -> Validation -> Performance -> обробник.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ApplicationProfile).Assembly));

        return services;
    }
}
