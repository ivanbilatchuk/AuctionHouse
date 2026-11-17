using Platform.IdentityServer.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Platform.IdentityServer.Data;

// ASP.NET Identity: AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, ... (третій контекст поряд із
// ConfigurationDbContext і PersistedGrantDbContext Duende; усі три — в окремій базі IdentityServerDb).
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options);
