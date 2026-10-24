using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public sealed class CategoryRepository(CatalogDbContext context) : GenericRepository<Category>(context), ICategoryRepository
{
    // Explicit Loading: спочатку лише категорія (1-й запит), лоти довантажуються окремо (2-й запит).
    public async Task<Category?> GetWithItemsAsync(long id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return null;

        await Context.Entry(category).Collection(c => c.Items).LoadAsync(ct);
        return category;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken ct = default)
        => await Set.AnyAsync(c => c.Id == id, ct);

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => await Set.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug, ct);

    public async Task<bool> HasDependentsAsync(long id, CancellationToken ct = default)
        => await Set.AnyAsync(c => c.ParentCategoryId == id, ct)
           || await Context.Items.AnyAsync(i => i.CategoryId == id, ct);
}
