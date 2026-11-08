namespace Community.Application.Caching;

// Час життя запису на кожному рівні. L1 (пам'ять процесу) завжди коротший за L2 (Redis).
public sealed record CacheEntrySettings(TimeSpan L1Sliding, TimeSpan L1Absolute, TimeSpan L2Ttl)
{
    public CacheEntrySettings Validate()
    {
        if (L1Sliding > L1Absolute || L1Absolute >= L2Ttl)
            throw new ArgumentException("Expected L1 sliding <= L1 absolute < L2 TTL.");
        return this;
    }
}
