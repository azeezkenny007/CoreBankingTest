using CoreBankingTest.CORE.Events;
using CoreBankingTest.CORE.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Transactions.EventHandlers
{
    public class MoneyTransferredEventHandler : INotificationHandler<MoneyTransferredEvent>
    {
        private readonly ILogger<MoneyTransferredEventHandler> _logger;
        private readonly IFraudDetectionService _fraudDetectionService;
        private readonly INotificationBroadcaster _notificationBroadcaster;

        public MoneyTransferredEventHandler(
            ILogger<MoneyTransferredEventHandler> logger,
            IFraudDetectionService fraudDetectionService,
            INotificationBroadcaster notificationBroadcaster)
        {
            _logger = logger;
            _fraudDetectionService = fraudDetectionService;
            _notificationBroadcaster = notificationBroadcaster;
        }

        public async Task Handle(MoneyTransferredEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing MoneyTransferredEvent for transaction {TransactionId}",
                notification.TransactionId);

            // Check for potential fraud
            var fraudResult = await _fraudDetectionService.CheckTransactionAsync(notification, cancellationToken);

            if (fraudResult.IsSuspicious)
            {
                _logger.LogWarning(
                    "Suspicious transaction detected: {TransactionId}. Score: {Score}, Reason: {Reason}",
                    notification.TransactionId, fraudResult.RiskScore, fraudResult.Reason);

                // Use your notification broadcaster instead of direct notification service
                await _notificationBroadcaster.BroadcastFraudAlertAsync(
                    notification.TransactionId.Value,
                    fraudResult.Reason,
                    notification.Amount.Amount);
            }

            // Send transaction notification
            await _notificationBroadcaster.BroadcastTransactionAsync(
                notification.TransactionId.Value,
                notification.Amount.Amount,
                "Transfer",
                notification.SourceAccountNumber.Value,
                notification.DestinationAccountNumber.Value);

            _logger.LogInformation("Completed fraud check for transaction {TransactionId}", notification.TransactionId);
        }

    }
}
