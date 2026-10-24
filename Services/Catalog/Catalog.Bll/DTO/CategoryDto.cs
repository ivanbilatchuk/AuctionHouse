namespace Catalog.Bll.DTO;

public sealed class CategoryDto
{
    public long Id { get; init; }
    public long? ParentCategoryId { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string AttributeKind { get; init; } = "";
    public int SortOrder { get; init; }
}
