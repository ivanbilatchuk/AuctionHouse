using Community.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ValidationException = Community.Application.Common.ValidationException;

namespace Community.WebApi.ExceptionHandling;

// Мапінг винятків у ProblemDetails, включно з винятками MongoDB.
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
                => (StatusCodes.Status409Conflict, "Duplicate key"),
            MongoConnectionException or TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Database unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request {Method} {Path} failed with {StatusCode}", httpContext.Request.Method, httpContext.Request.Path, status);
        else
            logger.LogWarning("Request {Method} {Path} failed with {StatusCode}: {Message}", httpContext.Request.Method, httpContext.Request.Path, status, exception.Message);

        ProblemDetails problem = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Type = $"https://httpstatuses.io/{status}";
        problem.Instance = httpContext.Request.Path;
        problem.Detail = exception switch
        {
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } => "A document with the same unique key already exists.",
            MongoConnectionException or TimeoutException => "MongoDB is temporarily unavailable. Retry later.",
            _ when status >= StatusCodes.Status500InternalServerError && !environment.IsDevelopment() => "An unexpected error occurred.",
            _ => exception.Message
        };

        if (environment.IsDevelopment() && status >= StatusCodes.Status500InternalServerError)
            problem.Extensions["exception"] = exception.ToString();

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
