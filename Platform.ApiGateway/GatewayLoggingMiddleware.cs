using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Platform.ApiGateway;

// Структурований запис про кожен проксійований запит: маршрут, кластер, статус, час, розмір відповіді, IP клієнта.
// Помилки проксі (таймаут, відмова з'єднання) логуються окремо з деталями з IForwarderErrorFeature.
public sealed class GatewayLoggingMiddleware(RequestDelegate next, ILogger<GatewayLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var originalBody = context.Response.Body;
        await using var countingBody = new CountingStream(originalBody);
        context.Response.Body = countingBody;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var elapsed = Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1);
        var proxyFeature = context.Features.Get<IReverseProxyFeature>();
        var route = proxyFeature?.Route.Config.RouteId ?? "(none)";
        var cluster = proxyFeature?.Cluster?.Config.ClusterId ?? "(none)";
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var errorFeature = context.Features.Get<IForwarderErrorFeature>();
        if (errorFeature is not null && errorFeature.Error != ForwarderError.None)
        {
            logger.LogWarning(
                "Proxy error {ProxyError} for {Method} {Path} -> route {Route} cluster {Cluster} destination {Destination}: {ExceptionType} {ExceptionMessage}",
                errorFeature.Error, context.Request.Method, context.Request.Path.Value, route, cluster,
                proxyFeature?.ProxiedDestination?.Model.Config.Address ?? "(none)",
                errorFeature.Exception?.GetType().Name, errorFeature.Exception?.Message);
        }

        logger.LogInformation(
            "{Method} {Path} -> route {Route} cluster {Cluster}: {StatusCode} in {Elapsed} ms, {ResponseSize} bytes, client {ClientIp}",
            context.Request.Method, context.Request.Path.Value, route, cluster,
            context.Response.StatusCode, elapsed, countingBody.BytesWritten, clientIp);
    }

    // Обгортка тіла відповіді, що лише рахує записані байти (Content-Length при chunked-відповідях відсутній).
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        // Внутрішній потік належить ASP.NET Core, його не закриваємо.
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
