using Duende.IdentityServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer;
using Platform.IdentityServer.Data;
using Platform.IdentityServer.Models;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Лаба 9, Частина 2: Duende IdentityServer (OAuth 2.0 / OpenID Connect).
// IdentityServerDb — ОКРЕМА база (сервер identity-sql в AppHost): ізоляція даних безпеки від бізнес-мікросервісів.
var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityServerDb' is not configured.");
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddRazorPages();

// ASP.NET Identity — сховище користувачів (AspNetUsers, ...).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

if (builder.Environment.IsDevelopment())
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.SameSite = SameSiteMode.Lax);

builder.Services
    .AddIdentityServer(options =>
    {
        // Публічна адреса (http://localhost:5700) — однаковий iss незалежно від того, як до сервера звернулися
        // (браузер чи Aggregator через service discovery). Мікросервіси перевіряють саме його.
        options.IssuerUri = builder.Configuration["IdentityServer:IssuerUri"];
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;
        options.EmitStaticAudienceClaim = false;

        // У розробці IdentityServer працює по HTTP: cookie з SameSite=None без Secure браузер відкидає, і вхід
        // через Authorization Code (Swagger UI, Postman) не завершився б. Lax достатньо для top-level редіректів OAuth.
        if (builder.Environment.IsDevelopment())
            options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
    })
    // Clients, ClientSecrets, ApiResources, ApiScopes, IdentityResources.
    .AddConfigurationStore(options =>
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)))
    // PersistedGrants (refresh tokens, authorization codes, consent), DeviceCodes, Keys (ключі підпису).
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        options.EnableTokenCleanup = true;
        options.TokenCleanupInterval = 3600;
    })
    // Користувачі для Authorization Code Flow — з ASP.NET Identity.
    .AddAspNetIdentity<ApplicationUser>()
    .AddLicenseSummary();

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, healthQuery: "SELECT 1;", name: "identityserver-db",
        tags: ["database", "sql", HealthTags.Ready], timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

// Міграції PersistedGrantDbContext, ConfigurationDbContext, ApplicationDbContext; синхронізація Config.cs; тестові користувачі.
await IdentityServerSeeder.SeedAsync(app.Services, app.Configuration, app.Environment);

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();

app.MapDefaultEndpoints();
// Quickstart UI (Login, Logout, Consent, Grants, Diagnostics, Device) — Razor Pages.
app.MapRazorPages().RequireAuthorization();

await app.RunAsync();
