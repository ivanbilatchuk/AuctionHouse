using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Item", "dbo", t =>
        {
            t.HasCheckConstraint("CK_Item_Estimate", "[EstimateLow] > 0 AND [EstimateHigh] >= [EstimateLow]");
            t.HasCheckConstraint("CK_Item_Status",
                "[Status] IN (N'Draft', N'Listed', N'InAuction', N'Sold', N'Withdrawn')");
        });

        builder.HasKey(i => i.Id).HasName("PK_Item");
        builder.Property(i => i.Id).HasColumnName("ItemId").UseIdentityColumn();

        builder.Property(i => i.SellerExternalId).IsRequired();
        builder.Property(i => i.Sku).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Title).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Slug).IsRequired().HasMaxLength(220);
        builder.Property(i => i.EstimateLow).HasColumnType("decimal(18,2)");
        builder.Property(i => i.EstimateHigh).HasColumnType("decimal(18,2)");
        builder.Property(i => i.Currency).IsRequired().HasColumnType("char(3)").HasMaxLength(3).IsFixedLength().IsUnicode(false);
        builder.Property(i => i.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.HasModificationAudit();
        builder.HasSoftDeleteAndRowVersion();

        builder.HasIndex(i => i.Sku).IsUnique().HasDatabaseName("UQ_Item_Sku");
        builder.HasIndex(i => i.Slug).IsUnique().HasDatabaseName("UQ_Item_Slug");
        builder.HasIndex(i => new { i.CategoryId, i.Status })
            .HasDatabaseName("IX_Item_CategoryId_Status")
            .IncludeProperties(i => new { i.Title, i.Slug, i.EstimateLow, i.EstimateHigh })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(i => i.Title).HasDatabaseName("IX_Item_Title").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(i => i.SellerExternalId).HasDatabaseName("IX_Item_SellerExternalId").HasFilter("[IsDeleted] = 0");

        // N:1 Item -> Category
        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Item_Category");

        // 1:1 Item -> ItemDetail (PK залежної таблиці є одночасно FK)
        builder.HasOne(i => i.Detail)
            .WithOne()
            .HasForeignKey<ItemDetail>(d => d.ItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ItemDetail_Item");

        // 1:N Item -> ItemImage
        builder.HasMany(i => i.Images)
            .WithOne()
            .HasForeignKey(im => im.ItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ItemImage_Item");

        builder.Navigation(i => i.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
