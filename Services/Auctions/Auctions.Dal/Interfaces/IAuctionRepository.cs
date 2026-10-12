using Auctions.Domain.Models;

namespace Auctions.Dal.Interfaces;

public interface IAuctionRepository : IGenericRepository<Auction>
{
    Task<Auction?> GetWithDetailsAsync(long id, bool forUpdate = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Auction>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

    Task<bool> HasUnfinishedAuctionForItemAsync(long itemId, CancellationToken cancellationToken = default);

    Task ApplyBidAsync(long auctionId, decimal amount, long bidderId, DateTime endsAt, CancellationToken cancellationToken = default);

    Task<string> ChangeStatusAsync(long auctionId, string newStatus, string? reason, CancellationToken cancellationToken = default);
}
