using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Category", "dbo", t =>
        {
            t.HasCheckConstraint("CK_Category_NotOwnParent", "[ParentCategoryId] IS NULL OR [ParentCategoryId] <> [CategoryId]");
            t.HasCheckConstraint("CK_Category_AttributeKind",
                "[AttributeKind] IN (N'generic', N'cards', N'stamps', N'vinyl', N'coins')");
        });

        builder.HasKey(c => c.Id).HasName("PK_Category");
        builder.Property(c => c.Id).HasColumnName("CategoryId").UseIdentityColumn();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(120);
        builder.Property(c => c.AttributeKind).IsRequired().HasMaxLength(20);
        builder.Property(c => c.SortOrder).IsRequired();
        builder.HasModificationAudit();
        builder.HasSoftDeleteAndRowVersion();

        builder.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("UQ_Category_Slug");
        builder.HasIndex(c => new { c.ParentCategoryId, c.SortOrder })
            .HasDatabaseName("IX_Category_ParentCategoryId")
            .IncludeProperties(c => new { c.Name, c.Slug })
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Category_Parent");

        builder.Navigation(c => c.Children).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Статичний сідінг довідника з фіксованими Id (HasData -> InsertData у міграції).
        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = 1L, ParentCategoryId = (long?)null, Name = "Trading Cards", Slug = "trading-cards", AttributeKind = "cards", SortOrder = 10, CreatedAt = seededAt, CreatedBy = "seed", IsDeleted = false },
            new { Id = 3L, ParentCategoryId = (long?)null, Name = "Stamps", Slug = "stamps", AttributeKind = "stamps", SortOrder = 20, CreatedAt = seededAt, CreatedBy = "seed", IsDeleted = false },
            new { Id = 4L, ParentCategoryId = (long?)null, Name = "Vinyl Records", Slug = "vinyl-records", AttributeKind = "vinyl", SortOrder = 30, CreatedAt = seededAt, CreatedBy = "seed", IsDeleted = false },
            new { Id = 5L, ParentCategoryId = (long?)null, Name = "Coins", Slug = "coins", AttributeKind = "coins", SortOrder = 40, CreatedAt = seededAt, CreatedBy = "seed", IsDeleted = false },
            new { Id = 2L, ParentCategoryId = (long?)1L, Name = "Pokemon Cards", Slug = "pokemon-cards", AttributeKind = "cards", SortOrder = 10, CreatedAt = seededAt, CreatedBy = "seed", IsDeleted = false });
    }
}
