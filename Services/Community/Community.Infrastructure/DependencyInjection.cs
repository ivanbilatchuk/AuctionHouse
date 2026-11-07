using Community.Application.Abstractions.Queries;
using Community.Domain.Repositories;
using Community.Infrastructure.HealthChecks;
using Community.Infrastructure.Indexes;
using Community.Infrastructure.Persistence;
using Community.Infrastructure.Persistence.Mapping;
using Community.Infrastructure.Queries;
using Community.Infrastructure.Repositories;
using Community.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Community.Infrastructure;

public static class DependencyInjection
{
    // Ім'я рядка підключення, який підставляє .NET Aspire (WithReference(communitydb)).
    public const string AspireConnectionName = "communitydb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        MongoMappings.Register();

        services.AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .PostConfigure(settings =>
            {
                var aspireConnection = configuration.GetConnectionString(AspireConnectionName);
                if (!string.IsNullOrWhiteSpace(aspireConnection))
                    settings.ConnectionString = aspireConnection;
            })
            .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString) && !string.IsNullOrWhiteSpace(s.DatabaseName),
                "MongoDb:ConnectionString and MongoDb:DatabaseName are required.")
            .ValidateOnStart();

        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IExpertOpinionRepository, ExpertOpinionRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<IExpertOpinionQueries, ExpertOpinionQueries>();
        services.AddScoped<IDiscussionQueries, DiscussionQueries>();

        services.AddSingleton<MongoIndexInitializer>();
        services.AddSingleton<IDataSeeder, MongoDataSeeder>();

        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

        return services;
    }

    // Викликається у Program.cs після Build(): індекси, потім (якщо увімкнено) ідемпотентний сідінг.
    public static async Task InitializeMongoAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await services.GetRequiredService<MongoIndexInitializer>().EnsureIndexesAsync(ct);

        var settings = services.GetRequiredService<IOptions<MongoDbSettings>>().Value;
        if (settings.SeedOnStartup)
            await services.GetRequiredService<IDataSeeder>().SeedAsync(ct);
        else
            services.GetRequiredService<ILogger<MongoDbContext>>().LogInformation("MongoDB seeding is disabled (MongoDb:SeedOnStartup=false)");
    }
}
