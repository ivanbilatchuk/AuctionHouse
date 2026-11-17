namespace Auth.Api.Services;

// Секція Authentication:AuthService. Issuer / Audience / SigningKey — ті самі, якими мікросервіси валідують токени
// (Platform.ServiceDefaults/Security/PlatformAuthentication.cs); SigningKey — секретний параметр AppHost jwt-signing-key.
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "auctionhouse-auth";

    public string Audience { get; set; } = "auctionhouse-api";

    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
