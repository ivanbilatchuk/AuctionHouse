using Auctions.Domain.Models;

namespace Auctions.Dal.Interfaces;

public interface IBidderRepository : IGenericRepository<Bidder>
{
    Task<Bidder?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default);

    Task<Bidder?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<AuctionRegistration?> GetRegistrationAsync(long auctionId, long bidderId, CancellationToken cancellationToken = default);

    Task AddRegistrationAsync(AuctionRegistration registration, CancellationToken cancellationToken = default);

    Task UpdateWalletAsync(long bidderId, decimal newBalance, string transactionType, long? auctionId, decimal amount, CancellationToken cancellationToken = default);
}
