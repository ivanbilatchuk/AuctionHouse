using Microsoft.AspNetCore.Http;

namespace Platform.ServiceDefaults;

// Додає X-Correlation-Id поточного вхідного запиту до кожного вихідного запиту HttpClient.
public sealed class CorrelationIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(correlationId) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
