using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.ServiceDefaults.HealthChecks;

// Теги, за якими перевірки розподіляються між /health/live і /health/ready.
public static class HealthTags
{
    // Лише сам процес, без зовнішніх залежностей (liveness).
    public const string Live = "live";
    // Залежності, без яких сервіс не повинен отримувати трафік (readiness).
    public const string Ready = "ready";
}

// JSON-відповідь health endpoint з деталями кожної перевірки:
// { "status": "Degraded", "totalDurationMs": 12.3, "checks": [ { "name": "redis-cache", "status": "Degraded", ... } ] }
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                tags = entry.Value.Tags,
                // Лише тип і повідомлення винятку — без stack trace і рядків підключення.
                error = entry.Value.Exception is { } ex ? $"{ex.GetType().Name}: {ex.Message}" : null,
                data = entry.Value.Data.Count == 0 ? null : entry.Value.Data
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
