using CoreBankingTest.API.Hubs;
using CoreBankingTest.CORE.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CoreBankingTest.API.Services
{
    public class NotificationBroadcaster : INotificationBroadcaster
    {
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly ILogger<NotificationBroadcaster> _logger;

        public NotificationBroadcaster(
            IHubContext<NotificationHub> notificationHub,
            ILogger<NotificationBroadcaster> logger)
        {
            _notificationHub = notificationHub;
            _logger = logger;
        }

        public async Task BroadcastCustomerCreatedAsync(Guid customerId, string customerName, string email, int creditScore)
        {
            try
            {
                await _notificationHub.Clients.All.SendAsync("CustomerCreated", new
                {
                    CustomerId = customerId,
                    CustomerName = customerName,
                    Email = email,
                    CreditScore = creditScore,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Broadcasted customer creation for {CustomerId}", customerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast customer creation for {CustomerId}", customerId);
                throw;
            }
        }

        public Task BroadcastFraudAlertAsync(Guid transactionId, string reason, decimal amount)
        {
            throw new NotImplementedException();
        }

        public async Task BroadcastTransactionAsync(Guid transactionId, decimal amount, string transactionType, Guid fromAccount, Guid toAccount)
        {
            // Implement transaction broadcasting
            await Task.CompletedTask;
        }

        public Task BroadcastTransactionAsync(Guid transactionId, decimal amount, string transactionType, string fromAccount, string toAccount)
        {
            throw new NotImplementedException();
        }
    }
}
