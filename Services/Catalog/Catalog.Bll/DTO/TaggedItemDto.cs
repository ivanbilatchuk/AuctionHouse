namespace Catalog.Bll.DTO;

public sealed class TaggedItemDto
{
    public long ItemId { get; init; }
    public string Sku { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string TagSlug { get; init; } = "";
    public DateTime AddedAt { get; init; }
}
