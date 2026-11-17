namespace Platform.Aggregator.Services;

public sealed class AggregatorOptions
{
    public const string SectionName = "Aggregator";

    // Таймаути, повтори й circuit breaker клієнтів — у Resilience/ServiceResilienceProfile (Лаба 7).
    public int RecentBidsCount { get; init; } = 5;
}
