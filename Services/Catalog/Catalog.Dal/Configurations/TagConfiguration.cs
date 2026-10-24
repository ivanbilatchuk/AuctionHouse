using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tag", "dbo");

        builder.HasKey(t => t.Id).HasName("PK_Tag");
        builder.Property(t => t.Id).HasColumnName("TagId").UseIdentityColumn();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(60);
        builder.HasCreationAudit();

        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName("UQ_Tag_Name");
        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("UQ_Tag_Slug");

        // Статичний сідінг довідника з фіксованими Id.
        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = 1L, Name = "First Edition", Slug = "first-edition", CreatedAt = seededAt, CreatedBy = "seed" },
            new { Id = 2L, Name = "Graded", Slug = "graded", CreatedAt = seededAt, CreatedBy = "seed" },
            new { Id = 3L, Name = "Rare", Slug = "rare", CreatedAt = seededAt, CreatedBy = "seed" },
            new { Id = 4L, Name = "Limited Edition", Slug = "limited-edition", CreatedAt = seededAt, CreatedBy = "seed" },
            new { Id = 5L, Name = "Certified", Slug = "certified", CreatedAt = seededAt, CreatedBy = "seed" });
    }
}
