using Platform.ApiGateway;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Security;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Лаба 9: Gateway — єдина точка входу. Валідує JWT ПЕРЕД проксіюванням (будь-який з трьох IdP: Auth Service,
// IdentityServer, Keycloak) і пересилає заголовок Authorization далі — мікросервіси валідують токен ще раз незалежно
// і самі перевіряють дозволи. Аудиторію Gateway не перевіряє: він не Resource Server, а токени адресовані різним сервісам.
builder.Services.AddAuthentication(PlatformAuthentication.Scheme)
    .AddPlatformSchemeSelector(builder.Configuration)
    .AddJwtBearer(PlatformAuthentication.AuthServiceScheme, options => options.ConfigureAuthServiceTokens(builder.Configuration))
    .AddJwtBearer(PlatformAuthentication.IdentityServerScheme, options =>
        options.ConfigureIdentityServerTokens(builder.Configuration, builder.Environment, audience: null))
    .AddKeycloakJwtBearer(
        serviceName: PlatformAuthentication.KeycloakServiceName,
        realm: PlatformAuthentication.KeycloakRealm,
        authenticationScheme: PlatformAuthentication.KeycloakScheme,
        configureOptions: options => options.ConfigureKeycloakTokens(builder.Environment, audience: null));

// AuthorizationPolicy маршрутів (appsettings.json): "default" — потрібен валідний токен (/api/**, /users/**),
// "anonymous" — без токена (/auth/**: login, register, refresh-token...; захищені /auth-endpoints перевіряє сам Auth Service).
builder.Services.AddAuthorization();

// Маршрути й кластери — у секції ReverseProxy (appsettings.json); адреси http://<ресурс> розв'язує service discovery Aspire.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(context =>
    {
        // X-User-Id з claim "sub" перевіреного токена (варіант А методички). Вхідний заголовок клієнта завжди
        // видаляється — підробити ідентичність, передавши X-User-Id самостійно, неможливо.
        context.AddRequestHeaderRemove(UserIdHeader.Name);
        context.AddRequestTransform(transform =>
        {
            if (transform.HttpContext.User.GetUserId() is { } userId)
                transform.ProxyRequest.Headers.TryAddWithoutValidation(UserIdHeader.Name, userId);
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

// CorrelationId — до проксі: заголовок записується в запит і YARP пересилає його далі.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GatewayLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "AuctionHouse API Gateway",
    routes = new[]
    {
        "/auth/*", "/users/*",
        "/api/auctions", "/api/bidders", "/api/items", "/api/categories", "/api/tags",
        "/api/expert-opinions", "/api/discussions", "/api/payments", "/api/aggregator/auctions/{id}/full"
    }
}));
app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();

internal static class UserIdHeader
{
    public const string Name = "X-User-Id";
}
