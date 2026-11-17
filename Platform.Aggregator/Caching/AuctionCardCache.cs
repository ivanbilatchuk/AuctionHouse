using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Dto;

namespace Platform.Aggregator.Caching;

// Кеш готової картки аукціону в Redis (IDistributedCache, System.Text.Json). Збій Redis не ламає агрегатор:
// помилка логується, а картка збирається з сервісів.
public sealed class AuctionCardCache(IDistributedCache cache, CacheMetrics metrics, ILogger<AuctionCardCache> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuctionCardDto?> GetAsync(long auctionId, string transport, CancellationToken ct)
    {
        var key = AggregatorCacheKeys.AuctionCard(auctionId, transport);
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is null)
            {
                metrics.Miss();
                logger.LogDebug("Cache miss {CacheKey} {CacheLevel}", key, CacheMetrics.L2);
                return null;
            }

            metrics.Hit();
            logger.LogDebug("Cache hit {CacheKey} {CacheLevel}", key, CacheMetrics.L2);
            return JsonSerializer.Deserialize<AuctionCardDto>(bytes, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Redis read failed for {CacheKey}: {ExceptionType} {ExceptionMessage}; building card from services",
                key, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    // Кешуються лише повні картки: відповідь із попередженнями (частковий збій) не має "прилипати" на 30 с.
    public async Task SetAsync(AuctionCardDto card, string transport, CancellationToken ct)
    {
        var key = AggregatorCacheKeys.AuctionCard(card.Auction.AuctionId, transport);
        if (card.Warnings.Count > 0)
        {
            logger.LogDebug("Card {CacheKey} has {WarningsCount} warnings and is not cached", key, card.Warnings.Count);
            return;
        }

        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(card, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = AggregatorCacheKeys.AuctionCardTtl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Redis write failed for {CacheKey}: {ExceptionType} {ExceptionMessage}", key, ex.GetType().Name, ex.Message);
        }
    }
}
