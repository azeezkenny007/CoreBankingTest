using Microsoft.AspNetCore.SignalR;

namespace CoreBankingTest.API.Hubs
{

    public class ErrorHandlingHubFilter : IHubFilter
    {
        private readonly ILogger<ErrorHandlingHubFilter> _logger;

        public ErrorHandlingHubFilter(ILogger<ErrorHandlingHubFilter> logger)
        {
            _logger = logger;
        }

        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            try
            {
                _logger.LogInformation("SignalR method {MethodName} called by {UserId}",
                    invocationContext.HubMethodName, invocationContext.Context.UserIdentifier);

                return await next(invocationContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SignalR method {MethodName}", invocationContext.HubMethodName);

                // Notify the caller about the error
                await invocationContext.Hub.Clients.Caller.SendAsync("Error",
                    $"Error executing {invocationContext.HubMethodName}: {ex.Message}");

                throw;
            }
        }

        public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            _logger.LogInformation("SignalR client connected: {ConnectionId}", context.Context.ConnectionId);
            return next(context);
        }

        public Task OnDisconnectedAsync(
            HubLifetimeContext context,
            Exception? exception,
            Func<HubLifetimeContext, Exception?, Task> next)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "SignalR client disconnected with error: {ConnectionId}",
                    context.Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("SignalR client disconnected: {ConnectionId}",
                    context.Context.ConnectionId);
            }

            return next(context, exception);
        }
    }
}
