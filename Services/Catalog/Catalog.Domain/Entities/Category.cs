using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Category
{
    public static readonly IReadOnlyList<string> AttributeKinds = ["generic", "cards", "stamps", "vinyl", "coins"];

    private readonly List<Category> _children = [];
    private readonly List<Item> _items = [];

    private Category() { }

    public Category(string name, string slug, string attributeKind = "generic", long? parentCategoryId = null, int sortOrder = 0)
    {
        Rename(name, slug);
        SetAttributeKind(attributeKind);
        ParentCategoryId = parentCategoryId;
        SortOrder = sortOrder;
    }

    public long Id { get; private set; }
    public long? ParentCategoryId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string AttributeKind { get; private set; } = "generic";
    public int SortOrder { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public Category? Parent { get; private set; }
    public IReadOnlyCollection<Category> Children => _children;
    public IReadOnlyCollection<Item> Items => _items;

    public void Rename(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Category name is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new DomainException("Category slug is required.");

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public void MoveTo(long? parentCategoryId)
    {
        if (parentCategoryId is not null && parentCategoryId == Id)
            throw new DomainException("Category cannot be its own parent.");

        ParentCategoryId = parentCategoryId;
    }

    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;

    private void SetAttributeKind(string attributeKind)
    {
        if (!AttributeKinds.Contains(attributeKind))
            throw new DomainException($"Attribute kind '{attributeKind}' is not supported.");

        AttributeKind = attributeKind;
    }
}
