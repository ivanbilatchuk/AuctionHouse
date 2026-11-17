using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults;

// Серверний interceptor gRPC: метод, peer, статус і тривалість кожного unary-виклику як структуровані властивості.
// Очікувані помилки (RpcException) — Warning зі статусом, неочікувані винятки — Error (клієнт отримає Internal).
public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogDebug("gRPC call started {GrpcMethod} from {GrpcPeer}", context.Method, context.Peer);

        try
        {
            var response = await continuation(request, context);
            logger.LogInformation("gRPC call {GrpcMethod} from {GrpcPeer} finished with {GrpcStatus} in {Elapsed} ms",
                context.Method, context.Peer, StatusCode.OK, ElapsedMs(startedAt));
            return response;
        }
        catch (RpcException ex)
        {
            logger.LogWarning("gRPC call {GrpcMethod} from {GrpcPeer} finished with {GrpcStatus} in {Elapsed} ms: {GrpcDetail}",
                context.Method, context.Peer, ex.StatusCode, ElapsedMs(startedAt), ex.Status.Detail);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "gRPC call {GrpcMethod} from {GrpcPeer} failed with unexpected {ExceptionType} in {Elapsed} ms",
                context.Method, context.Peer, ex.GetType().Name, ElapsedMs(startedAt));
            throw;
        }
    }

    private static double ElapsedMs(long startedAt) => Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1);
}
