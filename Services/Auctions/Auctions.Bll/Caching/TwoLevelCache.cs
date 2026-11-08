using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Auctions.Bll.Caching;

// Дворівневий cache-aside: L1 (IMemoryCache) -> L2 (Redis через IDistributedCache) -> джерело (БД).
// На зворотному шляху заповнюються обидва рівні. Збій Redis не валить сервіс: він логується,
// L2 на короткий час вимикається, а дані беруться з БД.
public sealed class TwoLevelCache(
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    CacheMetrics metrics,
    TimeProvider timeProvider,
    ILogger<TwoLevelCache> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan L2CooldownAfterFailure = TimeSpan.FromSeconds(30);

    private long _l2DisabledUntilTicks;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CacheEntrySettings settings, CancellationToken ct) where T : class
    {
        if (memoryCache.TryGetValue(key, out T? fromMemory) && fromMemory is not null)
        {
            metrics.Hit(CacheMetrics.L1, key);
            logger.LogDebug("Cache hit {CacheKey} {CacheLevel}", key, CacheMetrics.L1);
            return fromMemory;
        }

        metrics.Miss(CacheMetrics.L1, key);
        logger.LogDebug("Cache miss {CacheKey} {CacheLevel}", key, CacheMetrics.L1);

        var fromRedis = await TryGetFromL2Async<T>(key, ct);
        if (fromRedis is not null)
        {
            SetL1(key, fromRedis, settings);
            return fromRedis;
        }

        // Промах на обох рівнях — читаємо з БД (винятки, напр. "не знайдено", не кешуються).
        var value = await factory(ct);

        await TrySetL2Async(key, value, settings, ct);
        SetL1(key, value, settings);

        return value;
    }

    // Синхронна інвалідація одразу після запису: видаляємо ключ з обох рівнів.
    public async Task InvalidateAsync(IEnumerable<string> keys, string reason, CancellationToken ct)
    {
        foreach (var key in keys)
        {
            memoryCache.Remove(key);

            if (IsL2Available())
            {
                try
                {
                    await distributedCache.RemoveAsync(key, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    DisableL2(ex, key, "remove");
                }
            }

            metrics.Invalidated(key);
            logger.LogInformation("Cache invalidated {CacheKey}, reason {Reason}", key, reason);
        }
    }

    public Task InvalidateAsync(string key, string reason, CancellationToken ct) => InvalidateAsync([key], reason, ct);

    private void SetL1<T>(string key, T value, CacheEntrySettings settings) where T : class
    {
        memoryCache.Set(key, value, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = settings.L1Sliding,
            AbsoluteExpirationRelativeToNow = settings.L1Absolute
        });
    }

    private async Task<T?> TryGetFromL2Async<T>(string key, CancellationToken ct) where T : class
    {
        if (!IsL2Available())
            return null;

        try
        {
            var bytes = await distributedCache.GetAsync(key, ct);
            if (bytes is null)
            {
                metrics.Miss(CacheMetrics.L2, key);
                logger.LogDebug("Cache miss {CacheKey} {CacheLevel}", key, CacheMetrics.L2);
                return null;
            }

            metrics.Hit(CacheMetrics.L2, key);
            logger.LogDebug("Cache hit {CacheKey} {CacheLevel}", key, CacheMetrics.L2);
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Зіпсований або застарілий формат — вважаємо промахом, запис буде перезаписано.
            logger.LogWarning("Cache entry {CacheKey} in L2 could not be deserialized: {ExceptionMessage}", key, ex.Message);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            DisableL2(ex, key, "get");
            return null;
        }
    }

    private async Task TrySetL2Async<T>(string key, T value, CacheEntrySettings settings, CancellationToken ct)
    {
        if (!IsL2Available())
            return;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            await distributedCache.SetAsync(key, bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = settings.L2Ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            DisableL2(ex, key, "set");
        }
    }

    private bool IsL2Available() => timeProvider.GetUtcNow().UtcTicks >= Interlocked.Read(ref _l2DisabledUntilTicks);

    private void DisableL2(Exception ex, string key, string operation)
    {
        var until = timeProvider.GetUtcNow().Add(L2CooldownAfterFailure);
        Interlocked.Exchange(ref _l2DisabledUntilTicks, until.UtcTicks);
        logger.LogWarning("Redis (L2) {CacheOperation} failed for {CacheKey}: {ExceptionType} {ExceptionMessage}. " +
                          "Falling back to database, L2 is skipped until {L2RetryAt}",
            operation, key, ex.GetType().Name, ex.Message, until);
    }
}
