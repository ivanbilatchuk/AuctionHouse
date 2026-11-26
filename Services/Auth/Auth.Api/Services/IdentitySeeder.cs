using Auth.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Services;

// Під час старту: міграції IdentityDb, ролі User і Admin, обліковий запис адміністратора (лише в Development —
// у production адміністратора призначає інший адмін через POST /users/{id}/roles).
public static class IdentitySeeder
{
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentitySeeder));

        await provider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { UserRole, AdminRole })
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Role {Role} created", role);
            }

        var adminEmail = configuration["Seed:AdminEmail"];
        var adminPassword = configuration["Seed:AdminPassword"];
        if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            return;

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new ApplicationUser { UserName = "admin", Email = adminEmail, EmailConfirmed = true, DisplayName = "Platform Admin" };
        var result = await users.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            logger.LogError("Admin seed failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRolesAsync(admin, [UserRole, AdminRole]);
        logger.LogInformation("Development admin {Email} created", adminEmail);
    }
}
