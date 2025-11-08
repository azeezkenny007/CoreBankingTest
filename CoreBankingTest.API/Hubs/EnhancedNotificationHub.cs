using CoreBankingTest.API.Hubs.Interfaces;
using CoreBankingTest.API.Hubs.Models;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CoreBankingTest.API.Hubs
{
    public class EnhancedNotificationHub : Hub<IBankingClient>
    {
        private readonly ILogger<EnhancedNotificationHub> _logger;
        private readonly IMediator _mediator;
        private static readonly ConcurrentDictionary<string, UserSession> _userSessions = new();

        public EnhancedNotificationHub(ILogger<EnhancedNotificationHub> logger, IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var accountNumber = httpContext?.Request.Query["accountNumber"].ToString();
            var userId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(accountNumber))
            {
                var session = new UserSession(Context.ConnectionId, accountNumber, userId);
                _userSessions[Context.ConnectionId] = session;

                await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountNumber}");

                _logger.LogInformation("User {UserId} connected for account {AccountNumber}",
                    userId, accountNumber);

                // Notify client of successful connection
                await Clients.Caller.ConnectionStateChanged(new ConnectionState
                {
                    IsConnected = true,
                    StateChangedAt = DateTime.UtcNow,
                    Message = "Connected to real-time banking service"
                });
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_userSessions.TryRemove(Context.ConnectionId, out var session))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"account-{session.AccountNumber}");

                _logger.LogInformation("User {UserId} disconnected from account {AccountNumber}",
                    session.UserId, session.AccountNumber);

                // Notify client of disconnection (if still connected)
                if (exception == null)
                {
                    await Clients.Caller.ConnectionStateChanged(new ConnectionState
                    {
                        IsConnected = false,
                        StateChangedAt = DateTime.UtcNow,
                        Message = "Disconnected from banking service"
                    });
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to notify specific account of transaction
        public async Task NotifyTransaction(string accountNumber, TransactionNotification notification)
        {
            _logger.LogInformation("Sending transaction notification to account {AccountNumber}", accountNumber);

            await Clients.Group($"account-{accountNumber}").ReceiveTransactionNotification(notification);
        }

        // Method to notify balance update
        public async Task NotifyBalanceUpdate(string accountNumber, BalanceUpdate update)
        {
            await Clients.Group($"account-{accountNumber}").ReceiveBalanceUpdate(update);
        }

        // Broadcast fraud alert to security team and affected user
        public async Task BroadcastFraudAlert(FraudAlert alert)
        {
            // Send to security team group
            await Clients.Group("security-team").ReceiveFraudAlert(alert);

            // Send to affected account
            await Clients.Group($"account-{alert.AccountNumber}").ReceiveFraudAlert(alert);

            _logger.LogWarning("Broadcast fraud alert {AlertId} for account {AccountNumber}",
                alert.AlertId, alert.AccountNumber);
        }

        // Admin method to broadcast system-wide alerts
        public async Task BroadcastSystemAlert(SystemAlert alert)
        {
            await Clients.All.ReceiveSystemAlert(alert);
            _logger.LogInformation("Broadcast system alert: {AlertId}", alert.AlertId);
        }

        // Get connected users (admin functionality)
        public IEnumerable<UserSession> GetConnectedUsers()
        {
            return _userSessions.Values;
        }
    }

    public record UserSession(string ConnectionId, string AccountNumber, string? UserId)
    {
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }
}
