using System.Net;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Security;

namespace Auth.Api.Controllers;

/// <summary>Аутентифікація: реєстрація, вхід, refresh-токени, підтвердження email, відновлення і зміна пароля.</summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IJwtTokenService jwt,
    RefreshTokenService refreshTokens,
    IEmailSender email,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string RefreshCookieName = "refresh_token";

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Реєстрація (роль User автоматично) і лист із токеном підтвердження email.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = request.UserName, Email = request.Email, DisplayName = request.DisplayName };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return IdentityProblem(result);

        await users.AddToRoleAsync(user, IdentitySeeder.UserRole);
        await SendVerificationEmailAsync(user, ct);

        logger.LogInformation("User {UserId} ({UserName}) registered, email confirmation sent", user.Id, user.UserName);
        return Created($"/users/{user.Id}", new RegisterResponse(user.Id, user.UserName!, user.Email!, EmailConfirmationRequired: true));
    }

    /// <summary>Вхід: access token у тілі, refresh token — у HttpOnly cookie. Без підтвердженого email — 403.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = request.UserNameOrEmail.Contains('@')
            ? await users.FindByEmailAsync(request.UserNameOrEmail)
            : await users.FindByNameAsync(request.UserNameOrEmail);
        if (user is null)
            return Problem(statusCode: 401, title: "Invalid credentials");

        // lockoutOnFailure: 5 невдалих спроб -> блокування на 15 хв (налаштування AddIdentity).
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return Problem(statusCode: 403, title: "Account is locked", detail: $"Too many failed attempts. Try again after {user.LockoutEnd:u}.");
        // SignIn.RequireConfirmedEmail: правильний пароль, але email не підтверджено -> IsNotAllowed.
        if (check.IsNotAllowed && !user.EmailConfirmed)
            return Problem(statusCode: 403, title: "Email is not confirmed",
                detail: "Confirm your email via POST /auth/verify-email or request a new link via POST /auth/resend-verification.");
        if (!check.Succeeded)
            return Problem(statusCode: 401, title: "Invalid credentials");

        logger.LogInformation("User {UserId} logged in", user.Id);
        return Ok(await IssueTokensAsync(user, await refreshTokens.IssueAsync(user, ClientIp, ct)));
    }

    /// <summary>Новий access token за refresh token (cookie або тіло) з ротацією refresh token.</summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest? request, CancellationToken ct)
    {
        var presented = PresentedRefreshToken(request);
        if (presented is null)
            return Problem(statusCode: 401, title: "Refresh token is missing");

        var rotation = await refreshTokens.RotateAsync(presented, ClientIp, ct);
        if (!rotation.Succeeded)
        {
            Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTime.UnixEpoch));
            return Problem(statusCode: 401, title: rotation.Error);
        }

        var user = rotation.User!;
        if (await users.IsLockedOutAsync(user) || !user.EmailConfirmed)
            return Problem(statusCode: 403, title: "Account is not allowed to sign in");

        return Ok(await IssueTokensAsync(user, rotation.Token!));
    }

    /// <summary>Відкликати refresh token (власний; адміністратор — будь-який).</summary>
    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeToken(RefreshTokenRequest? request, CancellationToken ct)
    {
        var presented = PresentedRefreshToken(request);
        if (presented is null)
            return Problem(statusCode: 400, title: "Refresh token is missing");

        var token = await refreshTokens.FindActiveAsync(presented, ct);
        if (token is null || (token.UserId != User.GetUserId() && !User.IsInRole(IdentitySeeder.AdminRole)))
            return Problem(statusCode: 404, title: "Active refresh token not found");

        await refreshTokens.RevokeAsync(token, ClientIp, "Revoked by user", ct);
        if (request?.RefreshToken is null)
            Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTime.UnixEpoch));

        logger.LogInformation("Refresh token {TokenId} of user {UserId} revoked by {CallerId}", token.Id, token.UserId, User.GetUserId());
        return NoContent();
    }

    /// <summary>Вихід: відкликання поточного refresh token і видалення cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var presented)
            && await refreshTokens.FindActiveAsync(presented, ct) is { } token
            && token.UserId == User.GetUserId())
        {
            await refreshTokens.RevokeAsync(token, ClientIp, "Logout", ct);
        }

        Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTime.UnixEpoch));
        logger.LogInformation("User {UserId} logged out", User.GetUserId());
        return NoContent();
    }

    /// <summary>Підтвердити email одноразовим токеном з листа.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
            return Problem(statusCode: 400, title: "Invalid or expired verification token");
        if (user.EmailConfirmed)
            return Problem(statusCode: 400, title: "Email is already confirmed");

        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            return Problem(statusCode: 400, title: "Invalid or expired verification token");

        // Новий security stamp робить усі раніше видані токени (листи) недійсними — токен одноразовий.
        await users.UpdateSecurityStampAsync(user);
        logger.LogInformation("Email of user {UserId} confirmed", user.Id);
        return NoContent();
    }

    /// <summary>Повторно надіслати лист підтвердження (відповідь однакова незалежно від існування email).</summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(EmailRequest request, CancellationToken ct)
    {
        if (await users.FindByEmailAsync(request.Email) is { EmailConfirmed: false } user)
            await SendVerificationEmailAsync(user, ct);

        return Accepted();
    }

    /// <summary>Запит на скидання пароля: лист з одноразовим токеном (відповідь однакова для будь-якого email).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        if (await users.FindByEmailAsync(request.Email) is { EmailConfirmed: true } user)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await email.SendAsync(user.Email!, "AuctionHouse: password reset",
                $"POST /auth/reset-password\n{{ \"email\": \"{user.Email}\", \"token\": \"{token}\", \"newPassword\": \"...\" }}\n" +
                "The token is single-use and expires in 2 hours.", ct);
            logger.LogInformation("Password reset requested for user {UserId}", user.Id);
        }

        return Accepted();
    }

    /// <summary>Скинути пароль за токеном; після цього всі refresh-токени користувача відкликаються.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return Problem(statusCode: 400, title: "Invalid or expired reset token");

        // ResetPasswordAsync оновлює security stamp — токен стає недійсним після першого використання.
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result, "Invalid or expired reset token");

        await users.SetLockoutEndDateAsync(user, null);
        var revoked = await refreshTokens.RevokeAllAsync(user.Id, ClientIp, "Password reset", ct);
        logger.LogInformation("Password of user {UserId} reset, {Revoked} refresh tokens revoked", user.Id, revoked);
        return NoContent();
    }

    /// <summary>Змінити пароль (потрібен поточний); інші сесії (refresh-токени) відкликаються.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(User.GetUserId()!);
        if (user is null)
            return Problem(statusCode: 404, title: "User not found");

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);

        var revoked = await refreshTokens.RevokeAllAsync(user.Id, ClientIp, "Password changed", ct);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTime.UnixEpoch));
        logger.LogInformation("Password of user {UserId} changed, {Revoked} refresh tokens revoked", user.Id, revoked);
        return NoContent();
    }

    /// <summary>Лише Development: останні листи mock email service (токени підтвердження і скидання пароля).</summary>
    [HttpGet("dev/outbox")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult DevOutbox([FromServices] IEmailSender sender, [FromQuery] string? email)
    {
        if (!environment.IsDevelopment() || sender is not DevelopmentEmailSender dev)
            return NotFound();

        return Ok(dev.Outbox.Where(m => email is null || string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).Reverse());
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, IssuedRefreshToken refresh)
    {
        var roles = await users.GetRolesAsync(user);
        var access = jwt.GenerateAccessToken(user, roles);

        Response.Cookies.Append(RefreshCookieName, refresh.Token, CookieOptions(refresh.ExpiresAt));
        Response.Headers.CacheControl = "no-store";

        return new AuthResponse(access.Token, "Bearer", access.ExpiresAt, refresh.ExpiresAt,
            UsersController.ToDto(user, roles, isLockedOut: false));
    }

    // HttpOnly — недоступний JavaScript; SameSite=Strict — не надсилається з чужих сайтів (CSRF); Path=/auth — лише на auth-endpoints.
    private CookieOptions CookieOptions(DateTime expires) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        Expires = expires
    };

    private string? PresentedRefreshToken(RefreshTokenRequest? request) =>
        !string.IsNullOrWhiteSpace(request?.RefreshToken)
            ? request.RefreshToken
            : Request.Cookies.TryGetValue(RefreshCookieName, out var cookie) && !string.IsNullOrWhiteSpace(cookie) ? cookie : null;

    private async Task SendVerificationEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await email.SendAsync(user.Email!, "AuctionHouse: confirm your email",
            $"POST /auth/verify-email\n{{ \"userId\": \"{user.Id}\", \"token\": \"{token}\" }}\n" +
            "The token is single-use and expires in 2 hours.", ct);
    }

    private IActionResult IdentityProblem(IdentityResult result, string title = "Request is invalid")
    {
        var errors = result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return ValidationProblem(new ValidationProblemDetails(errors) { Title = title, Status = (int)HttpStatusCode.BadRequest });
    }
}
