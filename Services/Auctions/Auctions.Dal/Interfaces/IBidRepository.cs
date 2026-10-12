using Auctions.Domain.Models;

namespace Auctions.Dal.Interfaces;

public interface IBidRepository
{
    Task<Bid?> GetByIdAsync(long bidId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bid>> GetByAuctionAsync(long auctionId, CancellationToken cancellationToken = default);

    Task<Bid?> GetHighestAsync(long auctionId, CancellationToken cancellationToken = default);

    Task ClearWinningFlagAsync(long auctionId, CancellationToken cancellationToken = default);

    Task<long> AddAsync(Bid bid, CancellationToken cancellationToken = default);
}
