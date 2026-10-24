using AutoMapper;
using Catalog.Bll.DTO;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public sealed class CategoryService(IUnitOfWork uow, IMapper mapper) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await uow.Categories.GetAllAsync(ct);
        return mapper.Map<List<CategoryDto>>(categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList());
    }

    public async Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        return mapper.Map<CategoryDto>(category);
    }

    public async Task<CategoryWithItemsDto> GetWithItemsAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetWithItemsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        return mapper.Map<CategoryWithItemsDto>(category);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default)
    {
        var slug = input.Slug.Trim().ToLowerInvariant();

        if (await uow.Categories.ExistsBySlugAsync(slug, ct))
            throw new ConflictException($"Category with slug '{slug}' already exists.");
        if (input.ParentCategoryId is { } parentId && !await uow.Categories.ExistsAsync(parentId, ct))
            throw new ValidationException(nameof(CreateCategoryDto.ParentCategoryId), $"Category {parentId} does not exist.");

        var category = new Category(input.Name, slug, input.AttributeKind, input.ParentCategoryId, input.SortOrder);

        await uow.Categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);

        return mapper.Map<CategoryDto>(category);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        if (await uow.Categories.HasDependentsAsync(id, ct))
            throw new ConflictException($"Category {id} has subcategories or items and cannot be deleted.");

        uow.Categories.Delete(category);
        await uow.SaveChangesAsync(ct);
    }
}
