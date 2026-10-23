using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

// Друга специфікація з іншими критеріями: застосовується тим самим WithSpecification() без змін у репозиторії.
public sealed class ItemsBySellerSpec : Specification<Item>
{
    public ItemsBySellerSpec(Guid sellerExternalId, int page, int pageSize)
    {
        Query.AsNoTracking();
        Query.Include(i => i.Category);
        Query.Where(i => i.SellerExternalId == sellerExternalId);
        Query.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}
