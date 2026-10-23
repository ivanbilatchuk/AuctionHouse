using Ardalis.Specification;
using Catalog.Bll.DTO;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

// Критерії вибірки (Where / Include / OrderBy / Skip / Take) окремо від репозиторію;
// репозиторій лише застосовує специфікацію через WithSpecification() і виконує один SQL-запит.
public sealed class ItemsFilterSpec : Specification<Item>
{
    public ItemsFilterSpec(ItemQuery q, int page, int pageSize)
    {
        Query.AsNoTracking();
        Query.Include(i => i.Category);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            Query.Where(i => i.Title.Contains(term) || i.Sku.Contains(term));
        }

        if (q.CategoryId is not null)
            Query.Where(i => i.CategoryId == q.CategoryId);

        if (Enum.TryParse<ItemStatus>(q.Status, ignoreCase: true, out var status))
            Query.Where(i => i.Status == status);

        if (q.MinEstimate is not null)
            Query.Where(i => i.EstimateLow >= q.MinEstimate);

        if (q.MaxEstimate is not null)
            Query.Where(i => i.EstimateHigh <= q.MaxEstimate);

        var descending = string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        switch (q.SortBy.ToLowerInvariant())
        {
            case "sku":
                if (descending) Query.OrderByDescending(i => i.Sku).ThenBy(i => i.Id);
                else Query.OrderBy(i => i.Sku).ThenBy(i => i.Id);
                break;
            case "estimate":
                if (descending) Query.OrderByDescending(i => i.EstimateLow).ThenBy(i => i.Id);
                else Query.OrderBy(i => i.EstimateLow).ThenBy(i => i.Id);
                break;
            case "createdat":
                if (descending) Query.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id);
                else Query.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id);
                break;
            default:
                if (descending) Query.OrderByDescending(i => i.Title).ThenBy(i => i.Id);
                else Query.OrderBy(i => i.Title).ThenBy(i => i.Id);
                break;
        }

        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}
