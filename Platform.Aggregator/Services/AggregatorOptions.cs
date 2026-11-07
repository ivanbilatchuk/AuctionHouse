namespace Platform.Aggregator.Services;

public sealed class AggregatorOptions
{
    public const string SectionName = "Aggregator";

    // Короткі таймаути, щоб при недоступному сервісі картка поверталась швидко з попередженням.
    public int AttemptTimeoutSeconds { get; init; } = 2;
    public int TotalTimeoutSeconds { get; init; } = 5;
    public int RecentBidsCount { get; init; } = 5;
}
