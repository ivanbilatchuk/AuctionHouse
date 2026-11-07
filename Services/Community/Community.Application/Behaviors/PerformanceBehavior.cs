using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Community.Application.Behaviors;

// Попередження про повільні обробники (довше за поріг).
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            logger.LogWarning("Slow request {RequestName} took {ElapsedMs} ms (threshold {ThresholdMs} ms) {@Request}",
                typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, SlowRequestThresholdMs, request);

        return response;
    }
}
