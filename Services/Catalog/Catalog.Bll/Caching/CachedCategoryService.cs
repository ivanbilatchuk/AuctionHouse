using Catalog.Bll.DTO;
using Catalog.Bll.Services;

namespace Catalog.Bll.Caching;

// Декоратор ICategoryService: увесь довідник категорій кешується одним ключем, інвалідація — після створення/видалення.
public sealed class CachedCategoryService(CategoryService inner, TwoLevelCache cache) : ICategoryService
{
    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync(CatalogCacheKeys.CategoriesAll, inner.GetAllAsync, CatalogCacheKeys.CategoriesSettings, ct);

    public Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct = default)
        => inner.GetByIdAsync(id, ct);

    public Task<CategoryWithItemsDto> GetWithItemsAsync(long id, CancellationToken ct = default)
        => inner.GetWithItemsAsync(id, ct);

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default)
    {
        var created = await inner.CreateAsync(input, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.CategoriesAll, $"category {created.Id} created", ct);
        return created;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await inner.DeleteAsync(id, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.CategoriesAll, $"category {id} deleted", ct);
    }
}
