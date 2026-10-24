using Catalog.Bll.DTO;

namespace Catalog.Bll.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);

    Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<CategoryWithItemsDto> GetWithItemsAsync(long id, CancellationToken ct = default);

    Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
