using Community.Domain.Entities;
using Community.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Community.Infrastructure.Indexes;

// Автоматичне створення складених, text- та unique-індексів під час старту (ідемпотентно: той самий ключ і назва - no-op).
public sealed class MongoIndexInitializer(MongoDbContext context, ILogger<MongoIndexInitializer> logger)
{
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var opinionKeys = Builders<ExpertOpinion>.IndexKeys;
        await context.ExpertOpinions.Indexes.CreateManyAsync(
        [
            // Складений: «висновки по лоту, найновіші першими».
            new CreateIndexModel<ExpertOpinion>(
                opinionKeys.Ascending(o => o.Item.ItemId).Descending(o => o.CreatedAt),
                new CreateIndexOptions { Name = "ix_itemId_createdAt" }),
            // Unique: один експерт - один висновок на лот.
            new CreateIndexModel<ExpertOpinion>(
                opinionKeys.Ascending(o => o.Item.ItemId).Ascending(o => o.Expert.UserId),
                new CreateIndexOptions { Name = "ux_itemId_expert", Unique = true }),
            // Складений: модерація за категорією та вердиктом.
            new CreateIndexModel<ExpertOpinion>(
                opinionKeys.Ascending(o => o.CategoryKind).Ascending(o => o.Verdict),
                new CreateIndexOptions { Name = "ix_category_verdict" }),
            // Text: повнотекстовий пошук ?search= за назвою лота та нотатками експерта.
            new CreateIndexModel<ExpertOpinion>(
                opinionKeys.Text(o => o.Item.Title).Text(o => o.Notes),
                new CreateIndexOptions { Name = "tx_title_notes" })
        ], ct);

        var discussionKeys = Builders<Discussion>.IndexKeys;
        await context.Discussions.Indexes.CreateManyAsync(
        [
            // Unique: одне обговорення на аукціон.
            new CreateIndexModel<Discussion>(
                discussionKeys.Ascending(d => d.AuctionId),
                new CreateIndexOptions { Name = "ux_auctionId", Unique = true }),
            new CreateIndexModel<Discussion>(
                discussionKeys.Ascending(d => d.Item.ItemId),
                new CreateIndexOptions { Name = "ix_itemId" })
        ], ct);

        logger.LogInformation("MongoDB indexes are ensured for {Collections}",
            new[] { MongoDbContext.ExpertOpinionsCollection, MongoDbContext.DiscussionsCollection });
    }
}
