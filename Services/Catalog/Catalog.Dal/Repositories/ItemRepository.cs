using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public sealed class ItemRepository(CatalogDbContext context) : GenericRepository<Item>(context), IItemRepository
{
    // Eager Loading: картка лота з усім пов'язаним одним SQL-запитом (Include / ThenInclude).
    public async Task<Item?> GetWithDetailsAsync(long id, bool asTracking = false, CancellationToken ct = default)
    {
        IQueryable<Item> query = Set
            .Include(i => i.Category)
            .Include(i => i.Detail)
            .Include(i => i.Images)
            .Include(i => i.ItemTags).ThenInclude(it => it.Tag)
            .AsSingleQuery();

        if (!asTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
        => await Set.IgnoreQueryFilters().AnyAsync(i => i.Sku == sku, ct);

    public async Task<bool> ExistsBySlugAsync(string slug, long? exceptItemId = null, CancellationToken ct = default)
        => await Set.IgnoreQueryFilters().AnyAsync(i => i.Slug == slug && i.Id != exceptItemId, ct);
}
