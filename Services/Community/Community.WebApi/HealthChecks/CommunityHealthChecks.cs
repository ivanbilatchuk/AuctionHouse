using Community.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Platform.ServiceDefaults.HealthChecks;

namespace Community.WebApi.HealthChecks;

// Health checks залежностей сервісу спільноти (усі з тегом "ready" — потрапляють у /health/ready):
//   community-db — MongoDB, команда ping до бази сервісу через зареєстрований IMongoClient => Unhealthy;
//   redis-cache  — PING; недоступність => Degraded: кеш необов'язковий, сервіс читає з MongoDB (TwoLevelCache).
// Ці ж перевірки віддає стандартний gRPC Health Checking Protocol (MapGrpcHealthChecksService у Program.cs).
public static class CommunityHealthChecks
{
    public static IHealthChecksBuilder AddCommunityHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' is not configured.");

        return services.AddHealthChecks()
            .AddMongoDb(
                clientFactory: sp => sp.GetRequiredService<IMongoClient>(),
                databaseNameFactory: sp => sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.DatabaseName,
                name: "community-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "mongodb", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(5))
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded, // кеш необов'язковий
                tags: ["cache", "redis", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(3));
    }
}
