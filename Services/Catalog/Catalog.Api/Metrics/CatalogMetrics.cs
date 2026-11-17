using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Catalog.Api.Metrics;

// Бізнес-метрики каталогу лотів (OpenTelemetry Meter API). Singleton: Meter живе весь час роботи застосунку.
// Meter "Catalog.Api" додано в OpenTelemetry у ServiceDefaults (Extensions.BusinessMeters).
// Окремо від нього лишаються лічильники кешу Лаб. 06 (Meter "AuctionHouse.Catalog.Cache": cache.hits / cache.misses).
//
// Теги (низька кардинальність):
//   item.status — Draft | Listed | InAuction | Sold | Withdrawn (статус лота після операції);
//   operation   — create_item | update_item | change_item_status | delete_item;
//   status      — success | failure.
// Id лотів, SKU, slug і продавці в теги НЕ потрапляють.
public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _itemsCreated;
    private readonly Counter<long> _itemsUpdated;
    private readonly Counter<long> _itemStatusChanged;
    private readonly Counter<long> _itemsDeleted;
    private readonly Histogram<double> _operationDuration;
    private long _operationsInProgress;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _itemsCreated = meter.CreateCounter<long>(
            "catalog.items.created", unit: "{item}", description: "Number of created catalog items (lots)");
        _itemsUpdated = meter.CreateCounter<long>(
            "catalog.items.updated", unit: "{item}", description: "Number of updated catalog items");
        _itemStatusChanged = meter.CreateCounter<long>(
            "catalog.items.status_changed", unit: "{item}", description: "Number of catalog item status transitions");
        _itemsDeleted = meter.CreateCounter<long>(
            "catalog.items.deleted", unit: "{item}", description: "Number of deleted catalog items");

        _operationDuration = meter.CreateHistogram<double>(
            "catalog.operation.duration", unit: "ms", description: "Duration of catalog business operations");

        // Gauge: знімок поточного стану, значення читається callback-ом у момент збору метрик.
        meter.CreateObservableGauge(
            "catalog.operations.in_progress",
            () => Interlocked.Read(ref _operationsInProgress),
            unit: "{operation}",
            description: "Business operations currently being processed");
    }

    public void ItemCreated(string status) =>
        _itemsCreated.Add(1, new KeyValuePair<string, object?>("item.status", status));

    public void ItemUpdated() => _itemsUpdated.Add(1);

    public void ItemStatusChanged(string newStatus) =>
        _itemStatusChanged.Add(1, new KeyValuePair<string, object?>("item.status", newStatus));

    public void ItemDeleted() => _itemsDeleted.Add(1);

    // Варіант для операцій без результату (Update/ChangeStatus/Delete повертають Task).
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
