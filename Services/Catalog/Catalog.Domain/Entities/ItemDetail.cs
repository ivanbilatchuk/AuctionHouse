using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class ItemDetail
{
    private ItemDetail() { }

    internal ItemDetail(ItemCondition condition, short? productionYear, string? countryOfOrigin,
        string? certificateNumber, string? provenance, string? description)
    {
        Update(condition, productionYear, countryOfOrigin, certificateNumber, provenance, description);
    }

    public long ItemId { get; private set; }
    public ItemCondition Condition { get; private set; }
    public short? ProductionYear { get; private set; }
    public string? CountryOfOrigin { get; private set; }
    public string? CertificateNumber { get; private set; }
    public string? Provenance { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    internal void Update(ItemCondition condition, short? productionYear, string? countryOfOrigin,
        string? certificateNumber, string? provenance, string? description)
    {
        if (productionYear is < 1000 or > 2100)
            throw new DomainException("Production year must be between 1000 and 2100.");

        Condition = condition;
        ProductionYear = productionYear;
        CountryOfOrigin = countryOfOrigin;
        CertificateNumber = string.IsNullOrWhiteSpace(certificateNumber) ? null : certificateNumber.Trim();
        Provenance = provenance;
        Description = description;
    }
}
