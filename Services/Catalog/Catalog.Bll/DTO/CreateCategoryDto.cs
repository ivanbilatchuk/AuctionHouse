using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTO;

public sealed class CreateCategoryDto
{
    public long? ParentCategoryId { get; init; }

    [Required, StringLength(100)]
    public string Name { get; init; } = "";

    [Required, StringLength(120)]
    public string Slug { get; init; } = "";

    [Required, StringLength(20)]
    public string AttributeKind { get; init; } = "generic";

    public int SortOrder { get; init; }
}
