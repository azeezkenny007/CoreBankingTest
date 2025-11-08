using Grpc.Core;
using Grpc.Core.Interceptors;

namespace CoreBankingTest.API.gRPC.Interceptors
{
    public class ExceptionInterceptor : Interceptor
    {
        private readonly ILogger<ExceptionInterceptor> _logger;

        public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)
        {
            _logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (RpcException)
            {
                throw; // Already handled RpcException, re-throw
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing gRPC call {Method}", context.Method);

                var status = ex switch
                {
                    ArgumentException => new Status(StatusCode.InvalidArgument, ex.Message),
                    KeyNotFoundException => new Status(StatusCode.NotFound, ex.Message),
                    UnauthorizedAccessException => new Status(StatusCode.PermissionDenied, ex.Message),
                    InvalidOperationException => new Status(StatusCode.FailedPrecondition, ex.Message),
                    _ => new Status(StatusCode.Internal, "An internal error occurred")
                };

                throw new RpcException(status);
            }
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await continuation(request, responseStream, context);
            }
            catch (Exception ex) when (ex is not RpcException and not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in server streaming call {Method}", context.Method);
                throw new RpcException(new Status(StatusCode.Internal, "Streaming error occurred"));
            }
        }
    }
}
