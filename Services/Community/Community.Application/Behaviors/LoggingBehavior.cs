using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Community.Application.Behaviors;

// Структуроване логування кожного запиту MediatR та його відповіді.
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestName} in {ElapsedMs} ms with {ResponseType} {@Response}",
                requestName, stopwatch.ElapsedMilliseconds, typeof(TResponse).Name, response);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Request {RequestName} failed after {ElapsedMs} ms: {ExceptionType} {ExceptionMessage}",
                requestName, stopwatch.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
