using System.Diagnostics.Metrics;

namespace Catalog.Bll.Caching;

// Лічильники кешу (OpenTelemetry підхоплює їх через AddMeter("AuctionHouse.*") у ServiceDefaults).
public sealed class CacheMetrics
{
    public const string MeterName = "AuctionHouse.Catalog.Cache";
    public const string L1 = "L1";
    public const string L2 = "L2";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", unit: "{hit}", description: "Cache hits by level (L1 memory, L2 Redis)");
        _misses = meter.CreateCounter<long>("cache.misses", unit: "{miss}", description: "Cache misses by level (L1 memory, L2 Redis)");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", unit: "{key}", description: "Cache keys removed after writes");
    }

    public void Hit(string level, string key) => _hits.Add(1, Tags(level, key));

    public void Miss(string level, string key) => _misses.Add(1, Tags(level, key));

    public void Invalidated(string key) => _invalidations.Add(1, new KeyValuePair<string, object?>("cache.key_type", KeyType(key)));

    private static KeyValuePair<string, object?>[] Tags(string level, string key) =>
    [
        new("cache.level", level),
        new("cache.key_type", KeyType(key))
    ];

    // Тип ключа без ідентифікаторів ("catalog:item:42" -> "catalog:item"), щоб не роздувати кардинальність метрики.
    private static string KeyType(string key)
    {
        var parts = key.Split(':');
        return parts.Length >= 2 ? $"{parts[0]}:{parts[1]}" : key;
    }
}
