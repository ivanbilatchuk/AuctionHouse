using Community.Application;
using Community.Infrastructure;
using Community.WebApi;
using Community.WebApi.Grpc;
using Community.WebApi.HealthChecks;
using Community.WebApi.Metrics;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddWebApi();

// Лаба 7: health checks MongoDB (Unhealthy) і Redis (Degraded) з тегом "ready" — /health/ready.
builder.Services.AddCommunityHealthChecks(builder.Configuration);

// Лаба 7: бізнес-метрики (Meter "Community.WebApi"), Singleton.
builder.Services.AddSingleton<CommunityMetrics>();

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

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

// Індекси MongoDB і ідемпотентний сідінг (IDataSeeder) під час старту.
await app.Services.InitializeMongoAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();
app.MapGrpcService<CommunityGrpcService>();
app.MapGrpcHealthChecksService();

await app.RunAsync();
