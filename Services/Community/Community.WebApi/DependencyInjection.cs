using System.Reflection;
using Community.WebApi.ExceptionHandling;
using Community.WebApi.Mapping;
using Microsoft.OpenApi.Models;

namespace Community.WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services)
    {
        // Валідацію виконує ValidationBehavior (FluentValidation), тому неявний [Required] для non-nullable рядків вимкнено.
        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AuctionHouse - Community API",
                Version = "v1",
                Description = "Проєкт №3: експертні висновки та обговорення (Clean Architecture + CQRS + MongoDB)."
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
        });

        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(RequestProfile).Assembly));

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
