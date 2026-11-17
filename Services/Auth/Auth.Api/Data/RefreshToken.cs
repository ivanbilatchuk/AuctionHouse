namespace Auth.Api.Data;

// Refresh token (власна таблиця RefreshTokens). У БД зберігається лише SHA-256 хеш — витік бази не дає робочих токенів.
// Ротація: при /auth/refresh-token старий токен відкликається (RevokedAt, ReplacedByTokenHash) і створюється новий.
// Повторне використання вже відкликаного токена = ознака крадіжки -> відкликаються всі активні токени користувача.
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedByIp { get; set; }

    public string? RevokedReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTime now) => !IsRevoked && !IsExpired(now);
}
