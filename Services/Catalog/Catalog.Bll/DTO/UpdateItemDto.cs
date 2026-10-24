using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTO;

public sealed class UpdateItemDto
{
    [Range(1, long.MaxValue)]
    public long CategoryId { get; init; }

    [Required, StringLength(200)]
    public string Title { get; init; } = "";

    [Required, StringLength(220)]
    public string Slug { get; init; } = "";

    public decimal EstimateLow { get; init; }

    public decimal EstimateHigh { get; init; }
}
