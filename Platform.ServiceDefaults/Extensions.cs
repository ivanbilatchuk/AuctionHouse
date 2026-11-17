using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.HealthChecks;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

// Спільні налаштування всіх сервісів платформи: Serilog, OpenTelemetry, health checks,
// service discovery, стійкість HttpClient і прокидання X-Correlation-Id.
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    public const string LivenessEndpointPath = "/health/live";
    public const string ReadinessEndpointPath = "/health/ready";

    // Префікс ActivitySource-ів із бізнес-спанами (AuctionHouse.Auctions, AuctionHouse.Catalog, ...).
    public const string ActivitySourcePrefix = "AuctionHouse.";
    public const string MongoDbActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";
    public const string SamplingRatioKey = "OpenTelemetry:SamplingRatio";

    // Лаба 7: Meter-и бізнес-метрик (назва Meter = назва сервісу, класи в <Сервіс>.Api/Metrics/)
    // і Meter "Polly" — події resilience (resilience.polly.strategy.events: OnRetry, OnCircuitOpened, ...).
    public static readonly string[] BusinessMeters = ["Auctions.Api", "Catalog.Api", "Community.WebApi", "Platform.Aggregator"];
    public const string PollyMeter = "Polly";

    // Лаба 8: шина подій RabbitMQ (Platform.EventBus) — спани publish/process і метрики публікації/обробки/DLQ.
    // Контекст трасування передається через заголовки повідомлень, тож HTTP-запит -> publish -> process -> БД — один trace.
    public const string MessagingActivitySource = "Platform.Messaging";
    public const string EventBusMeter = "Platform.EventBus";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureSerilog();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
            http.AddHttpMessageHandler<CorrelationIdHandler>();
        });

        return builder;
    }

    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var isDevelopment = builder.Environment.IsDevelopment();
        var serviceName = builder.Environment.ApplicationName;

        // Консольний провайдер за замовчуванням дублював би вивід Serilog; OpenTelemetry-провайдер додається нижче
        // і отримує ті самі події завдяки writeToProviders: true.
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog((services, configuration) => configuration
            .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Diagnostics.HealthChecks", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.ServiceDiscovery", LogEventLevel.Information)
            // Необроблені винятки вже логує GlobalExceptionHandler кожного сервісу.
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter()),
            writeToProviders: true);

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var applicationName = builder.Environment.ApplicationName;
        // Aspire передає ім'я ресурсу (auctions-api, ...) через OTEL_SERVICE_NAME.
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? applicationName;

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                }))
            .WithMetrics(metrics => metrics
                // Власні лічильники кешу: AuctionHouse.Catalog.Cache, AuctionHouse.Auctions.Cache, ... (cache.hits / cache.misses).
                .AddMeter(ActivitySourcePrefix + "*")
                .AddMeter(BusinessMeters)
                .AddMeter(PollyMeter)
                .AddMeter(EventBusMeter)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(CreateSampler(builder))
                    .AddSource(applicationName)
                    .AddSource(ActivitySourcePrefix + "*")
                    .AddSource(MongoDbActivitySource)
                    .AddSource(MessagingActivitySource)
                    .AddAspNetCoreInstrumentation(options =>
                        // Регулярні перевірки здоров'я від Aspire не засмічують трейси.
                        options.Filter = context => !IsHealthPath(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    // Development — кожен трейс; інакше частка з конфігурації (за замовчуванням 10%), з повагою до рішення батьківського спану.
    private static Sampler CreateSampler(IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            return new AlwaysOnSampler();

        var ratio = Math.Clamp(builder.Configuration.GetValue(SamplingRatioKey, 0.1), 0.0, 1.0);
        return new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio));
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // "self" — єдина liveness-перевірка: процес живий і відповідає. Залежності (БД, Redis, gRPC)
        // реєструють сервіси з тегом "ready" у своїх <Сервіс>.Api/HealthChecks/.
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [HealthTags.Live]);

        return builder;
    }

    // Endpoints публікуються в усіх середовищах (не лише Development): їх використовують Aspire (WithHttpHealthCheck)
    // і Kubernetes (Лаб. 10). Відповідь не містить секретів — лише назви, статуси й повідомлення перевірок.
    //   /health/live  — liveness: лише "live" (без зовнішніх залежностей), збій => перезапуск процесу;
    //   /health/ready — readiness: перевірки з тегом "ready" (БД, Redis, gRPC-залежності), збій => без трафіку;
    //   /health       — усі перевірки з деталями (зручно для діагностики);
    //   /alive        — аліас liveness із шаблону Aspire.
    // Degraded (наприклад, недоступний Redis) відповідає 200: сервіс працює, хоч і без кешу.
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        var liveOptions = new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthTags.Live),
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
        };

        app.MapHealthChecks(LivenessEndpointPath, liveOptions);
        app.MapHealthChecks(AlivenessEndpointPath, liveOptions);

        app.MapHealthChecks(ReadinessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthTags.Ready),
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
        });

        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
        });

        return app;
    }

    internal static bool IsHealthPath(Microsoft.AspNetCore.Http.PathString path)
        => path.StartsWithSegments(HealthEndpointPath) || path.StartsWithSegments(AlivenessEndpointPath);
}
