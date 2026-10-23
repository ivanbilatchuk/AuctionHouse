namespace Catalog.Bll.DTO;

public sealed class TagDto
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
}
