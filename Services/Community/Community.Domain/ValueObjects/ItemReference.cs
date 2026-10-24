using Community.Domain.Common;
using Community.Domain.Exceptions;

namespace Community.Domain.ValueObjects;

// Посилання на лот із Catalog (без FK) + денормалізований знімок назви.
public sealed class ItemReference : ValueObject
{
    public ItemReference(long itemId, string title)
    {
        if (itemId <= 0) throw new DomainException("Item id must be positive.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Item title is required.");
        if (title.Length > 200) throw new DomainException("Item title must be at most 200 characters.");

        ItemId = itemId;
        Title = title.Trim();
    }

    public long ItemId { get; }

    public string Title { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ItemId;
        yield return Title;
    }
}
