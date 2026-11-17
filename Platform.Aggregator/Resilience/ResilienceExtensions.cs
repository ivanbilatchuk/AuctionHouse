using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Platform.Aggregator.Resilience;

// Resilience клієнтів Aggregator-а.
//
// ServiceDefaults підключає Standard Resilience Handler до ВСІХ HttpClient через ConfigureHttpClientDefaults —
// у т. ч. до gRPC-клієнтів (їх створює та сама IHttpClientFactory). Щоб задати власні параметри конкретному клієнту,
// стандартний обробник спершу прибирається (RemoveAllResilienceHandlers), і лише потім додається власний —
// обробники не нашаровуються (інакше повтори множилися б: 3 × 3 = 9 спроб).
public static class ResilienceExtensions
{
    // HTTP-рівень: retry (експоненційна затримка + jitter, лише для безпечних методів), circuit breaker
    // з логуванням переходів, таймаут спроби й загальний таймаут, узгоджені між собою.
    public static IHttpClientBuilder AddServiceResilience(this IHttpClientBuilder builder, ServiceResilienceProfile profile)
    {
        var clientName = builder.Name;

        builder
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler()
            .Configure((options, services) =>
            {
                var logger = services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Platform.Aggregator.Resilience");

                options.Retry.MaxRetryAttempts = profile.MaxRetryAttempts;
                options.Retry.Delay = profile.RetryDelay;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                // POST/PUT/PATCH/DELETE не повторюються: повтор може продублювати зміну.
                // gRPC-виклики теж ідуть як HTTP POST — їх повторює retry-політика gRPC-каналу (AddGrpcRetryPolicy).
                options.Retry.DisableForUnsafeHttpMethods();

                options.AttemptTimeout.Timeout = profile.AttemptTimeout;
                options.TotalRequestTimeout.Timeout = profile.TotalRequestTimeout;

                options.CircuitBreaker.FailureRatio = profile.FailureRatio;
                options.CircuitBreaker.MinimumThroughput = profile.MinimumThroughput;
                // Має бути щонайменше 2 × AttemptTimeout — інакше валідація опцій не пропустить старт.
                options.CircuitBreaker.SamplingDuration = profile.SamplingDuration;
                options.CircuitBreaker.BreakDuration = profile.BreakDuration;

                options.CircuitBreaker.OnOpened = args =>
                {
                    logger.LogError(
                        "Circuit opened for {Client} ({Profile}) for {BreakDuration}: {Reason}",
                        clientName, profile.Name, args.BreakDuration, DescribeOutcome(args.Outcome));
                    return ValueTask.CompletedTask;
                };
                options.CircuitBreaker.OnHalfOpened = _ =>
                {
                    logger.LogWarning("Circuit half-opened for {Client} ({Profile}): sending a probe request",
                        clientName, profile.Name);
                    return ValueTask.CompletedTask;
                };
                options.CircuitBreaker.OnClosed = _ =>
                {
                    logger.LogInformation("Circuit closed for {Client} ({Profile}): service recovered",
                        clientName, profile.Name);
                    return ValueTask.CompletedTask;
                };
            });

        return builder;
    }

    // gRPC-рівень: помилки gRPC (Unavailable, ResourceExhausted, ...) приходять статусом у трейлерах відповіді
    // з HTTP 200, тож HTTP-обробник їх не бачить. Повтори за gRPC-статусами робить сам канал (ServiceConfig).
    public static IHttpClientBuilder AddGrpcRetryPolicy(this IHttpClientBuilder builder, ServiceResilienceProfile profile)
    {
        var retryPolicy = new RetryPolicy
        {
            MaxAttempts = profile.GrpcMaxAttempts,
            InitialBackoff = profile.GrpcInitialBackoff,
            MaxBackoff = profile.GrpcMaxBackoff,
            BackoffMultiplier = 2
        };
        foreach (var code in profile.GrpcRetryableStatusCodes)
            retryPolicy.RetryableStatusCodes.Add(code);

        return builder.ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = retryPolicy
                }
            }
        });
    }

    private static string DescribeOutcome(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } ex
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"HTTP {(int?)outcome.Result?.StatusCode}";
}
