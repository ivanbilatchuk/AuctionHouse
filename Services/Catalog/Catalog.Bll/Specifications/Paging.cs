using Catalog.Bll.DTO;

namespace Catalog.Bll.Specifications;

public static class Paging
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
        => (Math.Max(page, 1), Math.Clamp(pageSize, 1, ItemQuery.MaxPageSize));
}
