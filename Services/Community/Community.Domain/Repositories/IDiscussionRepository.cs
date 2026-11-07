using Community.Domain.Entities;
using MongoDB.Bson;

namespace Community.Domain.Repositories;

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task<bool> ExistsForAuctionAsync(long auctionId, CancellationToken ct = default);

    Task AddAsync(Discussion discussion, CancellationToken ct = default);

    Task UpdateAsync(Discussion discussion, int expectedVersion, CancellationToken ct = default);
}
