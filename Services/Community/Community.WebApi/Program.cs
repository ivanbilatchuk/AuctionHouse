using Community.Application;
using Community.Infrastructure;
using Community.WebApi;
using Community.WebApi.Grpc;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddWebApi();

// Лаба 6: дворівневий кеш. L1 — IMemoryCache з лімітом записів (кожен запис має Size = 1),
// L2 — Redis через IDistributedCache (рядок підключення "redis" від Aspire або з appsettings.json).
builder.Services.AddMemoryCache(options => options.SizeLimit = 1024);
// Короткі таймаути: якщо Redis недоступний, сервіс швидко переходить на БД, а не чекає по 5 с.
builder.AddRedisDistributedCache("redis", configureOptions: redis =>
{
    redis.ConnectTimeout = 2000;
    redis.AsyncTimeout = 1000;
    redis.SyncTimeout = 1000;
});

// gRPC для внутрішніх викликів (Aggregator); логування кожного виклику — GrpcLoggingInterceptor із ServiceDefaults.
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());

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

await app.RunAsync();
