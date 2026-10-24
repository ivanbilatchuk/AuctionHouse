using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class ItemImageConfiguration : IEntityTypeConfiguration<ItemImage>
{
    public void Configure(EntityTypeBuilder<ItemImage> builder)
    {
        builder.ToTable("ItemImage", "dbo", t => t.HasCheckConstraint("CK_ItemImage_Url", "[Url] LIKE N'http%'"));

        builder.HasKey(im => im.Id).HasName("PK_ItemImage");
        builder.Property(im => im.Id).HasColumnName("ImageId").UseIdentityColumn();

        builder.Property(im => im.Url).IsRequired().HasMaxLength(500);
        builder.Property(im => im.IsPrimary).IsRequired();
        builder.Property(im => im.SortOrder).IsRequired();
        builder.HasCreationAudit();

        builder.HasIndex(im => new { im.ItemId, im.SortOrder })
            .HasDatabaseName("IX_ItemImage_ItemId_SortOrder")
            .IncludeProperties(im => new { im.Url, im.IsPrimary });
        builder.HasIndex(im => im.ItemId)
            .IsUnique()
            .HasDatabaseName("UX_ItemImage_OnePrimaryPerItem")
            .HasFilter("[IsPrimary] = 1");
    }
}
