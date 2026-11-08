using Auctions.Bll.DTO;
using Auctions.Bll.Services;

namespace Auctions.Bll.Caching;

// Декоратор IAuctionService: cache-aside для картки аукціону, інвалідація одразу після змін.
public sealed class CachedAuctionService(AuctionService inner, TwoLevelCache cache) : IAuctionService
{
    public Task<IReadOnlyList<AuctionSummaryDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default)
        => inner.GetAllAsync(status, cancellationToken);

    public Task<AuctionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(AuctionsCacheKeys.Auction(id), ct => inner.GetByIdAsync(id, ct),
            AuctionsCacheKeys.AuctionSettings, cancellationToken);

    public Task<AuctionDto> CreateAsync(CreateAuctionDto dto, CancellationToken cancellationToken = default)
        => inner.CreateAsync(dto, cancellationToken);

    public async Task<AuctionDto> UpdateAsync(long id, UpdateAuctionDto dto, CancellationToken cancellationToken = default)
    {
        var updated = await inner.UpdateAsync(id, dto, cancellationToken);
        await cache.InvalidateAsync(AuctionsCacheKeys.Auction(id), "auction updated", cancellationToken);
        return updated;
    }

    public async Task<AuctionDto> ChangeStatusAsync(long id, ChangeAuctionStatusDto dto, CancellationToken cancellationToken = default)
    {
        var changed = await inner.ChangeStatusAsync(id, dto, cancellationToken);
        await cache.InvalidateAsync(AuctionsCacheKeys.Auction(id), $"auction status changed to {changed.Status}", cancellationToken);
        return changed;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await inner.DeleteAsync(id, cancellationToken);
        await cache.InvalidateAsync([AuctionsCacheKeys.Auction(id), AuctionsCacheKeys.Bids(id)], "auction deleted", cancellationToken);
    }
}
