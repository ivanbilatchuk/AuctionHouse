using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults.Security;

// Лаба 9 (Частина 3.5): permission-based авторизація. Мікросервіс САМ, локально, вирішує «пропустити чи відхилити»
// на рівні конкретного endpoint — без звернення до Identity Provider на кожен запит.
//
// [RequirePermission("auctions:update")] = [Authorize(Policy = "auctions:update")] з PermissionRequirement.
// Немає дозволу -> 403 Forbidden (не 500); немає/невалідний токен -> 401.

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) : base(policy: permission) => Permission = permission;

    public string Permission { get; }
}

// Дозволи з токена будь-якого з трьох Identity Provider-ів платформи:
//   Keycloak (RPT)      — claim "authorization": { "permissions": [ { "rsname": "auctions-api", "scopes": [ ... ] } ] };
//   Duende IdentityServer — claim "scope": ApiScopes названо так само, як дозволи (scope-based політики, Частина 2);
//   Auth Service        — claim "role" (User / Admin) -> дозволи за матрицею RolePermissions (Частина 1).
public static class PermissionClaims
{
    public const string AuthorizationClaim = "authorization";
    public const string ScopeClaim = "scope";
    public const string RoleClaim = "role";

    public static HashSet<string> Extract(ClaimsPrincipal user)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in user.FindAll(AuthorizationClaim))
            AddKeycloakPermissions(claim.Value, permissions);

        // Duende видає scope масивом (окремий claim на кожен), Keycloak — рядком через пробіл.
        foreach (var claim in user.FindAll(ScopeClaim))
            foreach (var scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                permissions.Add(scope);

        foreach (var claim in user.FindAll(RoleClaim))
            permissions.UnionWith(RolePermissions.ForRole(claim.Value));

        return permissions;
    }

    private static void AddKeycloakPermissions(string json, HashSet<string> permissions)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("permissions", out var list) || list.ValueKind != JsonValueKind.Array)
                return;

            foreach (var permission in list.EnumerateArray())
            {
                if (!permission.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var scope in scopes.EnumerateArray())
                    if (scope.GetString() is { Length: > 0 } value)
                        permissions.Add(value);
            }
        }
        catch (JsonException)
        {
            // Пошкоджений claim не дає жодного дозволу.
        }
    }
}

public sealed class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (PermissionClaims.Extract(context.User).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning("Access denied: {Subject} (client {ClientId}, issuer {Issuer}) lacks permission {Permission}",
                context.User.GetUserId() ?? "-", context.User.FindFirst("client_id")?.Value ?? context.User.FindFirst("azp")?.Value ?? "-",
                context.User.FindFirst("iss")?.Value ?? "-", requirement.Permission);
        }

        return Task.CompletedTask;
    }
}

public static class PermissionAuthorizationExtensions
{
    // Політика на кожен дозвіл (таблиці 3.3.1–3.3.3 методички) + обробник.
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));
        });
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}

public static class ClaimsPrincipalExtensions
{
    // Ідентифікатор користувача ("sub") — однаковий claim в усіх трьох IdP. У токені Client Credentials його немає.
    public static string? GetUserId(this ClaimsPrincipal user) => user.FindFirst("sub")?.Value;

    public static Guid? GetUserGuid(this ClaimsPrincipal user) =>
        Guid.TryParse(user.GetUserId(), out var id) ? id : null;

    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        PermissionClaims.Extract(user).Contains(permission);
}
