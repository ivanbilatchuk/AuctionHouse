using Auth.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Api.Services;

// Видача, ротація та відкликання refresh-токенів (таблиця RefreshTokens).
public sealed class RefreshTokenService(
    ApplicationDbContext db,
    IJwtTokenService jwt,
    IOptions<JwtSettings> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenService> logger)
{
    public async Task<IssuedRefreshToken> IssueAsync(ApplicationUser user, string? ip, CancellationToken ct)
    {
        var (token, entity) = Create(user.Id, ip);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return new IssuedRefreshToken(token, entity.ExpiresAt);
    }

    // Ротація: старий токен відкликається (ReplacedByTokenHash -> новий), новий створюється — в одному SaveChanges.
    public async Task<RotationResult> RotateAsync(string presentedToken, string? ip, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = jwt.Hash(presentedToken);
        var existing = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
            return RotationResult.Fail("Refresh token is invalid.");

        if (existing.IsRevoked)
        {
            // Хтось пред'явив уже відкликаний (ротований) токен — можлива крадіжка: відкликаємо всю «родину».
            var revoked = await RevokeAllAsync(existing.UserId, ip, "Reuse of a revoked refresh token detected", ct);
            logger.LogWarning("Reuse of revoked refresh token {TokenId} for user {UserId}: {Revoked} active tokens revoked",
                existing.Id, existing.UserId, revoked);
            return RotationResult.Fail("Refresh token has been revoked.");
        }

        if (existing.IsExpired(now))
            return RotationResult.Fail("Refresh token has expired.");

        var (token, replacement) = Create(existing.UserId, ip);
        existing.RevokedAt = now;
        existing.RevokedByIp = ip;
        existing.RevokedReason = "Replaced by a new token (rotation)";
        existing.ReplacedByTokenHash = replacement.TokenHash;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);

        return RotationResult.Ok(existing.User, new IssuedRefreshToken(token, replacement.ExpiresAt));
    }

    public async Task<RefreshToken?> FindActiveAsync(string presentedToken, CancellationToken ct)
    {
        var hash = jwt.Hash(presentedToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        return token is not null && token.IsActive(now) ? token : null;
    }

    public async Task RevokeAsync(RefreshToken token, string? ip, string reason, CancellationToken ct)
    {
        token.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        token.RevokedByIp = ip;
        token.RevokedReason = reason;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> RevokeAllAsync(string userId, string? ip, string reason, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ip;
            token.RevokedReason = reason;
        }

        await db.SaveChangesAsync(ct);
        return active.Count;
    }

    private (string Token, RefreshToken Entity) Create(string userId, string? ip)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = jwt.GenerateRefreshToken();
        return (token, new RefreshToken
        {
            UserId = userId,
            TokenHash = jwt.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(options.Value.RefreshTokenLifetime),
            CreatedByIp = ip
        });
    }
}

public sealed record IssuedRefreshToken(string Token, DateTime ExpiresAt);

public sealed record RotationResult(bool Succeeded, string? Error, ApplicationUser? User, IssuedRefreshToken? Token)
{
    public static RotationResult Ok(ApplicationUser user, IssuedRefreshToken token) => new(true, null, user, token);

    public static RotationResult Fail(string error) => new(false, error, null, null);
}
