using Auctions.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Auctions.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var problem = exception switch
        {
            NotFoundException ex => Create(StatusCodes.Status404NotFound, "Resource not found",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5", ex.Message),
            BusinessConflictException ex => Create(StatusCodes.Status409Conflict, "Business rule conflict",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10", ex.Message),
            ValidationException ex => CreateValidation(ex),
            _ => Create(StatusCodes.Status500InternalServerError, "Internal server error",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1", "An unexpected error occurred.")
        };

        problem.Instance = httpContext.Request.Path;
        httpContext.Response.StatusCode = problem.Status!.Value;

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning("Request failed with {Status}: {Message}. TraceId: {TraceId}",
                problem.Status, exception.Message, httpContext.TraceIdentifier);
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails Create(int status, string title, string type, string detail) => new()
    {
        Status = status,
        Title = title,
        Type = type,
        Detail = detail
    };

    private static ProblemDetails CreateValidation(ValidationException ex)
    {
        var problem = Create(StatusCodes.Status400BadRequest, "Validation failed",
            "https://tools.ietf.org/html/rfc9110#section-15.5.1", ex.Message);

        if (ex.Errors.Count > 0)
        {
            problem.Extensions["errors"] = ex.Errors;
        }

        return problem;
    }
}
