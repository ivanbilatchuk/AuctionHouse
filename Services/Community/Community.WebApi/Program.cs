using Community.Application;
using Community.Infrastructure;
using Community.WebApi;
using Community.WebApi.Grpc;
using Community.WebApi.HealthChecks;
using Community.WebApi.IntegrationEvents;
using Community.WebApi.Metrics;
using Platform.EventBus;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.HealthChecks;
using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddWebApi(builder.Configuration);

// Лаба 7: health checks MongoDB (Unhealthy) і Redis (Degraded) з тегом "ready" — /health/ready.
builder.Services.AddCommunityHealthChecks(builder.Configuration);

// Лаба 7: бізнес-метрики (Meter "Community.WebApi"), Singleton.
builder.Services.AddSingleton<CommunityMetrics>();

// Лаба 8: RabbitMQ через Aspire.RabbitMQ.Client (рядок підключення "messaging"), health check брокера.
// Споживає ItemUpdated з catalog-events і оновлює item.title у висновках та обговореннях (черга community.catalog-sync).
// Consumer стартує після InitializeMongoAsync (hosted services запускаються в RunAsync) — індекс processedEvents уже є.
builder.AddEventBus();
builder.Services.AddHostedService<CatalogEventsConsumer>();

// Лаба 6: дворівневий кеш. L1 — IMemoryCache з лімітом записів (кожен запис має Size = 1),
// L2 — Redis через IDistributedCache (рядок підключення "redis" від Aspire або з appsettings.json).
builder.Services.AddMemoryCache(options => options.SizeLimit = 1024);
// Короткі таймаути: якщо Redis недоступний, сервіс швидко переходить на БД, а не чекає по 5 с.
// Власний health check Redis зі статусом Degraded (HealthChecks/) замінює вбудований Unhealthy-check Aspire.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true, redis =>
{
    redis.ConnectTimeout = 2000;
    redis.AsyncTimeout = 1000;
    redis.SyncTimeout = 1000;
});

// gRPC для внутрішніх викликів (Aggregator); логування кожного виклику — GrpcLoggingInterceptor із ServiceDefaults.
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());
// Стандартний gRPC Health Checking Protocol (grpc.health.v1.Health): статус "" обчислюється з тих самих
// ASP.NET Core health checks, що й /health/ready (тег "ready"). Його викликає health check Aggregator-а.
builder.Services.AddGrpcHealthChecks(options =>
{
    options.Services.Clear();
    options.Services.Map("", context => context.Tags.Contains(HealthTags.Ready));
});

// Лаба 9: аутентифікація JWT трьох Identity Provider-ів; кожен токен валідується ЛОКАЛЬНО (підпис, iss, aud, термін),
// без звернення до IdP на кожен запит. Policy scheme "Platform" за claim iss обирає схему:
//   AuthService    — Частина 1, власний Auth Service (ASP.NET Identity, HS256, ролі User / Admin);
//   IdentityServer — Частина 2, Duende IdentityServer (Authority + Audience = ApiResource "community_api");
//   Keycloak       — Частина 3, Keycloak через Aspire (realm AuctionHouse, Audience = Resource Server "community-api").
builder.Services.AddAuthentication(PlatformAuthentication.Scheme)
    .AddPlatformSchemeSelector(builder.Configuration)
    .AddJwtBearer(PlatformAuthentication.AuthServiceScheme, options => options.ConfigureAuthServiceTokens(builder.Configuration))
    .AddJwtBearer(PlatformAuthentication.IdentityServerScheme, options =>
        options.ConfigureIdentityServerTokens(builder.Configuration, builder.Environment, audience: "community_api"))
    .AddKeycloakJwtBearer(
        serviceName: PlatformAuthentication.KeycloakServiceName,
        realm: PlatformAuthentication.KeycloakRealm,
        authenticationScheme: PlatformAuthentication.KeycloakScheme,
        configureOptions: options => options.ConfigureKeycloakTokens(builder.Environment, audience: "community-api"));

// Permission-based авторизація: політика на кожен дозвіл; [RequirePermission(...)] стоїть на КОЖНОМУ endpoint контролерів
// і на gRPC-сервісі. Дозволи читаються з токена: authorization.permissions (Keycloak), scope (IdentityServer), role (Auth Service).
builder.Services.AddPermissionAuthorization();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

// Індекси MongoDB і ідемпотентний сідінг (IDataSeeder) під час старту.
await app.Services.InitializeMongoAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(options => options.UsePlatformOAuth(Permissions.Community.All));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();
app.MapGrpcService<CommunityGrpcService>();
app.MapGrpcHealthChecksService();

await app.RunAsync();
