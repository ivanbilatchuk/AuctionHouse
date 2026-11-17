using System.Diagnostics.Metrics;
using Catalog.Bll.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Catalog.Tests;

// Лаба 6: L1 -> L2 -> джерело, заповнення рівнів на зворотному шляху, інвалідація і fallback при збої Redis.
public class TwoLevelCacheTests
{
    private static readonly CacheEntrySettings Settings =
        new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

    private sealed record Payload(long Id, string Title);

    private static (TwoLevelCache Cache, MemoryCache L1) Create(IDistributedCache l2)
    {
        var l1 = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        var cache = new TwoLevelCache(l1, l2, new CacheMetrics(meterFactory), TimeProvider.System, NullLogger<TwoLevelCache>.Instance);
        return (cache, l1);
    }

    private static IDistributedCache NewRedisStub() => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task Miss_on_both_levels_calls_source_once_and_fills_L1_and_L2()
    {
        var l2 = NewRedisStub();
        var (cache, l1) = Create(l2);
        var calls = 0;

        Task<Payload> Source(CancellationToken _) { calls++; return Task.FromResult(new Payload(1, "Charizard")); }

        var first = await cache.GetOrCreateAsync("catalog:item:1", Source, Settings, CancellationToken.None);
        var second = await cache.GetOrCreateAsync("catalog:item:1", Source, Settings, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(first, second);
        Assert.True(l1.TryGetValue("catalog:item:1", out _));
        Assert.NotNull(await l2.GetAsync("catalog:item:1"));
    }

    [Fact]
    public async Task L2_hit_refills_L1_without_calling_source()
    {
        var l2 = NewRedisStub();
        var (warm, _) = Create(l2);
        await warm.GetOrCreateAsync("catalog:item:2", _ => Task.FromResult(new Payload(2, "Stamp")), Settings, CancellationToken.None);

        // Новий екземпляр сервісу: порожній L1, але спільний Redis.
        var (cold, coldL1) = Create(l2);
        var value = await cold.GetOrCreateAsync<Payload>("catalog:item:2",
            _ => throw new InvalidOperationException("source must not be called"), Settings, CancellationToken.None);

        Assert.Equal(new Payload(2, "Stamp"), value);
        Assert.True(coldL1.TryGetValue("catalog:item:2", out _));
    }

    [Fact]
    public async Task Invalidate_removes_key_from_both_levels()
    {
        var l2 = NewRedisStub();
        var (cache, _) = Create(l2);
        await cache.GetOrCreateAsync("catalog:item:3", _ => Task.FromResult(new Payload(3, "Old")), Settings, CancellationToken.None);

        await cache.InvalidateAsync("catalog:item:3", "item updated", CancellationToken.None);
        var fresh = await cache.GetOrCreateAsync("catalog:item:3", _ => Task.FromResult(new Payload(3, "New")), Settings, CancellationToken.None);

        Assert.Equal("New", fresh.Title);
    }

    [Fact]
    public async Task Redis_failure_falls_back_to_source()
    {
        var (cache, _) = Create(new FailingDistributedCache());

        var value = await cache.GetOrCreateAsync("catalog:item:4", _ => Task.FromResult(new Payload(4, "Coin")), Settings, CancellationToken.None);
        await cache.InvalidateAsync("catalog:item:4", "item deleted", CancellationToken.None);

        Assert.Equal(4, value.Id);
    }

    [Fact]
    public async Task Source_exception_is_not_cached()
    {
        var (cache, l1) = Create(NewRedisStub());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            cache.GetOrCreateAsync<Payload>("catalog:item:5", _ => throw new KeyNotFoundException(), Settings, CancellationToken.None));

        Assert.False(l1.TryGetValue("catalog:item:5", out _));
    }

    private sealed class FailingDistributedCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("Redis is down");
        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
    }
}
