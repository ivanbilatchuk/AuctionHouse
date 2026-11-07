using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

// Наскрізний X-Correlation-Id: береться із запиту або генерується, повертається клієнту,
// записується назад у Request.Headers (його далі пересилають YARP і CorrelationIdHandler),
// додається до всіх записів логу (LogContext) і до поточного спану.
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ActivityTagName = "correlation.id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        var generated = string.IsNullOrWhiteSpace(correlationId);
        if (generated)
            correlationId = Guid.NewGuid().ToString();

        context.Request.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag(ActivityTagName, correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Перевірки здоров'я від Aspire йдуть кожні кілька секунд — їх не логуємо.
            if (Extensions.IsHealthPath(context.Request.Path))
            {
                await next(context);
                return;
            }

            if (generated)
                logger.LogDebug("No {HeaderName} header in request, generated new CorrelationId {CorrelationId}", HeaderName, correlationId);

            logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path.Value);
            var startedAt = Stopwatch.GetTimestamp();

            await next(context);

            logger.LogInformation("Request finished with {StatusCode} in {Elapsed} ms",
                context.Response.StatusCode, Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1));
        }
    }
}
