using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.ServiceDefaults.HealthChecks;

namespace Auctions.Api.HealthChecks;

// Health checks залежностей сервісу аукціонів (усі з тегом "ready" — потрапляють у /health/ready):
//   auctions-db — SQL Server, легкий запит SELECT 1; недоступність БД => Unhealthy (сервіс без неї не працює);
//   redis-cache — PING; недоступність => Degraded: кеш необов'язковий, сервіс читає з БД (TwoLevelCache).
// Ці ж перевірки віддає стандартний gRPC Health Checking Protocol (MapGrpcHealthChecksService у Program.cs).
public static class AuctionsHealthChecks
{
    public static IHealthChecksBuilder AddAuctionsHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlConnectionString = configuration.GetConnectionString("AuctionsDb")
            ?? throw new InvalidOperationException("Connection string 'AuctionsDb' is not configured.");
        var redisConnectionString = configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' is not configured.");

        return services.AddHealthChecks()
            .AddSqlServer(
                connectionString: sqlConnectionString,
                healthQuery: "SELECT 1;",
                name: "auctions-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(5))
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded, // кеш необов'язковий
                tags: ["cache", "redis", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(3));
    }
}
