using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class ItemTagConfiguration : IEntityTypeConfiguration<ItemTag>
{
    public void Configure(EntityTypeBuilder<ItemTag> builder)
    {
        builder.ToTable("ItemTag", "dbo");

        // M:N через явну проміжну сутність: складений ключ + власні поля AddedAt/AddedBy.
        builder.HasKey(it => new { it.ItemId, it.TagId }).HasName("PK_ItemTag");
        builder.Property(it => it.AddedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(it => it.AddedBy).IsRequired().HasMaxLength(100).HasDefaultValueSql("SUSER_SNAME()");

        builder.HasOne(it => it.Item)
            .WithMany(i => i.ItemTags)
            .HasForeignKey(it => it.ItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ItemTag_Item");

        builder.HasOne(it => it.Tag)
            .WithMany(t => t.ItemTags)
            .HasForeignKey(it => it.TagId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ItemTag_Tag");

        builder.HasIndex(it => new { it.TagId, it.AddedAt })
            .HasDatabaseName("IX_ItemTag_TagId_AddedAt");
    }
}
