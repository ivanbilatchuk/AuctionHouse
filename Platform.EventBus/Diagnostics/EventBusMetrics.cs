using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Platform.EventBus.Diagnostics;

// Метрики шини подій (Meter "Platform.EventBus", підключено в ServiceDefaults). Singleton.
// Теги з низькою кардинальністю: тип події, exchange/черга, результат обробки — без EventId та Id сутностей.
//   messaging.events.published        Counter    event.type, messaging.destination.name (exchange)
//   messaging.events.publish_failed   Counter    event.type, messaging.destination.name
//   messaging.events.processed        Counter    event.type, messaging.destination.name (черга), outcome = ack | retry | dead_letter | requeue
//   messaging.process.duration        Histogram  ті самі теги, мс — для успішних і невдалих обробок
//   messaging.dlq.depth               Gauge      messaging.destination.name (DLQ) — скільки повідомлень чекає на розслідування
public sealed class EventBusMetrics
{
    public const string MeterName = "Platform.EventBus";

    private readonly Counter<long> _published;
    private readonly Counter<long> _publishFailed;
    private readonly Counter<long> _processed;
    private readonly Histogram<double> _processDuration;
    private readonly ConcurrentDictionary<string, long> _deadLetterDepth = new();

    public EventBusMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _published = meter.CreateCounter<long>("messaging.events.published", "{event}", "Integration events published to the broker");
        _publishFailed = meter.CreateCounter<long>("messaging.events.publish_failed", "{event}", "Integration events that could not be published");
        _processed = meter.CreateCounter<long>("messaging.events.processed", "{event}", "Delivered messages by processing outcome");
        _processDuration = meter.CreateHistogram<double>("messaging.process.duration", "ms", "Duration of message processing");

        meter.CreateObservableGauge("messaging.dlq.depth",
            () => _deadLetterDepth.Select(pair =>
                new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("messaging.destination.name", pair.Key))),
            "{message}", "Messages waiting in a dead letter queue");
    }

    public void Published(string eventType, string exchange) =>
        _published.Add(1, Tag("event.type", eventType), Tag("messaging.destination.name", exchange));

    public void PublishFailed(string eventType, string exchange) =>
        _publishFailed.Add(1, Tag("event.type", eventType), Tag("messaging.destination.name", exchange));

    public void Processed(string eventType, string queue, string outcome, double elapsedMs)
    {
        KeyValuePair<string, object?>[] tags =
            [Tag("event.type", eventType), Tag("messaging.destination.name", queue), Tag("outcome", outcome)];
        _processed.Add(1, tags);
        _processDuration.Record(elapsedMs, tags);
    }

    public void DeadLetterDepth(string deadLetterQueue, long depth) => _deadLetterDepth[deadLetterQueue] = depth;

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);
}
