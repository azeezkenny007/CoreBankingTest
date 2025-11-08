using CoreBankingTest.API.Hubs.Interfaces;
using CoreBankingTest.API.Hubs.Management;
using CoreBankingTest.API.Hubs.Models;
using CoreBankingTest.APP.Accounts.Queries.GetAccountDetails;
using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CoreBankingTest.API.Hubs
{
    public class EnhancedTransactionHub : Hub<IBankingClient>
    {
        private readonly ILogger<EnhancedTransactionHub> _logger;
        private readonly IMediator _mediator;
        private readonly ConnectionStateService _connectionState;
        private readonly IHubContext<EnhancedTransactionHub, IBankingClient> _hubContext;

        public EnhancedTransactionHub(
            ILogger<EnhancedTransactionHub> logger,
            IMediator mediator,
            ConnectionStateService connectionState,
            IHubContext<EnhancedTransactionHub, IBankingClient> hubContext)
        {
            _logger = logger;
            _mediator = mediator;
            _connectionState = connectionState;
            _hubContext = hubContext;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var accountNumber = httpContext?.Request.Query["accountNumber"].ToString();
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(accountNumber))
            {
                _logger.LogWarning("Connection attempt without account number: {ConnectionId}", Context.ConnectionId);
                Context.Abort(); // Reject connection without account number
                return;
            }

            // Validate account access (in production, implement proper authorization)
            if (!await ValidateAccountAccess(accountNumber, userId))
            {
                _logger.LogWarning("Unauthorized connection attempt for account {AccountNumber}", accountNumber);
                Context.Abort();
                return;
            }

            _connectionState.ConnectionEstablished(Context.ConnectionId, accountNumber, userId);

            await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountNumber}");
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-clients");

            // Send connection confirmation
            await Clients.Caller.ConnectionStateChanged(new ConnectionState
            {
                IsConnected = true,
                StateChangedAt = DateTime.UtcNow,
                Message = "Connected to real-time transaction service"
            });

            // Send recent transactions on connection
            await SendRecentTransactions(accountNumber);

            _logger.LogInformation("Client {ConnectionId} connected for account {AccountNumber}",
                Context.ConnectionId, accountNumber);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var status = exception == null ? ConnectionStatus.Disconnected : ConnectionStatus.Faulted;
            _connectionState.ConnectionTerminated(Context.ConnectionId, status);

            if (exception != null)
            {
                _logger.LogError(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);

                // Notify admin about faulty disconnections
                await NotifyAdminOfFaultyDisconnection(Context.ConnectionId, exception);
            }

            await base.OnDisconnectedAsync(exception);
        }

        [HubMethodName("SubscribeToTransactions")]
        public async Task SubscribeToTransactions(string accountNumber, DateTime? fromDate = null)
        {
            _connectionState.ConnectionActivity(Context.ConnectionId);

            if (!await ValidateAccountAccess(accountNumber, Context.UserIdentifier))
            {
                _logger.LogWarning("Unauthorized subscription attempt for account {AccountNumber}", accountNumber);
                await Clients.Caller.ReceiveSystemAlert(new SystemAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    Message = "Unauthorized subscription attempt",
                    Severity = "error",
                    Timestamp = DateTime.UtcNow
                });
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"transactions-{accountNumber}");

            _logger.LogInformation("Client {ConnectionId} subscribed to transactions for {AccountNumber}",
                Context.ConnectionId, accountNumber);

            await Clients.Caller.ReceiveSystemAlert(new SystemAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                Message = $"Subscribed to transaction feed for account {accountNumber}",
                Severity = "info",
                Timestamp = DateTime.UtcNow
            });
        }

        [HubMethodName("UnsubscribeFromTransactions")]
        public async Task UnsubscribeFromTransactions(string accountNumber)
        {
            _connectionState.ConnectionActivity(Context.ConnectionId);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"transactions-{accountNumber}");

            _logger.LogInformation("Client {ConnectionId} unsubscribed from transactions for {AccountNumber}",
                Context.ConnectionId, accountNumber);

            await Clients.Caller.ReceiveSystemAlert(new SystemAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                Message = $"Unsubscribed from transaction feed for account {accountNumber}",
                Severity = "info",
                Timestamp = DateTime.UtcNow
            });
        }

        // Admin method to broadcast to all connected clients
        [HubMethodName("BroadcastToAll")]
        public async Task BroadcastToAll(string message, string severity = "info")
        {
            // In production, add authorization check for admin role
            _connectionState.ConnectionActivity(Context.ConnectionId);

            var alert = new SystemAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                Message = message,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            };

            await Clients.All.ReceiveSystemAlert(alert);
            _logger.LogInformation("Admin broadcast: {Message}", message);
        }

        // Method to get connection statistics
        [HubMethodName("GetConnectionStats")]
        public ConnectionStats GetConnectionStats()
        {
            var connections = _connectionState.GetActiveConnections().ToList();

            return new ConnectionStats
            {
                TotalConnections = connections.Count,
                ActiveConnections = connections.Count(c => c.Status == ConnectionStatus.Connected),
                TotalMessagesToday = connections.Sum(c => c.MessagesSent + c.MessagesReceived),
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime
            };
        }

        private async Task<bool> ValidateAccountAccess(string accountNumber, string? userId)
        {
            // In production, implement proper account authorization
            // For now, basic validation
            try
            {
                var query = new GetAccountDetailsQuery
                {
                    AccountNumber = AccountNumber.Create(accountNumber)
                };
                var result = await _mediator.Send(query);
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        private async Task SendRecentTransactions(string accountNumber)
        {
            try
            {
                var query = new GetTransactionHistoryQuery
                {
                    AccountNumber = AccountNumber.Create(accountNumber),
                    PageSize = 10,
                    StartDate = DateTime.UtcNow.AddHours(-24) // Last 24 hours
                };

                var result = await _mediator.Send(query);

                if (result.IsSuccess && result.Data!.Transactions.Any())
                {
                    foreach (var transaction in result.Data.Transactions.OrderByDescending(t => t.Timestamp))
                    {
                        var notification = new TransactionNotification
                        {
                            TransactionId = transaction.TransactionId.ToString(),
                            AccountNumber = accountNumber,
                            Amount = transaction.Amount,
                            Type = transaction.Type.ToString(),
                            Description = transaction.Description,
                            Timestamp = transaction.Timestamp,
                            RunningBalance = 0 // Would calculate from transaction history
                        };

                        await Clients.Caller.ReceiveTransactionNotification(notification);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending recent transactions to {AccountNumber}", accountNumber);
            }
        }

        private async Task NotifyAdminOfFaultyDisconnection(string connectionId, Exception exception)
        {
            var alert = new SystemAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                Message = $"Client {connectionId} disconnected with error: {exception.Message}",
                Severity = "warning",
                Timestamp = DateTime.UtcNow
            };

            await Clients.Group("admin-team").ReceiveSystemAlert(alert);
        }
    }
}
