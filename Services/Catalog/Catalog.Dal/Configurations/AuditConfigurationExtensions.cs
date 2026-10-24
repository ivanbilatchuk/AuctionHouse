using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

internal static class AuditConfigurationExtensions
{
    public static void HasCreationAudit<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<DateTime>("CreatedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property<string>("CreatedBy").HasMaxLength(100).IsRequired().HasDefaultValueSql("SUSER_SNAME()");
    }

    public static void HasModificationAudit<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.HasCreationAudit();
        builder.Property<DateTime?>("UpdatedAt").HasColumnType("datetime2(3)");
        builder.Property<string?>("UpdatedBy").HasMaxLength(100);
    }

    public static void HasSoftDeleteAndRowVersion<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<bool>("IsDeleted").IsRequired();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
