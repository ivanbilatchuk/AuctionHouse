using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.ExceptionHandling;

// Централізований мапінг винятків у ProblemDetails; кожен виняток логується тут один раз.
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException or FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "Request cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request {Method} {Path} failed with {StatusCode}",
                httpContext.Request.Method, httpContext.Request.Path, status);
        else
            logger.LogWarning("Request {Method} {Path} failed with {StatusCode}: {ExceptionType} {Message}",
                httpContext.Request.Method, httpContext.Request.Path, status, exception.GetType().Name, exception.Message);

        ProblemDetails problem = exception switch
        {
            ValidationException ve => new ValidationProblemDetails(ve.Errors.ToDictionary(e => e.Key, e => e.Value)),
            FluentValidation.ValidationException fve => new ValidationProblemDetails(
                fve.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            _ => new ProblemDetails()
        };

        problem.Status = status;
        problem.Title = title;
        problem.Type = $"https://httpstatuses.io/{status}";
        problem.Instance = httpContext.Request.Path;
        problem.Detail = status >= StatusCodes.Status500InternalServerError
            ? (environment.IsDevelopment() ? exception.Message : "An unexpected error occurred. Use traceId to find the details in the logs.")
            : exception.Message;

        if (environment.IsDevelopment())
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
