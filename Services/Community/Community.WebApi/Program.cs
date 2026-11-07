using Community.Application;
using Community.Infrastructure;
using Community.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddWebApi();

var app = builder.Build();

// Індекси MongoDB і ідемпотентний сідінг (IDataSeeder) під час старту.
await app.Services.InitializeMongoAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapControllers();

await app.RunAsync();
