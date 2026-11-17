using Microsoft.AspNetCore.Identity;

namespace Platform.IdentityServer.Models;

// Користувач IdentityServer (ASP.NET Identity, AspNetUsers у базі IdentityServerDb).
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
