using Community.Application.Abstractions.Queries;
using Community.Application.Common;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Community.Infrastructure.Queries;

// Read side: сервер одразу проєктує документи в DTO ($project), пагінація - $skip/$limit.
public sealed class ExpertOpinionQueries(MongoDbContext context) : IExpertOpinionQueries
{
    private static readonly BsonDocument DetailsProjection = new()
    {
        { "_id", new BsonDocument("$toString", "$_id") },
        { "itemId", "$item.itemId" },
        { "itemTitle", "$item.title" },
        { "categoryKind", "$categoryKind" },
        { "expert", "$expert" },
        { "verdict", "$verdict" },
        { "confidence", "$confidence.value" },
        { "attributes", "$attributes" },
        { "notes", "$notes" },
        { "createdAt", "$createdAt" },
        { "updatedAt", "$updatedAt" },
        { "version", "$version" }
    };

    private static readonly BsonDocument ListProjection = new()
    {
        { "_id", new BsonDocument("$toString", "$_id") },
        { "itemId", "$item.itemId" },
        { "itemTitle", "$item.title" },
        { "categoryKind", "$categoryKind" },
        { "expertName", "$expert.displayName" },
        { "verdict", "$verdict" },
        { "confidence", "$confidence.value" },
        { "createdAt", "$createdAt" }
    };

    private readonly IMongoCollection<ExpertOpinion> _collection = context.ExpertOpinions;

    public async Task<ExpertOpinionDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var objectId = ObjectId.Parse(id);

        return await _collection.Aggregate()
            .Match(o => o.Id == objectId)
            .Project<ExpertOpinionDto>(DetailsProjection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<ExpertOpinionListItemDto>> GetListAsync(ExpertOpinionFilter filter, CancellationToken ct = default)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var match = BuildFilter(filter);

        var items = await _collection.Aggregate()
            .Match(match)
            .Sort(BuildSort(filter))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project<ExpertOpinionListItemDto>(ListProjection)
            .ToListAsync(ct);

        var total = await _collection.CountDocumentsAsync(match, cancellationToken: ct);

        return new PagedResult<ExpertOpinionListItemDto>(items, total, page, pageSize);
    }

    private static FilterDefinition<ExpertOpinion> BuildFilter(ExpertOpinionFilter f)
    {
        var builder = Builders<ExpertOpinion>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(f.Search))
            filter &= builder.Text(f.Search.Trim());
        if (f.ItemId is { } itemId)
            filter &= builder.Eq(o => o.Item.ItemId, itemId);
        if (Enum.TryParse<CategoryKind>(f.CategoryKind, ignoreCase: true, out var kind))
            filter &= builder.Eq(o => o.CategoryKind, kind);
        if (Enum.TryParse<Verdict>(f.Verdict, ignoreCase: true, out var verdict))
            filter &= builder.Eq(o => o.Verdict, verdict);
        if (f.MinConfidence is { } minConfidence)
            filter &= builder.Gte(o => o.Confidence.Value, minConfidence);

        return filter;
    }

    private static SortDefinition<ExpertOpinion> BuildSort(ExpertOpinionFilter f)
    {
        var sort = Builders<ExpertOpinion>.Sort;
        var descending = string.Equals(f.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

        var primary = string.Equals(f.SortBy, "confidence", StringComparison.OrdinalIgnoreCase)
            ? (descending ? sort.Descending(o => o.Confidence.Value) : sort.Ascending(o => o.Confidence.Value))
            : (descending ? sort.Descending(o => o.CreatedAt) : sort.Ascending(o => o.CreatedAt));

        return sort.Combine(primary, sort.Ascending(o => o.Id));
    }
}
