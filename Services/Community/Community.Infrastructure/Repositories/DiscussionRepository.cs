using Community.Domain.Entities;
using Community.Domain.Exceptions;
using Community.Domain.Repositories;
using Community.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Community.Infrastructure.Repositories;

public sealed class DiscussionRepository(MongoDbContext context) : IDiscussionRepository
{
    private readonly IMongoCollection<Discussion> _collection = context.Discussions;

    public async Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(ct);

    public async Task<bool> ExistsForAuctionAsync(long auctionId, CancellationToken ct = default)
        => await _collection.Find(d => d.AuctionId == auctionId).Limit(1).AnyAsync(ct);

    public Task AddAsync(Discussion discussion, CancellationToken ct = default)
        => _collection.InsertOneAsync(discussion, cancellationToken: ct);

    public async Task UpdateAsync(Discussion discussion, int expectedVersion, CancellationToken ct = default)
    {
        var filter = Builders<Discussion>.Filter.Eq(d => d.Id, discussion.Id)
                     & Builders<Discussion>.Filter.Eq(d => d.Version, expectedVersion);

        var result = await _collection.ReplaceOneAsync(filter, discussion, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new ConflictException($"Discussion {discussion.Id} was modified by another request (expected version {expectedVersion}).");
    }
}
