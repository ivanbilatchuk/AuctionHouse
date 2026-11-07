using Community.Domain.Entities;
using Community.Domain.Exceptions;
using Community.Domain.Repositories;
using Community.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Community.Infrastructure.Repositories;

public sealed class ExpertOpinionRepository(MongoDbContext context) : IExpertOpinionRepository
{
    private readonly IMongoCollection<ExpertOpinion> _collection = context.ExpertOpinions;

    public async Task<ExpertOpinion?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _collection.Find(o => o.Id == id).FirstOrDefaultAsync(ct);

    public async Task<bool> ExistsForExpertAsync(long itemId, Guid expertUserId, CancellationToken ct = default)
        => await _collection
            .Find(o => o.Item.ItemId == itemId && o.Expert.UserId == expertUserId)
            .Limit(1)
            .AnyAsync(ct);

    public Task AddAsync(ExpertOpinion opinion, CancellationToken ct = default)
        => _collection.InsertOneAsync(opinion, cancellationToken: ct);

    // Оптимістична конкурентність: фільтр за _id І очікуваною версією; якщо документ уже змінено - 0 збігів.
    public async Task UpdateAsync(ExpertOpinion opinion, int expectedVersion, CancellationToken ct = default)
    {
        var filter = Builders<ExpertOpinion>.Filter.Eq(o => o.Id, opinion.Id)
                     & Builders<ExpertOpinion>.Filter.Eq(o => o.Version, expectedVersion);

        var result = await _collection.ReplaceOneAsync(filter, opinion, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new ConflictException($"Expert opinion {opinion.Id} was modified by another request (expected version {expectedVersion}).");
    }

    public async Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default)
    {
        var result = await _collection.DeleteOneAsync(o => o.Id == id, ct);
        return result.DeletedCount > 0;
    }
}
