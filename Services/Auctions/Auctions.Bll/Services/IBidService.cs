using Auctions.Bll.DTO;

namespace Auctions.Bll.Services;

public interface IBidService
{
    Task<IReadOnlyList<BidDto>> GetByAuctionAsync(long auctionId, CancellationToken cancellationToken = default);

    Task<BidDto> GetByIdAsync(long auctionId, long bidId, CancellationToken cancellationToken = default);

    Task<BidDto> PlaceBidAsync(long auctionId, PlaceBidDto dto, CancellationToken cancellationToken = default);
}
