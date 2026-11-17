using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.ServiceDefaults.Security;

namespace Auth.Api.Controllers;

/// <summary>
/// Керування користувачами і ролями. Admin-endpoints — [Authorize(Roles = "Admin")]; owner-endpoints звіряють
/// userId з токена ("sub") з {id}. Ролі в JWT дозволяють перевірку без звернення до БД; нова роль потрапляє в токен
/// після наступного /auth/refresh-token.
/// </summary>
[ApiController]
[Route("users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    ApplicationDbContext db,
    TimeProvider timeProvider,
    ILogger<UsersController> logger) : ControllerBase
{
    private const string Admin = IdentitySeeder.AdminRole;

    /// <summary>Список користувачів (Admin).</summary>
    [HttpGet]
    [Authorize(Roles = Admin)]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var list = await users.Users.OrderBy(u => u.UserName).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var result = new List<UserDto>(list.Count);
        foreach (var user in list)
            result.Add(ToDto(user, await users.GetRolesAsync(user), await users.IsLockedOutAsync(user)));

        Response.Headers["X-Total-Count"] = (await users.Users.CountAsync(ct)).ToString();
        return Ok(result);
    }

    /// <summary>Дані користувача (власник або Admin).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(string id)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(ToDto(user, await users.GetRolesAsync(user), await users.IsLockedOutAsync(user)));
    }

    /// <summary>Оновити профіль (власник або Admin).</summary>
    [HttpPut("{id}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        user.DisplayName = request.DisplayName;
        user.PhoneNumber = request.PhoneNumber;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(ToDto(user, await users.GetRolesAsync(user), await users.IsLockedOutAsync(user)));
    }

    /// <summary>Видалити користувача (Admin). Refresh-токени видаляються каскадно.</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == User.GetUserId())
            return Problem(statusCode: 400, title: "Administrators cannot delete their own account");

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        await users.DeleteAsync(user);
        logger.LogInformation("User {UserId} deleted by {AdminId}", id, User.GetUserId());
        return NoContent();
    }

    /// <summary>Історія refresh-токенів (власник або Admin). Самі токени не повертаються — лише метадані.</summary>
    [HttpGet("{id}/refresh-tokens")]
    [ProducesResponseType<IReadOnlyList<RefreshTokenDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshTokens(string id, CancellationToken ct)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokens = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(tokens.Select(t => new RefreshTokenDto(t.Id, t.CreatedAt, t.ExpiresAt, t.CreatedByIp, t.RevokedAt, t.RevokedReason, t.IsActive(now))));
    }

    /// <summary>Ролі користувача (будь-який автентифікований).</summary>
    [HttpGet("{id}/roles")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await users.GetRolesAsync(user));
    }

    /// <summary>Призначити роль (Admin).</summary>
    [HttpPost("{id}/roles")]
    [Authorize(Roles = Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignRole(string id, AssignRoleRequest request)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        if (!await roles.RoleExistsAsync(request.Role))
            return Problem(statusCode: 400, title: $"Role '{request.Role}' does not exist");

        if (!await users.IsInRoleAsync(user, request.Role))
            await users.AddToRoleAsync(user, request.Role);

        logger.LogInformation("Role {Role} assigned to {UserId} by {AdminId}", request.Role, id, User.GetUserId());
        return NoContent();
    }

    /// <summary>Видалити роль (Admin).</summary>
    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        if (id == User.GetUserId() && role == Admin)
            return Problem(statusCode: 400, title: "Administrators cannot remove their own Admin role");

        await users.RemoveFromRoleAsync(user, role);
        logger.LogInformation("Role {Role} removed from {UserId} by {AdminId}", role, id, User.GetUserId());
        return NoContent();
    }

    // Власник ресурсу (sub з токена = {id}) або адміністратор.
    private bool IsOwnerOrAdmin(string id) => User.GetUserId() == id || User.IsInRole(Admin);

    internal static UserDto ToDto(ApplicationUser user, IEnumerable<string> roles, bool isLockedOut) =>
        new(user.Id, user.UserName!, user.Email, user.EmailConfirmed, user.DisplayName, user.PhoneNumber, user.CreatedAt,
            isLockedOut, roles.ToArray());
}
