using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public sealed class ItemTagRepository(CatalogDbContext context) : GenericRepository<ItemTag>(context), IItemTagRepository
{
    // LINQ to Entities через проміжну сутність M:N: відбір за її власним полем AddedAt.
    public async Task<IReadOnlyList<ItemTag>> GetRecentlyTaggedAsync(string tagSlug, DateTime since, CancellationToken ct = default)
        => await Set
            .Where(it => it.Tag.Slug == tagSlug && it.AddedAt >= since)
            .OrderByDescending(it => it.AddedAt)
            .Include(it => it.Item)
            .Include(it => it.Tag)
            .AsNoTracking()
            .ToListAsync(ct);
}
