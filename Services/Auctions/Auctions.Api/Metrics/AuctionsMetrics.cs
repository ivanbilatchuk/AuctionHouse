using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Auctions.Api.Metrics;

// Бізнес-метрики сервісу аукціонів (OpenTelemetry Meter API). Singleton: Meter живе весь час роботи застосунку.
// Meter "Auctions.Api" додано в OpenTelemetry у ServiceDefaults (Extensions.BusinessMeters) — експорт у Dashboard по OTLP.
//
// Теги (низька кардинальність, однакові назви в усіх сервісах):
//   auction.status — Draft | Scheduled | Active | Closed | Settled | Unsold | Cancelled (статус після операції);
//   bid.position   — leading | outbid (чи стала ставка лідерською);
//   operation      — create_auction | change_status | place_bid | register_bidder;
//   status         — success | failure.
// Ідентифікатори аукціонів, учасників і суми ставок у теги НЕ потрапляють (кожне значення — окремий часовий ряд).
public sealed class AuctionsMetrics
{
    public const string MeterName = "Auctions.Api";

    private readonly Counter<long> _auctionsCreated;
    private readonly Counter<long> _auctionStatusChanged;
    private readonly Counter<long> _bidsPlaced;
    private readonly Counter<long> _biddersRegistered;
    private readonly Histogram<double> _operationDuration;
    private readonly Histogram<double> _bidAmount;
    private long _operationsInProgress;

    public AuctionsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _auctionsCreated = meter.CreateCounter<long>(
            "auctions.created", unit: "{auction}", description: "Number of created auctions");
        _auctionStatusChanged = meter.CreateCounter<long>(
            "auctions.status_changed", unit: "{auction}", description: "Number of auction status transitions");
        _bidsPlaced = meter.CreateCounter<long>(
            "auctions.bids.placed", unit: "{bid}", description: "Number of accepted bids");
        _biddersRegistered = meter.CreateCounter<long>(
            "auctions.bidders.registered", unit: "{bidder}", description: "Number of bidder registrations for auctions");

        _operationDuration = meter.CreateHistogram<double>(
            "auctions.operation.duration", unit: "ms", description: "Duration of auction business operations");
        _bidAmount = meter.CreateHistogram<double>(
            "auctions.bid.amount", unit: "{currency}", description: "Distribution of accepted bid amounts");

        // Gauge: знімок поточного стану, значення читається callback-ом у момент збору метрик.
        meter.CreateObservableGauge(
            "auctions.operations.in_progress",
            () => Interlocked.Read(ref _operationsInProgress),
            unit: "{operation}",
            description: "Business operations currently being processed");
    }

    public void AuctionCreated(string status) =>
        _auctionsCreated.Add(1, new KeyValuePair<string, object?>("auction.status", status));

    public void AuctionStatusChanged(string newStatus) =>
        _auctionStatusChanged.Add(1, new KeyValuePair<string, object?>("auction.status", newStatus));

    public void BidPlaced(bool isLeading, decimal amount)
    {
        var position = new KeyValuePair<string, object?>("bid.position", isLeading ? "leading" : "outbid");
        _bidsPlaced.Add(1, position);
        _bidAmount.Record((double)amount, position);
    }

    public void BidderRegistered() => _biddersRegistered.Add(1);

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
