using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Platform.ServiceDefaults.Security;

// Лаба 9: Swagger UI мікросервісів з двома способами авторизації:
//   "Bearer" — вставити access token будь-якого з трьох IdP (Auth Service, IdentityServer, Keycloak RPT);
//   "oauth2" — Authorization Code + PKCE через IdentityServer, public client "swagger" без секрету (Частина 2).
public static class SwaggerSecurityExtensions
{
    public const string SwaggerClientId = "swagger";

    public static void AddPlatformSecurity(this SwaggerGenOptions options, IConfiguration configuration, params string[] permissions)
    {
        var authority = (configuration[$"{PlatformAuthentication.IdentityServerSection}:Authority"] ?? "http://localhost:5700").TrimEnd('/');

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token: Auth Service (POST /auth/login), IdentityServer або Keycloak (RPT)"
        });

        var scopes = new Dictionary<string, string> { ["openid"] = "OpenID", ["profile"] = "Profile" };
        foreach (var permission in permissions)
            scopes[permission] = permission;

        options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Duende IdentityServer: Authorization Code + PKCE (client \"swagger\")",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{authority}/connect/authorize"),
                    TokenUrl = new Uri($"{authority}/connect/token"),
                    Scopes = scopes
                }
            }
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [Reference("Bearer")] = [],
            [Reference("oauth2")] = [.. scopes.Keys]
        });
    }

    public static void UsePlatformOAuth(this SwaggerUIOptions options, params string[] permissions)
    {
        options.OAuthClientId(SwaggerClientId);
        options.OAuthAppName("AuctionHouse Swagger");
        options.OAuthUsePkce();
        options.OAuthScopes(["openid", "profile", .. permissions]);
    }

    private static OpenApiSecurityScheme Reference(string id) =>
        new() { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = id } };
}
