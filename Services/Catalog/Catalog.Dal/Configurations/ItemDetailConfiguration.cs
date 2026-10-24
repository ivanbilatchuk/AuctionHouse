using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class ItemDetailConfiguration : IEntityTypeConfiguration<ItemDetail>
{
    public void Configure(EntityTypeBuilder<ItemDetail> builder)
    {
        builder.ToTable("ItemDetail", "dbo", t =>
        {
            t.HasCheckConstraint("CK_ItemDetail_Condition",
                "[Condition] IN (N'Mint', N'NearMint', N'Excellent', N'Good', N'Fair', N'Poor')");
            t.HasCheckConstraint("CK_ItemDetail_Year", "[ProductionYear] IS NULL OR [ProductionYear] BETWEEN 1000 AND 2100");
        });

        builder.HasKey(d => d.ItemId).HasName("PK_ItemDetail");
        builder.Property(d => d.ItemId).ValueGeneratedNever();

        builder.Property(d => d.Condition).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.CountryOfOrigin).HasMaxLength(100);
        builder.Property(d => d.CertificateNumber).HasMaxLength(50);
        builder.Property(d => d.Provenance).HasMaxLength(2000);
        builder.Property(d => d.Description);
        builder.HasModificationAudit();

        builder.HasIndex(d => d.CertificateNumber)
            .IsUnique()
            .HasDatabaseName("UX_ItemDetail_CertificateNumber")
            .HasFilter("[CertificateNumber] IS NOT NULL");
    }
}
