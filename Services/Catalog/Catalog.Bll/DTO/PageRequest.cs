namespace Catalog.Bll.DTO;

public sealed class PageRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = ItemQuery.DefaultPageSize;
}
