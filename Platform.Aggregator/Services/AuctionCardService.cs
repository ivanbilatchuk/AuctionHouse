using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Dto;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Platform.Aggregator.Services;

// Збирає картку аукціону з трьох сервісів. Аукціон обов'язковий; лот, висновки й ставки запитуються паралельно,
// і збій будь-якого з них дає попередження у Warnings замість 500.
public sealed class AuctionCardService(
    AuctionsClient auctions,
    CatalogClient catalog,
    CommunityClient community,
    IOptions<AggregatorOptions> options,
    TimeProvider timeProvider,
    ILogger<AuctionCardService> logger)
{
    public const string ActivitySourceName = "AuctionHouse.Aggregator";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public async Task<AuctionCardResult> GetAuctionCardAsync(long auctionId, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("BuildAuctionCard");
        activity?.SetTag("auction.id", auctionId);

        var warnings = new List<string>();

        // Крок 1: аукціон — без нього картки немає (404), а його недоступність — 503.
        var auctionCall = await SafeAsync(AuctionsClient.ServiceName, auctionId,
            token => auctions.GetAuctionAsync(auctionId, token), ct);
        if (!auctionCall.Succeeded)
            return AuctionCardResult.Unavailable($"{AuctionsClient.ServiceName} service is unavailable");
        if (auctionCall.Value is null)
        {
            logger.LogInformation("Auction {AuctionId} not found, card is not built", auctionId);
            return AuctionCardResult.NotFound();
        }

        var auction = auctionCall.Value;
        activity?.SetTag("auction.item_id", auction.ItemId);

        // Крок 2: паралельно лот, висновки експертів і ставки.
        var lotTask = SafeAsync(CatalogClient.ServiceName, auctionId, token => catalog.GetItemAsync(auction.ItemId, token), ct);
        var opinionsTask = SafeAsync(CommunityClient.ServiceName, auctionId, token => community.GetExpertOpinionsAsync(auction.ItemId, token), ct);
        var bidsTask = SafeAsync(AuctionsClient.ServiceName + "-bids", auctionId, token => auctions.GetBidsAsync(auctionId, token), ct);

        await Task.WhenAll(lotTask, opinionsTask, bidsTask);

        var lotCall = await lotTask;
        var opinionsCall = await opinionsTask;
        var bidsCall = await bidsTask;

        if (!lotCall.Succeeded) warnings.Add($"{CatalogClient.ServiceName} service is unavailable");
        if (!opinionsCall.Succeeded) warnings.Add($"{CommunityClient.ServiceName} service is unavailable");
        if (!bidsCall.Succeeded) warnings.Add($"{AuctionsClient.ServiceName} bids are unavailable");

        var lot = lotCall.Value;
        var opinions = opinionsCall.Value ?? [];
        var bids = bidsCall.Value ?? [];

        // Перевірки узгодженості між контекстами (дані дублюються, тож можливі розбіжності).
        if (lotCall.Succeeded && lot is null)
            warnings.Add($"Lot {auction.ItemId} referenced by auction {auctionId} was not found in catalog");
        if (lot is not null && lot.Id != auction.ItemId)
            warnings.Add($"Catalog returned lot {lot.Id}, but auction {auctionId} references item {auction.ItemId}");
        if (lot is not null && !string.Equals(lot.Title, auction.ItemTitle, StringComparison.Ordinal))
            warnings.Add($"Item title in auction ('{auction.ItemTitle}') differs from catalog ('{lot.Title}')");

        var foreignOpinions = opinions.Count(o => o.ItemId != auction.ItemId);
        if (foreignOpinions > 0)
        {
            warnings.Add($"{foreignOpinions} expert opinion(s) refer to another item and were skipped");
            opinions = opinions.Where(o => o.ItemId == auction.ItemId).ToList();
        }

        var recentBids = bids
            .OrderByDescending(b => b.PlacedAt)
            .Take(options.Value.RecentBidsCount)
            .ToList();

        var card = new AuctionCardDto(auction, lot, opinions, recentBids, BuildSummary(auction, lot, opinions), warnings);

        activity?.SetTag("card.warnings", warnings.Count);
        logger.LogInformation(
            "Auction card {AuctionId} built for item {ItemId}: lot {LotFound}, {OpinionsCount} opinions, {BidsCount} recent bids, {WarningsCount} warnings",
            auctionId, auction.ItemId, lot is not null, opinions.Count, recentBids.Count, warnings.Count);

        return AuctionCardResult.Ok(card);
    }

    private AuctionCardSummaryDto BuildSummary(AuctionInfoDto auction, LotDto? lot, IReadOnlyList<ExpertOpinionDto> opinions)
    {
        var nextMinimumBid = auction.BidCount == 0 ? auction.StartingPrice : auction.CurrentPrice + auction.BidIncrement;

        string? priceVsEstimate = lot is null ? null
            : auction.CurrentPrice < lot.EstimateLow ? "BelowEstimate"
            : auction.CurrentPrice > lot.EstimateHigh ? "AboveEstimate"
            : "WithinEstimate";

        var consensus = opinions.Count == 0 ? null
            : opinions.GroupBy(o => o.Verdict).OrderByDescending(g => g.Count()).First().Key;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        TimeSpan? timeLeft = auction.Status == "Active" && auction.EndsAt > now ? auction.EndsAt - now : null;

        return new AuctionCardSummaryDto(
            CurrentPrice: auction.CurrentPrice,
            NextMinimumBid: nextMinimumBid,
            Currency: lot?.Currency,
            ReserveMet: auction.ReservePrice is { } reserve ? auction.CurrentPrice >= reserve : null,
            PriceVsEstimate: priceVsEstimate,
            ExpertOpinionsCount: opinions.Count,
            ExpertConsensus: consensus,
            AverageConfidence: opinions.Count == 0 ? null : Math.Round(opinions.Average(o => o.Confidence), 1),
            TotalBids: auction.BidCount,
            TimeLeft: timeLeft);
    }

    // Обгортка виклику сервісу: логування часу відповіді; мережеві помилки й таймаути -> Succeeded = false.
    // Скасування запиту самим клієнтом (ct) не ковтаємо.
    private async Task<ServiceCall<T>> SafeAsync<T>(string service, long auctionId, Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        logger.LogInformation("Calling {Service} for auction {AuctionId}", service, auctionId);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var value = await call(ct);
            logger.LogInformation("{Service} responded in {Elapsed} ms", service, ElapsedMs(startedAt));
            return new ServiceCall<T>(true, value);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException
                                       or TaskCanceledException
                                       or TimeoutRejectedException
                                       or BrokenCircuitException
                                       or JsonException)
        {
            logger.LogWarning("{Service} is unavailable for auction {AuctionId} after {Elapsed} ms: {ExceptionType} {ExceptionMessage}",
                service, auctionId, ElapsedMs(startedAt), ex.GetType().Name, ex.Message);
            return new ServiceCall<T>(false, default);
        }
    }

    private static double ElapsedMs(long startedAt) => Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1);

    private readonly record struct ServiceCall<T>(bool Succeeded, T? Value);
}

public sealed record AuctionCardResult(AuctionCardDto? Card, bool IsNotFound, string? UnavailableReason)
{
    public static AuctionCardResult Ok(AuctionCardDto card) => new(card, false, null);
    public static AuctionCardResult NotFound() => new(null, true, null);
    public static AuctionCardResult Unavailable(string reason) => new(null, false, reason);
}
