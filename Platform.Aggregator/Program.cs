using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi.Models;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість і X-Correlation-Id для HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services.AddOptions<AggregatorOptions>().Bind(builder.Configuration.GetSection(AggregatorOptions.SectionName));
var aggregatorOptions = builder.Configuration.GetSection(AggregatorOptions.SectionName).Get<AggregatorOptions>() ?? new AggregatorOptions();

// Типізовані клієнти; "https+http://" — спершу https-ендпоінт ресурсу, інакше http.
builder.Services.AddHttpClient<AuctionsClient>(client => client.BaseAddress = new("https+http://auctions-api"));
builder.Services.AddHttpClient<CatalogClient>(client => client.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<CommunityClient>(client => client.BaseAddress = new("https+http://community-api"));

// Стандартний resilience-хендлер із ServiceDefaults, але з короткими таймаутами й однією повторною спробою:
// недоступний сервіс має швидко перетворитися на попередження, а не тримати запит ~30 с.
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(resilience =>
{
    resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(aggregatorOptions.AttemptTimeoutSeconds);
    resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(aggregatorOptions.TotalTimeoutSeconds);
    resilience.Retry.MaxRetryAttempts = 1;
    resilience.Retry.Delay = TimeSpan.FromMilliseconds(200);
    resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(10, aggregatorOptions.AttemptTimeoutSeconds * 2));
});

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

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
