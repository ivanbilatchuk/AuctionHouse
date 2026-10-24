using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Seeding;

// Програмний сідінг після MigrateAsync: ідемпотентний (перевірка наявності), довідники вже вставлені через HasData.
public static class DbInitializer
{
    private static readonly Guid SellerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SellerB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        if (await db.Items.IgnoreQueryFilters().AnyAsync(ct))
            return;

        var items = new List<Item>
        {
            Create(2, SellerA, "PKM-BS-004-1ED", "Charizard Holo 1st Edition, Base Set 1999 (PSA 9)", "charizard-holo-1st-edition-base-set-1999-psa-9",
                8000m, 12000m, ItemCondition.Mint, 1999, "USA", "PSA-60000001", [1, 2, 3, 5], ItemStatus.InAuction),
            Create(3, SellerA, "STP-GB-1840-PB", "Penny Black, 1840, Great Britain", "penny-black-1840-great-britain",
                300m, 600m, ItemCondition.Excellent, 1840, "United Kingdom", null, [3, 5], ItemStatus.Listed),
            Create(4, SellerA, "VNL-PF-DSOTM-UK1", "Pink Floyd - The Dark Side of the Moon, UK 1st Press, 1973", "pink-floyd-dark-side-of-the-moon-uk-1st-press-1973",
                1500m, 2500m, ItemCondition.NearMint, 1973, "United Kingdom", null, [4], ItemStatus.Draft),
            Create(2, SellerB, "PKM-BS-002-1ED", "Blastoise Holo 1st Edition, Base Set 1999 (PSA 8)", "blastoise-holo-1st-edition-base-set-1999-psa-8",
                3000m, 4500m, ItemCondition.NearMint, 1999, "USA", "PSA-60000002", [1, 2, 5], ItemStatus.Listed),
            Create(2, SellerB, "PKM-JU-010-1ED", "Mewtwo Holo, Jungle Set 1999", "mewtwo-holo-jungle-set-1999",
                250m, 400m, ItemCondition.Excellent, 1999, "USA", null, [3], ItemStatus.Listed),
            Create(1, SellerA, "TCG-MTG-LEA-BL", "Black Lotus, Alpha Edition 1993", "black-lotus-alpha-edition-1993",
                20000m, 35000m, ItemCondition.Good, 1993, "USA", "BGS-00012345", [3, 2], ItemStatus.Listed),
            Create(3, SellerB, "STP-US-1918-JI", "Inverted Jenny, 1918, United States", "inverted-jenny-1918-united-states",
                150000m, 250000m, ItemCondition.Good, 1918, "USA", "PF-0546781", [3, 5], ItemStatus.Listed),
            Create(3, SellerB, "STP-UA-1918-SH", "Shahiv Issue, 1918, Ukrainian People's Republic", "shahiv-issue-1918-ukrainian-peoples-republic",
                40m, 90m, ItemCondition.Fair, 1918, "Ukraine", null, [4], ItemStatus.Draft),
            Create(4, SellerA, "VNL-BT-ABBEY-UK1", "The Beatles - Abbey Road, UK 1st Press, 1969", "the-beatles-abbey-road-uk-1st-press-1969",
                800m, 1200m, ItemCondition.Excellent, 1969, "United Kingdom", null, [1], ItemStatus.Listed),
            Create(4, SellerB, "VNL-NV-NEVER-US1", "Nirvana - Nevermind, US 1st Press, 1991", "nirvana-nevermind-us-1st-press-1991",
                400m, 700m, ItemCondition.NearMint, 1991, "USA", null, [1, 4], ItemStatus.Withdrawn),
            Create(5, SellerA, "CN-US-1933-DE", "1933 Double Eagle Gold Coin", "1933-double-eagle-gold-coin",
                5000m, 9000m, ItemCondition.Excellent, 1933, "USA", "NGC-1933-0001", [3, 5], ItemStatus.Listed),
            Create(5, SellerB, "CN-UA-1996-HR", "Ukrainian Hryvnia Silver Commemorative Coin, 1996", "ukrainian-hryvnia-silver-commemorative-coin-1996",
                60m, 120m, ItemCondition.Mint, 1996, "Ukraine", null, [4], ItemStatus.Listed),
        };

        db.Items.AddRange(items);
        await db.SaveChangesAsync(ct);
    }

    private static Item Create(long categoryId, Guid seller, string sku, string title, string slug,
        decimal low, decimal high, ItemCondition condition, short year, string country, string? certificate,
        long[] tagIds, ItemStatus status)
    {
        var item = new Item(categoryId, seller, sku, title, slug, low, high);
        item.SetDetail(condition, year, country, certificate, "Seed data for the AuctionHouse catalog.", title);
        item.AddImage($"https://img.example.com/items/{slug}/front.jpg", isPrimary: true);
        item.AddImage($"https://img.example.com/items/{slug}/back.jpg");

        foreach (var tagId in tagIds)
            item.AddTag(tagId);

        switch (status)
        {
            case ItemStatus.Listed:
                item.List();
                break;
            case ItemStatus.InAuction:
                item.List();
                item.SendToAuction();
                break;
            case ItemStatus.Withdrawn:
                item.Withdraw();
                break;
        }

        return item;
    }
}
