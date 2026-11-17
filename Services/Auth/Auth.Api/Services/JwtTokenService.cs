using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Services;

public interface IJwtTokenService
{
    // Access token: 15 хв, HS256; claims sub (userId), unique_name (username), email, role (кожна роль окремо), jti.
    AccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    // Refresh token: 64 випадкові байти (криптостійкий ГПВЧ); у БД — лише його SHA-256 хеш.
    string GenerateRefreshToken();

    // Перевірка підпису, issuer, audience і терміну дії (той самий набір правил, що й у мікросервісах).
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

    string Hash(string refreshToken);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed class JwtTokenService(IOptions<JwtSettings> options, TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_settings.SigningKey));

    public AccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.Add(_settings.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("email_verified", user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
        };
        if (user.Email is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expires);
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = SigningKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = "role"
        });

        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    public string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
