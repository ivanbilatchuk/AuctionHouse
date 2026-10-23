using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTO;

public sealed class CreateItemDto
{
    [Range(1, long.MaxValue)]
    public long CategoryId { get; init; }

    public Guid SellerExternalId { get; init; }

    [Required, StringLength(50)]
    public string Sku { get; init; } = "";

    [Required, StringLength(200)]
    public string Title { get; init; } = "";

    [Required, StringLength(220)]
    public string Slug { get; init; } = "";

    public decimal EstimateLow { get; init; }

    public decimal EstimateHigh { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "USD";

    public CreateItemDetailDto? Detail { get; init; }

    public List<string> ImageUrls { get; init; } = [];

    public List<long> TagIds { get; init; } = [];
}

public sealed class CreateItemDetailDto
{
    [Required]
    public string Condition { get; init; } = "";

    public short? ProductionYear { get; init; }

    [StringLength(100)]
    public string? CountryOfOrigin { get; init; }

    [StringLength(50)]
    public string? CertificateNumber { get; init; }

    [StringLength(2000)]
    public string? Provenance { get; init; }

    public string? Description { get; init; }
}
