using System.Reflection;
using Auth.Api.Data;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.HealthChecks;
using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Лаба 9, Частина 1: Auth Service на ASP.NET Identity. IdentityDb — окрема база на окремому SQL Server (identity-sql)
// в AppHost: дані користувачів ізольовано від бізнес-баз.
var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Токени підтвердження email і скидання пароля: термін дії 2 год (одноразовість — через security stamp).
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

// JWT: AddIdentity ставить схемою за замовчуванням cookie Identity — для API замінюємо її на JwtBearer.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(PlatformAuthentication.AuthServiceSection));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    // Issuer, Audience, SigningKey (HS256), ClockSkew 30 с — ті самі параметри, що й у мікросервісах.
    .AddJwtBearer(options => options.ConfigureAuthServiceTokens(builder.Configuration));
builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddSingleton<IEmailSender, DevelopmentEmailSender>();

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, healthQuery: "SELECT 1;", name: "identity-db",
        tags: ["database", "sql", HealthTags.Ready], timeout: TimeSpan.FromSeconds(5));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuctionHouse - Auth Service",
        Version = "v1",
        Description = "Лаба 9, Частина 1: ASP.NET Identity + JWT (access 15 хв, refresh 7 днів з ротацією)."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token з POST /auth/login"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

// Міграції IdentityDb, ролі User / Admin, адміністратор для розробки.
await IdentitySeeder.SeedAsync(app.Services, app.Configuration, app.Environment);

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();

await app.RunAsync();
