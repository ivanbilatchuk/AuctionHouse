using System.Reflection;
using Catalog.Api.ExceptionHandling;
using Catalog.Api.Middleware;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validation;
using Catalog.Dal;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Seeding;
using Catalog.Dal.Uow;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog: налаштування й sink-и в appsettings*.json, SQL-команди EF Core на рівні Information.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
        .Enrich.FromLogContext());

    var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
        ?? throw new InvalidOperationException("Connection string 'CatalogDb' is not configured.");

    builder.Services.AddDbContext<CatalogDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        options.ConfigureWarnings(w => w
            .Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)
            // Виняток логує лише GlobalExceptionHandler, тому власні Error-записи EF Core про той самий збій знижено до Debug.
            .Log((CoreEventId.SaveChangesFailed, LogLevel.Debug),
                 (CoreEventId.QueryIterationFailed, LogLevel.Debug),
                 (RelationalEventId.CommandError, LogLevel.Debug)));
        if (builder.Environment.IsDevelopment())
            options.EnableSensitiveDataLogging();
    });
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

    builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ItemProfile).Assembly));
    builder.Services.AddValidatorsFromAssembly(typeof(CreateItemDtoValidator).Assembly);
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IItemService, ItemService>();
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<ITagService, TagService>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "AuctionHouse - Catalog API",
            Version = "v1",
            Description = "Проєкт №2: каталог лотів, категорії та теги (EF Core Code First)."
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
    });

    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>();

    var app = builder.Build();

    // Міграції, потім ідемпотентний програмний сідінг (вимикається Seeding:Enabled=false, у Production не виконується).
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync();

        if (!app.Environment.IsProduction() && app.Configuration.GetValue("Seeding:Enabled", true))
            await DbInitializer.SeedAsync(db);
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnostics, http) =>
            diagnostics.Set("UserAgent", http.Request.Headers.UserAgent.ToString()));
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    app.MapHealthChecks("/health");
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Catalog API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
