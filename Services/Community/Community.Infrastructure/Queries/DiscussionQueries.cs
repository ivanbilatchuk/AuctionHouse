using Community.Application.Abstractions.Queries;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Community.Infrastructure.Queries;

public sealed class DiscussionQueries(MongoDbContext context) : IDiscussionQueries
{
    private static readonly BsonDocument Projection = new()
    {
        { "_id", new BsonDocument("$toString", "$_id") },
        { "auctionId", "$auctionId" },
        { "itemId", "$item.itemId" },
        { "itemTitle", "$item.title" },
        { "status", "$status" },
        { "commentCount", "$commentCount" },
        { "comments", MapComments("$comments", "c", includeReplies: true) },
        { "createdAt", "$createdAt" },
        { "updatedAt", "$updatedAt" },
        { "version", "$version" }
    };

    private readonly IMongoCollection<Discussion> _collection = context.Discussions;

    public async Task<DiscussionDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var objectId = ObjectId.Parse(id);

        return await _collection.Aggregate()
            .Match(d => d.Id == objectId)
            .Project<DiscussionDto>(Projection)
            .FirstOrDefaultAsync(ct);
    }

    // Вкладені масиви comments/replies проєктуються в CommentDto на сервері ($map).
    private static BsonDocument MapComments(string input, string alias, bool includeReplies)
    {
        var item = new BsonDocument
        {
            { "_id", new BsonDocument("$toString", $"$${alias}._id") },
            { "author", $"$${alias}.author" },
            { "text", $"$${alias}.text" },
            { "createdAt", $"$${alias}.createdAt" }
        };

        if (includeReplies)
            item.Add("replies", MapComments($"$${alias}.replies", "r", includeReplies: false));

        return new BsonDocument("$map", new BsonDocument
        {
            { "input", new BsonDocument("$ifNull", new BsonArray { input, new BsonArray() }) },
            { "as", alias },
            { "in", item }
        });
    }
}
