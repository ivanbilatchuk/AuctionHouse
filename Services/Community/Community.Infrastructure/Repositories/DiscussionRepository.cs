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

    // Лаба 8: денормалізована назва лота за подією ItemUpdated. Поле itemTitleSyncedAt (поза доменною моделлю,
    // ігнорується під час читання) — час останньої застосованої зміни: старіша подія документ не перезаписує.
    // Version збільшується — паралельне редагування документа через API отримає ConflictException, а не затре назву.
    public async Task<long> UpdateItemTitleAsync(long itemId, string title, DateTime changedAt, CancellationToken ct = default)
    {
        var filter = Builders<Discussion>.Filter;
        var update = Builders<Discussion>.Update
            .Set(d => d.Item.Title, title)
            .Set(TitleSyncedAtField, changedAt)
            .Set(d => d.UpdatedAt, DateTime.UtcNow)
            .Inc(d => d.Version, 1);

        var result = await _collection.UpdateManyAsync(
            filter.Eq(d => d.Item.ItemId, itemId)
            & (filter.Exists(TitleSyncedAtField, false) | filter.Lt(TitleSyncedAtField, changedAt)),
            update, cancellationToken: ct);

        return result.ModifiedCount;
    }

    private const string TitleSyncedAtField = "itemTitleSyncedAt";
}
