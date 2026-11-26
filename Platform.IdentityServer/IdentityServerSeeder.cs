using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer.Data;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer;

// Під час старту: міграції трьох контекстів (PersistedGrant, Configuration, Application),
// синхронізація Config.cs -> ConfigurationDbContext (код — джерело правди, БД оновлюється при кожному старті),
// тестові користувачі (лише Development).
public static class IdentityServerSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentityServerSeeder));

        await provider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        var configurationDb = provider.GetRequiredService<ConfigurationDbContext>();
        await configurationDb.Database.MigrateAsync();
        await SyncConfigurationAsync(configurationDb);
        logger.LogInformation("IdentityServer configuration synchronized: {Clients} clients, {ApiResources} API resources, {ApiScopes} API scopes",
            Config.Clients.Count(), Config.ApiResources.Count(), Config.ApiScopes.Count());

        if (environment.IsDevelopment())
            await SeedUsersAsync(provider, configuration["Seed:UserPassword"] ?? "Pass123$", logger);
    }

    private static async Task SyncConfigurationAsync(ConfigurationDbContext db)
    {
        foreach (var client in Config.Clients)
        {
            var existing = await db.Clients.Where(c => c.ClientId == client.ClientId).ToListAsync();
            db.Clients.RemoveRange(existing);
            db.Clients.Add(client.ToEntity());
        }

        foreach (var resource in Config.IdentityResources)
        {
            db.IdentityResources.RemoveRange(await db.IdentityResources.Where(r => r.Name == resource.Name).ToListAsync());
            db.IdentityResources.Add(resource.ToEntity());
        }

        foreach (var scope in Config.ApiScopes)
        {
            db.ApiScopes.RemoveRange(await db.ApiScopes.Where(s => s.Name == scope.Name).ToListAsync());
            db.ApiScopes.Add(scope.ToEntity());
        }

        foreach (var resource in Config.ApiResources)
        {
            db.ApiResources.RemoveRange(await db.ApiResources.Where(r => r.Name == resource.Name).ToListAsync());
            db.ApiResources.Add(resource.ToEntity());
        }

        await db.SaveChangesAsync();
    }

    // alice — адміністратор, bob — звичайний користувач. Scopes у токені визначає клієнт і згода користувача,
    // а не роль: ролі в access token IdentityServer не потрапляють (scope-based авторизація).
    private static async Task SeedUsersAsync(IServiceProvider provider, string password, ILogger logger)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "User" })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (name, displayName, email, role) in new[]
                 {
                     ("alice", "Alice Smith", "alice@auctionhouse.local", "Admin"),
                     ("bob", "Bob Smith", "bob@auctionhouse.local", "User")
                 })
        {
            if (await users.FindByNameAsync(name) is not null)
                continue;

            var user = new ApplicationUser { UserName = name, Email = email, EmailConfirmed = true, DisplayName = displayName };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Seeding user {User} failed: {Errors}", name, string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }

            await users.AddToRoleAsync(user, role);
            await users.AddClaimsAsync(user,
            [
                new Claim(JwtClaimTypes.Name, displayName),
                new Claim(JwtClaimTypes.Email, email)
            ]);
            logger.LogInformation("Development user {User} ({Role}) created", name, role);
        }
    }
}
