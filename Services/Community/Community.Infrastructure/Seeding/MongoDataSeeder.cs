using Community.Domain.Entities;
using Community.Domain.ValueObjects;
using Community.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Community.Infrastructure.Seeding;

// Ідемпотентний сідінг: колекція наповнюється InsertMany лише якщо вона порожня.
public sealed class MongoDataSeeder(MongoDbContext context, ILogger<MongoDataSeeder> logger) : IDataSeeder
{
    private static readonly Person ExpertSavchuk = new(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Dr. Andriy Savchuk");
    private static readonly Person ExpertKoval = new(Guid.Parse("66666666-6666-6666-6666-666666666666"), "Olha Koval");
    private static readonly Person Seller = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Vintage Vault");
    private static readonly Person Marko = new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Marko Bondar");
    private static readonly Person Olena = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Olena Hrytsenko");

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedExpertOpinionsAsync(ct);
        await SeedDiscussionsAsync(ct);
    }

    private async Task SeedExpertOpinionsAsync(CancellationToken ct)
    {
        if (await context.ExpertOpinions.Find(FilterDefinition<ExpertOpinion>.Empty).Limit(1).AnyAsync(ct))
        {
            logger.LogInformation("Seeding of {Collection} skipped: data already exists", MongoDbContext.ExpertOpinionsCollection);
            return;
        }

        var opinions = new List<ExpertOpinion>
        {
            Opinion(1, "Charizard Holo 1st Edition, Base Set 1999 (PSA 9)", CategoryKind.Cards, ExpertSavchuk, Verdict.Authentic, 97,
                new() { ["gradingCompany"] = "PSA", ["grade"] = "9" }, "Slab and label verified against the PSA registry."),
            Opinion(1, "Charizard Holo 1st Edition, Base Set 1999 (PSA 9)", CategoryKind.Cards, ExpertKoval, Verdict.Authentic, 92,
                new() { ["gradingCompany"] = "PSA", ["grade"] = "9" }, null),
            Opinion(2, "Penny Black, 1840, Great Britain", CategoryKind.Stamps, ExpertSavchuk, Verdict.Authentic, 88,
                new() { ["perforation"] = "imperforate", ["watermark"] = "small crown" }, "Plate 1a, four clear margins."),
            Opinion(3, "Pink Floyd - The Dark Side of the Moon, UK 1st Press, 1973", CategoryKind.Vinyl, ExpertKoval, Verdict.Questionable, 60,
                new() { ["matrixNumber"] = "SHVL 804 A-2", ["pressing"] = "UK 1st" }, "Matrix suggests a later reissue; sleeve looks original."),
            Opinion(4, "Blastoise Holo 1st Edition, Base Set 1999 (PSA 8)", CategoryKind.Cards, ExpertSavchuk, Verdict.Authentic, 90,
                new() { ["gradingCompany"] = "PSA", ["grade"] = "8" }, null),
            Opinion(6, "Black Lotus, Alpha Edition 1993", CategoryKind.Cards, ExpertKoval, Verdict.Counterfeit, 85,
                new() { ["gradingCompany"] = "BGS", ["grade"] = "7" }, "Print rosette pattern and cardstock do not match Alpha."),
            Opinion(7, "Inverted Jenny, 1918, United States", CategoryKind.Stamps, ExpertSavchuk, Verdict.Authentic, 95,
                new() { ["perforation"] = "11", ["watermark"] = "none" }, "Position 49 of the sheet, certificate PF-0546781."),
            Opinion(9, "The Beatles - Abbey Road, UK 1st Press, 1969", CategoryKind.Vinyl, ExpertSavchuk, Verdict.Authentic, 80,
                new() { ["matrixNumber"] = "YEX 749-1", ["pressing"] = "UK 1st" }, null),
            Opinion(11, "1933 Double Eagle Gold Coin", CategoryKind.Coins, ExpertKoval, Verdict.Questionable, 55,
                new() { ["metal"] = "gold", ["weightGrams"] = "33.44" }, "Weight matches, but provenance documents are incomplete."),
            Opinion(12, "Ukrainian Hryvnia Silver Commemorative Coin, 1996", CategoryKind.Coins, ExpertSavchuk, Verdict.Authentic, 99,
                new() { ["metal"] = "silver", ["weightGrams"] = "31.1" }, null)
        };

        await context.ExpertOpinions.InsertManyAsync(opinions, cancellationToken: ct);
        logger.LogInformation("Seeded {Count} documents into {Collection}", opinions.Count, MongoDbContext.ExpertOpinionsCollection);
    }

    private async Task SeedDiscussionsAsync(CancellationToken ct)
    {
        if (await context.Discussions.Find(FilterDefinition<Discussion>.Empty).Limit(1).AnyAsync(ct))
        {
            logger.LogInformation("Seeding of {Collection} skipped: data already exists", MongoDbContext.DiscussionsCollection);
            return;
        }

        var charizard = new Discussion(1, new ItemReference(1, "Charizard Holo 1st Edition, Base Set 1999 (PSA 9)"));
        var question = charizard.AddComment(Marko, "Is the PSA slab free of scratches? Could you add a photo of the back?");
        charizard.Reply(question.Id, Seller, "Yes, the case is clean. Back photo added to the lot gallery.");
        charizard.Reply(question.Id, Marko, "Thank you, that settles it.");
        charizard.AddComment(Olena, "Two independent expert opinions confirm authenticity - great lot.");

        var pennyBlack = new Discussion(2, new ItemReference(2, "Penny Black, 1840, Great Britain"));
        pennyBlack.AddComment(Olena, "Which plate number is it?");

        var discussions = new List<Discussion> { charizard, pennyBlack };

        await context.Discussions.InsertManyAsync(discussions, cancellationToken: ct);
        logger.LogInformation("Seeded {Count} documents into {Collection}", discussions.Count, MongoDbContext.DiscussionsCollection);
    }

    private static ExpertOpinion Opinion(long itemId, string title, CategoryKind kind, Person expert, Verdict verdict,
        int confidence, Dictionary<string, string> attributes, string? notes)
        => new(new ItemReference(itemId, title), kind, expert, verdict, new Confidence(confidence), attributes, notes);
}
