using System.Reflection;
using Microsoft.OpenApi.Models;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Лаба 9: JWT від трьох Identity Provider-ів, валідація локально (без звернення до IdP на кожен запит).
// Policy scheme "Platform" за claim iss обирає: Auth Service (HS256) | IdentityServer (Authority, aud payments_api) |
// Keycloak (realm AuctionHouse, aud payments-api).
builder.Services.AddAuthentication(PlatformAuthentication.Scheme)
    .AddPlatformSchemeSelector(builder.Configuration)
    .AddJwtBearer(PlatformAuthentication.AuthServiceScheme, options => options.ConfigureAuthServiceTokens(builder.Configuration))
    .AddJwtBearer(PlatformAuthentication.IdentityServerScheme, options =>
        options.ConfigureIdentityServerTokens(builder.Configuration, builder.Environment, audience: "payments_api"))
    .AddKeycloakJwtBearer(
        serviceName: PlatformAuthentication.KeycloakServiceName,
        realm: PlatformAuthentication.KeycloakRealm,
        authenticationScheme: PlatformAuthentication.KeycloakScheme,
        configureOptions: options => options.ConfigureKeycloakTokens(builder.Environment, audience: "payments-api"));

// Permission-based авторизація: політика на кожен дозвіл, [RequirePermission] на кожному endpoint.
builder.Services.AddPermissionAuthorization();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuctionHouse - Payments API",
        Version = "v1",
        Description = "Лаба 9: демо-сервіс для перевірки доступу (payments:read / process / refund — лише admin)."
    });
    options.AddPlatformSecurity(builder.Configuration, Permissions.Payments.All);
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(options => options.UsePlatformOAuth(Permissions.Payments.All));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();

await app.RunAsync();
