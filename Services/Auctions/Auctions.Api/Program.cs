using System.Reflection;
using Auctions.Api.ExceptionHandling;
using Auctions.Api.Grpc;
using Auctions.Bll.Caching;
using Auctions.Bll.Mapping;
using Auctions.Bll.Services;
using Auctions.Bll.Validation;
using Auctions.Dal.Interfaces;
using Auctions.Dal.Uow;
using Microsoft.OpenApi.Models;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Рядок підключення береремо з конфігурації: appsettings.json або змінна середовища
// ConnectionStrings__AuctionsDb (саме її підставляє .NET Aspire через WithReference).
var connectionString = builder.Configuration.GetConnectionString("AuctionsDb")
    ?? throw new InvalidOperationException("Connection string 'AuctionsDb' is not configured.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuctionHouse - Auctions API",
        Version = "v1",
        Description = "Проєкт №1: аукціони, учасники торгів і ставки (ADO.NET + Dapper)."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks();

builder.Services.AddScoped<IUnitOfWork>(sp =>
    new UnitOfWork(connectionString, sp.GetRequiredService<ILogger<UnitOfWork>>()));
builder.Services.AddScoped<IAuctionRepository>(sp => sp.GetRequiredService<IUnitOfWork>().Auctions);
builder.Services.AddScoped<IBidderRepository>(sp => sp.GetRequiredService<IUnitOfWork>().Bidders);
builder.Services.AddScoped<IBidRepository>(sp => sp.GetRequiredService<IUnitOfWork>().Bids);

builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AuctionProfile).Assembly));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAuctionValidator, AuctionValidator>();
builder.Services.AddScoped<IBidderService, BidderService>();

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
builder.Services.AddAuctionsCaching();

// gRPC для внутрішніх викликів (Aggregator); логування кожного виклику — GrpcLoggingInterceptor із ServiceDefaults.
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();
app.MapGrpcService<AuctionsGrpcService>();

app.Run();
