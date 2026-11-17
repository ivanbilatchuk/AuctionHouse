using MediatR;

namespace Community.Application.Caching;

// Cache-aside для запитів, що реалізують ICacheableQuery: L1 -> L2 -> обробник (MongoDB).
// Стоїть після ValidationBehavior, тож невалідні запити до кешу не доходять.
public sealed class CachingBehavior<TRequest, TResponse>(TwoLevelCache cache) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheable || cacheable.GetCacheDescriptor() is not { } descriptor)
            return await next();

        var key = descriptor.Key;
        if (descriptor.VersionKey is { } versionKey)
        {
            var version = await cache.GetOrCreateAsync(versionKey,
                _ => Task.FromResult(new CacheVersion(Guid.NewGuid().ToString("N")[..8])),
                CommunityCacheKeys.VersionSettings, cancellationToken);
            key = $"{key}:v:{version.Value}";
        }

        var cached = await cache.GetOrCreateAsync(key,
            async _ => new CachedResponse<TResponse>(await next()),
            descriptor.Settings, cancellationToken);

        return cached.Value;
    }
}

public sealed record CacheVersion(string Value);

// Обгортка, бо TwoLevelCache працює з reference-типами, а TResponse може бути будь-яким.
public sealed record CachedResponse<T>(T Value);
