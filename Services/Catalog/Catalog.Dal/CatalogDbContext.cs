using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Catalog.Dal;

public sealed class CatalogDbContext : DbContext
{
    public const string AuditUser = "catalog-api";

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemDetail> ItemDetails => Set<ItemDetail>();
    public DbSet<ItemImage> ItemImages => Set<ItemImage>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ItemTag> ItemTags => Set<ItemTag>();

    // Усі IEntityTypeConfiguration<T> із цієї збірки підключаються централізовано.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Deleted && entry.Metadata.FindProperty("IsDeleted") is not null)
            {
                entry.State = EntityState.Modified;
                entry.Property("IsDeleted").CurrentValue = true;
            }

            if (entry.State == EntityState.Modified)
                SetIfExists(entry, now);
        }
    }

    private static void SetIfExists(EntityEntry entry, DateTime now)
    {
        if (entry.Metadata.FindProperty("UpdatedAt") is not null)
            entry.Property("UpdatedAt").CurrentValue = now;
        if (entry.Metadata.FindProperty("UpdatedBy") is not null)
            entry.Property("UpdatedBy").CurrentValue = AuditUser;
    }
}
