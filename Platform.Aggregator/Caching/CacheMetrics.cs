using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Caching;

// Лічильники кешу агрегатора (лише L2 — Redis). Експортуються через AddMeter("AuctionHouse.*") у ServiceDefaults.
public sealed class CacheMetrics
{
    public const string MeterName = "AuctionHouse.Aggregator.Cache";
    public const string L2 = "L2";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", unit: "{hit}", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", unit: "{miss}", description: "Cache misses by level");
    }

    public void Hit() => _hits.Add(1, new KeyValuePair<string, object?>("cache.level", L2), new KeyValuePair<string, object?>("cache.key_type", "aggregator:auction"));

    public void Miss() => _misses.Add(1, new KeyValuePair<string, object?>("cache.level", L2), new KeyValuePair<string, object?>("cache.key_type", "aggregator:auction"));
}
