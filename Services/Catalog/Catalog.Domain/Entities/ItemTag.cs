namespace Catalog.Domain.Entities;

// Явна проміжна сутність M:N (Item <-> Tag): складений ключ (ItemId, TagId) і власні поля AddedAt/AddedBy.
public class ItemTag
{
    private ItemTag() { }

    internal ItemTag(Item item, long tagId)
    {
        Item = item;
        TagId = tagId;
    }

    public long ItemId { get; private set; }
    public long TagId { get; private set; }
    public DateTime AddedAt { get; private set; }
    public string AddedBy { get; private set; } = null!;

    public Item Item { get; private set; } = null!;
    public Tag Tag { get; private set; } = null!;
}
