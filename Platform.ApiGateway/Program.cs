using Platform.ApiGateway;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (Platform.ServiceDefaults).
builder.AddServiceDefaults();

// Маршрути й кластери — у секції ReverseProxy (appsettings.json); адреси http://<ресурс> розв'язує service discovery Aspire.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// CorrelationId — до проксі: заголовок записується в запит і YARP пересилає його далі.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GatewayLoggingMiddleware>();

app.MapGet("/", () => Results.Ok(new
{
    service = "AuctionHouse API Gateway",
    routes = new[]
    {
        "/api/auctions", "/api/bidders", "/api/items", "/api/categories", "/api/tags",
        "/api/expert-opinions", "/api/discussions", "/api/aggregator/auctions/{id}/full"
    }
}));
app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
