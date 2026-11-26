using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults.Security;

// Лаба 9: мікросервіси приймають JWT трьох Identity Provider-ів і валідують їх НЕЗАЛЕЖНО, без звернення до IdP
// на кожен запит (підпис перевіряється ключем: симетричним — Auth Service, з JWKS discovery document — IdentityServer і Keycloak).
//
//   "Platform"        policy scheme за замовчуванням: за claim "iss" (без валідації) обирає одну з трьох схем нижче;
//   "AuthService"     Частина 1: власний Auth Service (ASP.NET Identity), HS256, issuer/audience/секрет з конфігурації;
//   "IdentityServer"  Частина 2: Duende IdentityServer, Authority + Audience = ApiResource ("auctions_api");
//   "Keycloak"        Частина 3: Keycloak через Aspire (AddKeycloakJwtBearer), realm AuctionHouse, Audience = Resource Server.
public static class PlatformAuthentication
{
    public const string Scheme = "Platform";
    public const string AuthServiceScheme = "AuthService";
    public const string IdentityServerScheme = "IdentityServer";
    public const string KeycloakScheme = "Keycloak";

    // Ім'я ресурсу Keycloak в AppHost і realm, який створює Pulumi (infra/keycloak).
    public const string KeycloakServiceName = "keycloak";
    public const string KeycloakRealm = "AuctionHouse";

    public const string AuthServiceSection = "Authentication:AuthService";
    public const string IdentityServerSection = "Authentication:IdentityServer";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    // Policy scheme: дивиться, ХТО видав токен, і передає валідацію відповідній схемі.
    public static AuthenticationBuilder AddPlatformSchemeSelector(this AuthenticationBuilder builder, IConfiguration configuration)
    {
        var authServiceIssuer = configuration[$"{AuthServiceSection}:Issuer"] ?? AuthServiceDefaults.DefaultIssuer;

        return builder.AddPolicyScheme(Scheme, "AuthService | IdentityServer | Keycloak", options =>
        {
            options.ForwardDefaultSelector = context => SelectScheme(context, authServiceIssuer);
        });
    }

    private static string SelectScheme(HttpContext context, string authServiceIssuer)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return IdentityServerScheme;

        var token = header["Bearer ".Length..].Trim();
        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(token))
            return IdentityServerScheme;

        var issuer = handler.ReadJsonWebToken(token).Issuer;
        if (string.Equals(issuer, authServiceIssuer, StringComparison.Ordinal))
            return AuthServiceScheme;
        if (issuer.Contains("/realms/", StringComparison.Ordinal))
            return KeycloakScheme;
        return IdentityServerScheme;
    }

    // Частина 1: токени Auth Service. audience = null -> аудиторію не перевіряємо (Gateway, Aggregator).
    public static void ConfigureAuthServiceTokens(this JwtBearerOptions options, IConfiguration configuration)
    {
        var settings = AuthServiceDefaults.Read(configuration);

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = ClockSkew,
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
    }

    // Частина 2: токени Duende IdentityServer. Ключі підпису — з /.well-known/openid-configuration (jwks_uri).
    public static void ConfigureIdentityServerTokens(this JwtBearerOptions options, IConfiguration configuration,
        IHostEnvironment environment, string? audience)
    {
        options.Authority = configuration[$"{IdentityServerSection}:Authority"] ?? "http://localhost:5700";
        options.Audience = audience;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateAudience = audience is not null;
        options.TokenValidationParameters.ValidTypes = ["at+jwt"];
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.ClockSkew = ClockSkew;
    }

    // Частина 3: токени Keycloak (у т. ч. RPT з authorization.permissions). Authority знаходить AddKeycloakJwtBearer
    // через service discovery Aspire; у розробці Keycloak працює по HTTP.
    public static void ConfigureKeycloakTokens(this JwtBearerOptions options, IHostEnvironment environment, string? audience)
    {
        options.Audience = audience;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateAudience = audience is not null;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.ClockSkew = ClockSkew;
    }
}

// Параметри токенів Auth Service: Issuer / Audience — у appsettings, ключ підпису — секретний параметр AppHost
// (jwt-signing-key), який Aspire передає Auth Service і всім мікросервісам (Authentication__AuthService__SigningKey).
public sealed record AuthServiceDefaults(string Issuer, string Audience, string SigningKey)
{
    public const string DefaultIssuer = "auctionhouse-auth";
    public const string DefaultAudience = "auctionhouse-api";

    public static AuthServiceDefaults Read(IConfiguration configuration)
    {
        var section = configuration.GetSection(PlatformAuthentication.AuthServiceSection);
        var key = section["SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException(
                $"{PlatformAuthentication.AuthServiceSection}:SigningKey must be configured (at least 32 bytes for HS256).");

        return new AuthServiceDefaults(section["Issuer"] ?? DefaultIssuer, section["Audience"] ?? DefaultAudience, key);
    }
}
