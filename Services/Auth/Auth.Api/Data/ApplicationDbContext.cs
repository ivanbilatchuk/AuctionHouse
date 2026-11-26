using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

// IdentityDb: стандартні таблиці ASP.NET Identity (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims,
// AspNetUserTokens, ...) + RefreshTokens. Окрема база через Aspire (IdentityDb на сервері identity-sql).
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(100);
            user.Property(u => u.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("RefreshTokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.TokenHash).IsRequired().HasMaxLength(64).IsUnicode(false);
            token.Property(t => t.ReplacedByTokenHash).HasMaxLength(64).IsUnicode(false);
            token.Property(t => t.CreatedByIp).HasMaxLength(45);
            token.Property(t => t.RevokedByIp).HasMaxLength(45);
            token.Property(t => t.RevokedReason).HasMaxLength(200);
            token.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");
            token.Property(t => t.ExpiresAt).HasColumnType("datetime2(3)");
            token.Property(t => t.RevokedAt).HasColumnType("datetime2(3)");

            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => new { t.UserId, t.ExpiresAt });

            token.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
