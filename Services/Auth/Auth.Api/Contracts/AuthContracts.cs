using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[a-zA-Z0-9._-]+$")] string UserName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    [StringLength(100)] string? DisplayName);

public sealed record LoginRequest([Required] string UserNameOrEmail, [Required] string Password);

// Refresh token береться з HttpOnly cookie; тіло — запасний варіант для клієнтів без cookie (Postman, мобільні).
public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record VerifyEmailRequest([Required] string UserId, [Required] string Token);

public sealed record EmailRequest([Required, EmailAddress] string Email);

public sealed record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

// Access token — у тілі відповіді; refresh token — лише в HttpOnly cookie (JavaScript його не прочитає -> XSS не вкраде).
public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAt, DateTime RefreshTokenExpiresAt, UserDto User);

public sealed record RegisterResponse(string UserId, string UserName, string Email, bool EmailConfirmationRequired);

public sealed record UserDto(string Id, string UserName, string? Email, bool EmailConfirmed, string? DisplayName,
    string? PhoneNumber, DateTime CreatedAt, bool IsLockedOut, IReadOnlyCollection<string> Roles);

public sealed record UpdateUserRequest([StringLength(100)] string? DisplayName, [Phone, StringLength(30)] string? PhoneNumber);

public sealed record AssignRoleRequest([Required] string Role);

public sealed record RefreshTokenDto(Guid Id, DateTime CreatedAt, DateTime ExpiresAt, string? CreatedByIp,
    DateTime? RevokedAt, string? RevokedReason, bool IsActive);
