using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.ServiceDefaults.HealthChecks;

namespace Catalog.Api.HealthChecks;

// Health checks залежностей каталогу (усі з тегом "ready" — потрапляють у /health/ready):
//   catalog-db  — SQL Server, легкий запит SELECT 1; недоступність БД => Unhealthy;
//   redis-cache — PING; недоступність => Degraded: кеш необов'язковий, сервіс читає з БД (TwoLevelCache).
// Ці ж перевірки віддає стандартний gRPC Health Checking Protocol (MapGrpcHealthChecksService у Program.cs).
public static class CatalogHealthChecks
{
    public static IHealthChecksBuilder AddCatalogHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlConnectionString = configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("Connection string 'CatalogDb' is not configured.");
        var redisConnectionString = configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' is not configured.");

        return services.AddHealthChecks()
            .AddSqlServer(
                connectionString: sqlConnectionString,
                healthQuery: "SELECT 1;",
                name: "catalog-db",
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
