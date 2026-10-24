using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Tag
{
    private readonly List<ItemTag> _itemTags = [];

    private Tag() { }

    public Tag(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tag name is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new DomainException("Tag slug is required.");

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public long Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;

    public IReadOnlyCollection<ItemTag> ItemTags => _itemTags;
}
