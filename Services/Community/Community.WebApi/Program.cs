using Community.Application;
using Community.Infrastructure;
using Community.WebApi;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, стійкість HttpClient (Platform.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddWebApi();

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

await app.RunAsync();
