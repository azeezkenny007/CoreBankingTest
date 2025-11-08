using CoreBankingTest.API.Hubs.Interfaces;
using CoreBankingTest.API.Hubs.Management;
using CoreBankingTest.API.Hubs.Models;
using CoreBankingTest.CORE.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CoreBankingTest.API.Hubs.EventHandlers
{
    public class EnhancedRealTimeEventHandler :
       INotificationHandler<MoneyTransferredEvent>,
       INotificationHandler<AccountCreatedEvent>,
       INotificationHandler<InsufficientFundsEvent>
    {
        private readonly ILogger<EnhancedRealTimeEventHandler> _logger;
        private readonly IHubContext<EnhancedTransactionHub, IBankingClient> _hubContext;
        private readonly ConnectionStateService _connectionState;
        private static readonly ThreadLocal<Random> _random = new(() => new Random());

        public EnhancedRealTimeEventHandler(
            ILogger<EnhancedRealTimeEventHandler> logger,
            IHubContext<EnhancedTransactionHub, IBankingClient> hubContext,
            ConnectionStateService connectionState)
        {
            _logger = logger;
            _hubContext = hubContext;
            _connectionState = connectionState;
        }

        public async Task Handle(MoneyTransferredEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Processing real-time notification for transfer {TransactionId}",
                notification.TransactionId);

            try
            {
                // Notify source account
                var sourceNotification = new TransactionNotification
                {
                    TransactionId = notification.TransactionId.ToString(),
                    AccountNumber = notification.SourceAccountNumber.ToString(),
                    Amount = -notification.Amount.Amount, // Negative for debit
                    Type = "Transfer Debit",
                    Description = $"Transfer to {notification.DestinationAccountNumber}",
                    Timestamp = notification.TransferDate,
                    RunningBalance = await GetCurrentBalance(notification.SourceAccountNumber.ToString())
                };

                await _hubContext.Clients.Group($"account-{notification.SourceAccountNumber}")
                    .ReceiveTransactionNotification(sourceNotification);

                // Notify destination account
                var destNotification = new TransactionNotification
                {
                    TransactionId = notification.TransactionId.ToString(),
                    AccountNumber = notification.DestinationAccountNumber.ToString(),
                    Amount = notification.Amount.Amount, // Positive for credit
                    Type = "Transfer Credit",
                    Description = $"Transfer from {notification.SourceAccountNumber}",
                    Timestamp = notification.TransferDate,
                    RunningBalance = await GetCurrentBalance(notification.DestinationAccountNumber.ToString())
                };

                await _hubContext.Clients.Group($"account-{notification.DestinationAccountNumber}")
                    .ReceiveTransactionNotification(destNotification);

                // Send balance updates
                await SendBalanceUpdate(notification.SourceAccountNumber.ToString());
                await SendBalanceUpdate(notification.DestinationAccountNumber.ToString());

                _logger.LogInformation(
                    "Sent real-time notifications for transfer {TransactionId}",
                    notification.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send real-time notifications for transfer {TransactionId}",
                    notification.TransactionId);
            }
        }

        public async Task Handle(AccountCreatedEvent notification, CancellationToken cancellationToken)
        {
            var alert = new SystemAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                Message = $"New account created: {notification.AccountNumber}",
                Severity = "info",
                Timestamp = DateTime.UtcNow
            };

            // Notify admin team
            await _hubContext.Clients.Group("admin-team").ReceiveSystemAlert(alert);

            _logger.LogInformation("Notified admin team of new account creation");
        }

        public async Task Handle(InsufficientFundsEvent notification, CancellationToken cancellationToken)
        {
            var fraudAlert = new FraudAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                AccountNumber = notification.AccountNumber.ToString(),
                Description = $"Insufficient funds attempt: Attempted to withdraw {notification.RequestedAmount}",
                Severity = "warning",
                DetectedAt = DateTime.UtcNow
            };

            // Notify security team and account owner
            await _hubContext.Clients.Group("security-team").ReceiveFraudAlert(fraudAlert);
            await _hubContext.Clients.Group($"account-{notification.AccountNumber}").ReceiveFraudAlert(fraudAlert);

            _logger.LogWarning("Sent insufficient funds alert for account {AccountNumber}",
                notification.AccountNumber);
        }

        private async Task<decimal> GetCurrentBalance(string accountNumber)
        {
            // In production, this would query the current balance from the database
            // For demo purposes, return a simulated balance
            return 1000 + _random.Value!.Next(-500, 500);
        }

        private async Task SendBalanceUpdate(string accountNumber)
        {
            var balanceUpdate = new BalanceUpdate
            {
                AccountNumber = accountNumber,
                NewBalance = await GetCurrentBalance(accountNumber),
                Currency = "USD",
                UpdatedAt = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"account-{accountNumber}")
                .ReceiveBalanceUpdate(balanceUpdate);
        }
    }
}
