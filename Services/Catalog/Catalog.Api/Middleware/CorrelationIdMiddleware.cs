using System.Diagnostics;
using Serilog.Context;

namespace Catalog.Api.Middleware;

// Додає до кожного запису логу структуровані властивості CorrelationId, RequestPath і UserId (якщо є).
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        using (LogContext.PushProperty("UserId", context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null))
        {
            await next(context);
        }
    }
}
