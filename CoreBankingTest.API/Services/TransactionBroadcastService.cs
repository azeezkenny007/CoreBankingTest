using CoreBankingTest.API.Hubs;
using CoreBankingTest.API.Hubs.Interfaces;
using CoreBankingTest.API.Hubs.Models;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CoreBankingTest.API.Services
{
    public class TransactionBroadcastService : IHostedService, IDisposable
    {
        private readonly ILogger<TransactionBroadcastService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<EnhancedTransactionHub, IBankingClient> _hubContext;
        private Timer? _broadcastTimer;
        private readonly Random _random = new();

        public TransactionBroadcastService(
            ILogger<TransactionBroadcastService> logger,
            IServiceProvider serviceProvider,
            IHubContext<EnhancedTransactionHub, IBankingClient> hubContext)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Transaction Broadcast Service starting...");

            // Start broadcasting simulated transactions (for demo purposes)
            _broadcastTimer = new Timer(BroadcastSimulatedTransactions, null,
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)); // Every 30 seconds

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Transaction Broadcast Service stopping...");
            _broadcastTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async void BroadcastSimulatedTransactions(object? state)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Get some active accounts for simulation
                var activeAccounts = await GetActiveAccounts(mediator);
                if (!activeAccounts.Any()) return;

                // Simulate a random transaction
                var sourceAccount = activeAccounts[_random.Next(activeAccounts.Count)];
                var destinationAccount = activeAccounts[_random.Next(activeAccounts.Count)];

                // Ensure different accounts
                while (destinationAccount == sourceAccount && activeAccounts.Count > 1)
                {
                    destinationAccount = activeAccounts[_random.Next(activeAccounts.Count)];
                }

                var amount = _random.Next(10, 500);
                var transactionType = amount > 200 ? "Transfer" : "Payment";

                var notification = new TransactionNotification
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    AccountNumber = sourceAccount,
                    Amount = -amount, // Debit from source
                    Type = "Debit",
                    Description = $"{transactionType} to {destinationAccount}",
                    Timestamp = DateTime.UtcNow,
                    RunningBalance = 0 // Would be calculated from actual balance
                };

                // Broadcast to source account
                await _hubContext.Clients.Group($"account-{sourceAccount}")
                    .ReceiveTransactionNotification(notification);

                // Also send to destination account if different
                if (sourceAccount != destinationAccount)
                {
                    var creditNotification = new TransactionNotification
                    {
                        TransactionId = notification.TransactionId,
                        AccountNumber = destinationAccount,
                        Amount = amount, // Credit to destination
                        Type = "Credit",
                        Description = $"{transactionType} from {sourceAccount}",
                        Timestamp = DateTime.UtcNow,
                        RunningBalance = 0
                    };

                    await _hubContext.Clients.Group($"account-{destinationAccount}")
                        .ReceiveTransactionNotification(creditNotification);
                }

                _logger.LogDebug("Broadcast simulated transaction {TransactionId}", notification.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting simulated transactions");
            }
        }

        private async Task<List<string>> GetActiveAccounts(IMediator mediator)
        {
            // In production, this would query actual active accounts
            // For demo, return some sample account numbers
            return new List<string> { "123456789", "987654321", "555555555", "111111111" };
        }

        public void Dispose()
        {
            _broadcastTimer?.Dispose();
        }
    }
}
