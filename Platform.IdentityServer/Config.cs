using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Platform.ServiceDefaults.Security;

namespace Platform.IdentityServer;

// Лаба 9, Частина 2: конфігурація IdentityServer. Під час старту синхронізується в ConfigurationDbContext
// (таблиці Clients, ClientSecrets, ApiResources, ApiScopes, IdentityResources) — IdentityServerSeeder.
//
// ApiScope = дозвіл платформи (auctions:read, catalog:publish, ...): той самий рядок, що й політика
// [RequirePermission(...)] у мікросервісі, — тож scope-based авторизація не потребує окремої таблиці відповідності.
// ApiResource = мікросервіс (аудиторія токена, aud): auctions_api, catalog_api, community_api, payments_api.
public static class Config
{
    // Публічні адреси Swagger UI мікросервісів (http-профілі) — redirect URI і CORS для public client "swagger".
    private static readonly string[] SwaggerOrigins =
        ["http://localhost:5180", "http://localhost:5280", "http://localhost:5380", "http://localhost:5580"];

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
        Permissions.All.Select(permission => new ApiScope(permission, $"AuctionHouse permission {permission}"));

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("auctions_api", "Auctions API (Проєкт №1)") { Scopes = Permissions.Auctions.All },
        new ApiResource("catalog_api", "Catalog API (Проєкт №2)") { Scopes = Permissions.Catalog.All },
        new ApiResource("community_api", "Community API (Проєкт №3)") { Scopes = Permissions.Community.All },
        new ApiResource("payments_api", "Payments API (демо)") { Scopes = Permissions.Payments.All }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Postman — Authorization Code (+ PKCE, як і для всіх code-клієнтів Duende за замовчуванням), confidential.
        new Client
        {
            ClientId = "postman",
            ClientName = "Postman (Authorization Code)",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            ClientSecrets = { new Secret("postman_secret".Sha256()) },
            RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 900,
            AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                .. Permissions.All
            ]
        },

        // Aggregator — Client Credentials (сервіс-до-сервісу): лише читання, без user context (у токені немає sub).
        new Client
        {
            ClientId = "aggregator_service",
            ClientName = "Platform.Aggregator (Client Credentials)",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("aggregator_secret".Sha256()) },
            AccessTokenLifetime = 900,
            AllowedScopes = [Permissions.Auctions.Read, Permissions.Catalog.Read, Permissions.Community.Read]
        },

        // Swagger UI — public client: секрету немає, тому PKCE обов'язковий (запит без code_challenge відхиляється).
        new Client
        {
            ClientId = "swagger",
            ClientName = "Swagger UI (public, PKCE)",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = SwaggerOrigins.Select(origin => $"{origin}/swagger/oauth2-redirect.html").ToList(),
            AllowedCorsOrigins = SwaggerOrigins,
            AccessTokenLifetime = 900,
            AllowedScopes =
            [
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                .. Permissions.All
            ]
        }
    ];
}
