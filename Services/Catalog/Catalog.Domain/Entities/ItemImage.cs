namespace Catalog.Domain.Entities;

public class ItemImage
{
    private ItemImage() { }

    internal ItemImage(string url, bool isPrimary, int sortOrder)
    {
        Url = url;
        IsPrimary = isPrimary;
        SortOrder = sortOrder;
    }

    public long Id { get; private set; }
    public long ItemId { get; private set; }
    public string Url { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;

    internal void UnmarkPrimary() => IsPrimary = false;
}
