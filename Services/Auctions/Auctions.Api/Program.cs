using System.Reflection;
using Auctions.Api.ExceptionHandling;
using Auctions.Bll.Mapping;
using Auctions.Bll.Services;
using Auctions.Bll.Validation;
using Auctions.Dal.Interfaces;
using Auctions.Dal.Uow;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IBidderService, BidderService>();
builder.Services.AddScoped<IBidService, BidService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
