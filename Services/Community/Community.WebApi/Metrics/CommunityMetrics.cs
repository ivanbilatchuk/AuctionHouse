using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Community.WebApi.Metrics;

// Бізнес-метрики спільноти: висновки експертів і обговорення (OpenTelemetry Meter API). Singleton.
// Meter "Community.WebApi" додано в OpenTelemetry у ServiceDefaults (Extensions.BusinessMeters).
//
// Теги (низька кардинальність):
//   opinion.verdict — Authentic | Questionable | Counterfeit (висновок експерта);
//   category.kind   — Cards | Stamps | Vinyl | Coins (тип категорії лота);
//   operation       — create_opinion | update_opinion | delete_opinion | create_discussion | add_comment;
//   status          — success | failure.
// ObjectId документів, ідентифікатори експертів і лотів у теги НЕ потрапляють.
public sealed class CommunityMetrics
{
    public const string MeterName = "Community.WebApi";

    private readonly Counter<long> _opinionsCreated;
    private readonly Counter<long> _opinionsUpdated;
    private readonly Counter<long> _opinionsDeleted;
    private readonly Counter<long> _discussionsCreated;
    private readonly Counter<long> _commentsAdded;
    private readonly Histogram<double> _operationDuration;
    private readonly Histogram<int> _opinionConfidence;
    private long _operationsInProgress;

    public CommunityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _opinionsCreated = meter.CreateCounter<long>(
            "community.opinions.created", unit: "{opinion}", description: "Number of created expert opinions");
        _opinionsUpdated = meter.CreateCounter<long>(
            "community.opinions.updated", unit: "{opinion}", description: "Number of updated expert opinions");
        _opinionsDeleted = meter.CreateCounter<long>(
            "community.opinions.deleted", unit: "{opinion}", description: "Number of deleted expert opinions");
        _discussionsCreated = meter.CreateCounter<long>(
            "community.discussions.created", unit: "{discussion}", description: "Number of created discussions");
        _commentsAdded = meter.CreateCounter<long>(
            "community.comments.added", unit: "{comment}", description: "Number of comments added to discussions");

        _operationDuration = meter.CreateHistogram<double>(
            "community.operation.duration", unit: "ms", description: "Duration of community business operations");
        _opinionConfidence = meter.CreateHistogram<int>(
            "community.opinion.confidence", unit: "%", description: "Distribution of expert confidence in created opinions");

        // Gauge: знімок поточного стану, значення читається callback-ом у момент збору метрик.
        meter.CreateObservableGauge(
            "community.operations.in_progress",
            () => Interlocked.Read(ref _operationsInProgress),
            unit: "{operation}",
            description: "Business operations currently being processed");
    }

    public void OpinionCreated(string verdict, string categoryKind, int confidence)
    {
        var tags = new TagList
        {
            { "opinion.verdict", verdict },
            { "category.kind", categoryKind }
        };
        _opinionsCreated.Add(1, tags);
        _opinionConfidence.Record(confidence, new KeyValuePair<string, object?>("opinion.verdict", verdict));
    }

    public void OpinionUpdated() => _opinionsUpdated.Add(1);

    public void OpinionDeleted() => _opinionsDeleted.Add(1);

    public void DiscussionCreated() => _discussionsCreated.Add(1);

    public void CommentAdded() => _commentsAdded.Add(1);

    // Варіант для операцій без результату (Update/Delete).
    public Task MeasureAsync(string operation, Func<Task> action) =>
        MeasureAsync(operation, async () => { await action(); return true; });

    // Тривалість записується для ВСІХ виконань — і успішних, і невдалих (виняток прокидається далі).
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Interlocked.Increment(ref _operationsInProgress);
        var startedAt = Stopwatch.GetTimestamp();
        var success = false;
        try
        {
            var result = await action();
            success = true;
            return result;
        }
        finally
        {
            _operationDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
            Interlocked.Decrement(ref _operationsInProgress);
        }
    }
}
