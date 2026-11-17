using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.ServiceDefaults.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

// Перевірка gRPC-залежності Aggregator-а через стандартний gRPC Health Checking Protocol (grpc.health.v1.Health/Check).
// Сервіси відповідають на нього через MapGrpcHealthChecksService на основі своїх "ready"-перевірок (БД, Redis).
// Короткий дедлайн (2 с) — швидко виявити недоступність; RpcException => failureStatus реєстрації + Warning у лог.
// Виклик ідемпотентний і не має побічних ефектів у сервісі, який перевіряється.
public sealed class GrpcServiceHealthCheck(Health.HealthClient client, string serviceName, ILogger<GrpcServiceHealthCheck> logger)
    : IHealthCheck
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var reply = await client.CheckAsync(new HealthCheckRequest(),
                deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: cancellationToken);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{serviceName} is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{serviceName} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning("gRPC health check of {Service} failed: {GrpcStatus} {Detail}", serviceName, ex.StatusCode, ex.Status.Detail);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{serviceName} is unavailable ({ex.StatusCode})", ex);
        }
    }
}

public static class GrpcServiceHealthCheckExtensions
{
    // Окремий іменований gRPC-клієнт Health для кожного сервісу. Без resilience-обробника: health check не повинен
    // повторювати запити чи розмикати спільний circuit breaker — він має швидко сказати правду про стан.
    public static IHealthChecksBuilder AddGrpcServiceHealthCheck(
        this IHealthChecksBuilder builder, string serviceName, Uri grpcAddress, HealthStatus failureStatus)
    {
        var clientName = $"{serviceName}-health";

        builder.Services
            .AddGrpcClient<Health.HealthClient>(clientName, o => o.Address = grpcAddress)
            .RemoveAllResilienceHandlers();

        return builder.Add(new HealthCheckRegistration(
            serviceName,
            sp => new GrpcServiceHealthCheck(
                sp.GetRequiredService<GrpcClientFactory>().CreateClient<Health.HealthClient>(clientName),
                serviceName,
                sp.GetRequiredService<ILogger<GrpcServiceHealthCheck>>()),
            failureStatus,
            ["grpc", "downstream", HealthTags.Ready],
            timeout: TimeSpan.FromSeconds(3)));
    }
}
