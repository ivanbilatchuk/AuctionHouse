using System.Reflection;
using System.Text.Json.Serialization;
using Auctions.Grpc;
using Catalog.Grpc;
using Community.Grpc;
using Microsoft.OpenApi.Models;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Security;
using Platform.Aggregator.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Duende.AccessTokenManagement;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість і X-Correlation-Id для HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Лаба 9, Частина 2: Client Credentials Flow (сервіс-до-сервісу). Aggregator сам отримує access token у IdentityServer
// (client "aggregator_service", лише scopes *:read) і додає його до кожного HTTP- і gRPC-виклику сервісів.
// Токен кешується до закінчення терміну дії (Duende.AccessTokenManagement); user context у ньому немає — лише ідентичність сервісу.
var identityServerAuthority = (builder.Configuration["Authentication:IdentityServer:Authority"] ?? "http://localhost:5700").TrimEnd('/');
var serviceTokenClient = ServiceTokenWarmup.ClientName;
builder.Services.AddClientCredentialsTokenManagement()
    .AddClient(serviceTokenClient, client =>
    {
        client.TokenEndpoint = new Uri($"{identityServerAuthority}/connect/token");
        client.ClientId = ClientId.Parse(builder.Configuration["Authentication:IdentityServer:ClientId"] ?? "aggregator_service");
        client.ClientSecret = ClientSecret.Parse(builder.Configuration["Authentication:IdentityServer:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication:IdentityServer:ClientSecret is not configured."));
        client.Scope = Scope.Parse($"{Permissions.Auctions.Read} {Permissions.Catalog.Read} {Permissions.Community.Read}");
    });
builder.Services.AddHostedService<ServiceTokenWarmup>();

// Вхідні запити (від Gateway): токен будь-якого з трьох IdP; аудиторію не перевіряємо — Aggregator не є Resource Server-ом.
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
builder.Services.AddAuthorization();

builder.Services.AddOptions<AggregatorOptions>().Bind(builder.Configuration.GetSection(AggregatorOptions.SectionName));

// Лаба 7: політики resilience за критичністю сервісу (обґрунтування — README.md, ServiceResilienceProfile):
//   Auctions, Catalog — критичні (без аукціону/лота картки немає): більше повторів, чутливіший circuit breaker;
//   Community         — некритичний (висновки експертів): швидко здатися й повернути часткову картку.
var critical = ServiceResilienceProfile.Critical;
var nonCritical = ServiceResilienceProfile.NonCritical;

// Типізовані клієнти; "https+http://" — спершу https-ендпоінт ресурсу, інакше http.
// Стандартний обробник із ServiceDefaults прибирається (RemoveAllResilienceHandlers) і замінюється власним — без нашарування.
builder.Services.AddHttpClient<AuctionsClient>(client => client.BaseAddress = new("https+http://auctions-api"))
    .AddServiceResilience(critical)
    .AddClientCredentialsTokenHandler(serviceTokenClient);
builder.Services.AddHttpClient<CatalogClient>(client => client.BaseAddress = new("https+http://catalog-api"))
    .AddServiceResilience(critical)
    .AddClientCredentialsTokenHandler(serviceTokenClient);
builder.Services.AddHttpClient<CommunityClient>(client => client.BaseAddress = new("https+http://community-api"))
    .AddServiceResilience(nonCritical)
    .AddClientCredentialsTokenHandler(serviceTokenClient);

// gRPC-клієнти: логічні адреси "http://_grpc.<ресурс>" — ендпоінт "grpc" (HTTP/2 без TLS) кожного сервісу,
// який розв'язує service discovery. Resilience, service discovery і CorrelationIdHandler застосовуються
// з ConfigureHttpClientDefaults (ServiceDefaults), тож X-Correlation-Id потрапляє в gRPC metadata.
// Два рівні без дублювання повторів: HTTP-обробник (таймаути, circuit breaker; retry вимкнено, бо gRPC — це POST)
// і retry-політика gRPC-каналу за статусами Unavailable / ResourceExhausted.
builder.Services.AddGrpcClient<AuctionsService.AuctionsServiceClient>(o => o.Address = new Uri("http://_grpc.auctions-api"))
    .AddServiceResilience(critical)
    .AddGrpcRetryPolicy(critical)
    .AddClientCredentialsTokenHandler(serviceTokenClient);
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("http://_grpc.catalog-api"))
    .AddServiceResilience(critical)
    .AddGrpcRetryPolicy(critical)
    .AddClientCredentialsTokenHandler(serviceTokenClient);
builder.Services.AddGrpcClient<CommunityService.CommunityServiceClient>(o => o.Address = new Uri("http://_grpc.community-api"))
    .AddServiceResilience(nonCritical)
    .AddGrpcRetryPolicy(nonCritical)
    .AddClientCredentialsTokenHandler(serviceTokenClient);

builder.Services.AddScoped<GrpcCardDataSource>();
builder.Services.AddScoped<HttpCardDataSource>();

// Кеш готової картки: Redis (IDistributedCache), рядок підключення "redis" від Aspire або з appsettings.json.
// Короткі таймаути: якщо Redis недоступний, сервіс швидко переходить на БД, а не чекає по 5 с.
// Власний health check Redis зі статусом Degraded (нижче) замінює вбудований Unhealthy-check Aspire.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true, redis =>
{
    redis.ConnectTimeout = 2000;
    redis.AsyncTimeout = 1000;
    redis.SyncTimeout = 1000;
});
builder.Services.AddSingleton<CacheMetrics>();
builder.Services.AddSingleton<AuctionCardCache>();

// Лаба 7: readiness Aggregator-а — стан gRPC-залежностей (gRPC Health Checking Protocol) і Redis.
// Недоступність критичного сервісу => Unhealthy; некритичного (Community) і кешу => Degraded:
// Aggregator і далі віддає картку, лише без висновків експертів / без кешу.
builder.Services.AddHealthChecks()
    .AddGrpcServiceHealthCheck("auctions-api", new Uri("http://_grpc.auctions-api"), HealthStatus.Unhealthy)
    .AddGrpcServiceHealthCheck("catalog-api", new Uri("http://_grpc.catalog-api"), HealthStatus.Unhealthy)
    .AddGrpcServiceHealthCheck("community-api", new Uri("http://_grpc.community-api"), HealthStatus.Degraded)
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' is not configured."),
        name: "redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(3));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuctionCardService>();

// camelCase (за замовчуванням Web) і без null-полів у відповідях.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuctionHouse - Aggregator",
        Version = "v1",
        Description = "Лаба 5: композитна картка аукціону з Auctions, Catalog і Community."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
