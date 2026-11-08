using Auctions.Bll.DTO;
using Auctions.Bll.Services;

namespace Auctions.Bll.Caching;

// Декоратор IBidService: кешує список ставок аукціону; нова ставка змінює і ставки, і ціну аукціону —
// тому видаляються обидва ключі.
public sealed class CachedBidService(BidService inner, TwoLevelCache cache) : IBidService
{
    public Task<IReadOnlyList<BidDto>> GetByAuctionAsync(long auctionId, CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(AuctionsCacheKeys.Bids(auctionId), ct => inner.GetByAuctionAsync(auctionId, ct),
            AuctionsCacheKeys.BidsSettings, cancellationToken);

    public Task<BidDto> GetByIdAsync(long auctionId, long bidId, CancellationToken cancellationToken = default)
        => inner.GetByIdAsync(auctionId, bidId, cancellationToken);

    public async Task<BidDto> PlaceBidAsync(long auctionId, PlaceBidDto dto, CancellationToken cancellationToken = default)
    {
        var bid = await inner.PlaceBidAsync(auctionId, dto, cancellationToken);
        await cache.InvalidateAsync([AuctionsCacheKeys.Auction(auctionId), AuctionsCacheKeys.Bids(auctionId)],
            $"bid {bid.BidId} placed", cancellationToken);
        return bid;
    }
}
