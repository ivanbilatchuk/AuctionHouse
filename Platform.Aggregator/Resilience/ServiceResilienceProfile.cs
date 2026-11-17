using Grpc.Core;

namespace Platform.Aggregator.Resilience;

// Параметри resilience для одного downstream-сервісу Aggregator-а. Обґрунтування значень — README.md
// (розділ «Лаба 7: політики resilience Aggregator»).
public sealed record ServiceResilienceProfile
{
    public required string Name { get; init; }

    // --- HTTP (Standard Resilience Handler: retry + circuit breaker + таймаути) ---
    public required int MaxRetryAttempts { get; init; }
    public required TimeSpan RetryDelay { get; init; }
    public required TimeSpan AttemptTimeout { get; init; }
    public required TimeSpan TotalRequestTimeout { get; init; }
    public required double FailureRatio { get; init; }
    public required int MinimumThroughput { get; init; }
    public required TimeSpan SamplingDuration { get; init; }
    public required TimeSpan BreakDuration { get; init; }

    // --- gRPC (вбудована retry-політика каналу за gRPC-статусами) ---
    public required int GrpcMaxAttempts { get; init; }
    public required TimeSpan GrpcInitialBackoff { get; init; }
    public required TimeSpan GrpcMaxBackoff { get; init; }
    public required StatusCode[] GrpcRetryableStatusCodes { get; init; }

    // Критичний сервіс: без нього картку не зібрати (Auctions — сам аукціон, Catalog — лот).
    // Більше повторів, чутливіший circuit breaker (раніше розмикається, раніше пробує знову).
    // TotalRequestTimeout ≈ AttemptTimeout × (1 + MaxRetryAttempts) + затримки: 2 × 4 + (0.3 + 0.6 + 1.2) ≈ 10.1 с → 11 с.
    public static readonly ServiceResilienceProfile Critical = new()
    {
        Name = "critical",
        MaxRetryAttempts = 3,
        RetryDelay = TimeSpan.FromMilliseconds(300),
        AttemptTimeout = TimeSpan.FromSeconds(2),
        TotalRequestTimeout = TimeSpan.FromSeconds(11),
        FailureRatio = 0.3,
        MinimumThroughput = 5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(10),
        GrpcMaxAttempts = 4,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(300),
        GrpcMaxBackoff = TimeSpan.FromSeconds(3),
        GrpcRetryableStatusCodes = [StatusCode.Unavailable, StatusCode.ResourceExhausted]
    };

    // Некритичний сервіс (Community — висновки експертів): Aggregator і без нього поверне часткову картку
    // з попередженням. Швидко здається: 1 повтор, короткі таймаути; менш чутливий breaker, довше розімкнений.
    // TotalRequestTimeout ≈ 1 × 2 + 0.2 ≈ 2.2 с → 3 с.
    public static readonly ServiceResilienceProfile NonCritical = new()
    {
        Name = "non-critical",
        MaxRetryAttempts = 1,
        RetryDelay = TimeSpan.FromMilliseconds(200),
        AttemptTimeout = TimeSpan.FromSeconds(1),
        TotalRequestTimeout = TimeSpan.FromSeconds(3),
        FailureRatio = 0.7,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(20),
        BreakDuration = TimeSpan.FromSeconds(30),
        GrpcMaxAttempts = 2,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(200),
        GrpcMaxBackoff = TimeSpan.FromMilliseconds(500),
        GrpcRetryableStatusCodes = [StatusCode.Unavailable]
    };
}
