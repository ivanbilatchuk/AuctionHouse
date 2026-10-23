using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

public sealed class TagBySlugSpec : Specification<Tag>
{
    public TagBySlugSpec(string slug)
    {
        Query.AsNoTracking();
        Query.Where(t => t.Slug == slug);
    }
}
