using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Data;

// Користувач платформи (AspNetUsers). Id — GUID-рядок: саме його мікросервіси бачать у claim "sub"
// і зберігають як ExternalUserId / SellerExternalId (посилання на Identity без FK, лаба 1).
public sealed class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
