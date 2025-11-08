using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CoreBankingTest.API.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        private static readonly ConcurrentDictionary<string, string> _userConnections = new();

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var accountNumber = httpContext?.Request.Query["accountNumber"].ToString();

            if (!string.IsNullOrEmpty(accountNumber))
            {
                _userConnections[Context.ConnectionId] = accountNumber;
                await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountNumber}");

                _logger.LogInformation("Client {ConnectionId} connected for account {AccountNumber}",
                    Context.ConnectionId, accountNumber);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_userConnections.TryRemove(Context.ConnectionId, out var accountNumber))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"account-{accountNumber}");

                _logger.LogInformation("Client {ConnectionId} disconnected for account {AccountNumber}",
                    Context.ConnectionId, accountNumber);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Client can call this method to join specific groups
        public async Task SubscribeToAccount(string accountNumber)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountNumber}");
            _userConnections[Context.ConnectionId] = accountNumber;

            _logger.LogInformation("Client {ConnectionId} subscribed to account {AccountNumber}",
                Context.ConnectionId, accountNumber);

            await Clients.Caller.SendAsync("SubscriptionConfirmed",
                $"Subscribed to account {accountNumber}");
        }

        public async Task UnsubscribeFromAccount(string accountNumber)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"account-{accountNumber}");

            _logger.LogInformation("Client {ConnectionId} unsubscribed from account {AccountNumber}",
                Context.ConnectionId, accountNumber);

            await Clients.Caller.SendAsync("UnsubscriptionConfirmed",
                $"Unsubscribed from account {accountNumber}");
        }
    }
}
