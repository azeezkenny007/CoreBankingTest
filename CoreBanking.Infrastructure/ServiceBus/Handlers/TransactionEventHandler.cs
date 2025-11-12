//using CoreBankingTest.CORE.Events;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace CoreBankingTest.DAL.ServiceBus.Handlers
//{
//    public class TransactionEventHandler : BaseMessageHandler<MoneyTransferredEvent>
//    {
//        private readonly ILogger<TransactionEventHandler> _logger;
//        private readonly IFraudDetectionService _fraudDetectionService;
//        private readonly INotificationService _notificationService;

//        public TransactionEventHandler(
//            IServiceBusClientFactory clientFactory,
//            ServiceBusConfiguration config,
//            ILogger<TransactionEventHandler> logger,
//            IFraudDetectionService fraudDetectionService,
//            INotificationService notificationService)
//            : base(clientFactory, config.TransactionTopicName, "fraud-detection", logger)
//        {
//            _logger = logger;
//            _fraudDetectionService = fraudDetectionService;
//            _notificationService = notificationService;
//        }

//        protected override async Task HandleMessageAsync(MoneyTransferredEvent transactionEvent, CancellationToken cancellationToken)
//        {
//            _logger.LogInformation("Processing MoneyTransferredEvent for transaction {TransactionId}",
//                transactionEvent.TransactionId);

//            // Check for potential fraud
//            var fraudResult = await _fraudDetectionService.CheckTransactionAsync(transactionEvent, cancellationToken);

//            if (fraudResult.IsSuspicious)
//            {
//                _logger.LogWarning(
//                    "Suspicious transaction detected: {TransactionId}. Score: {Score}, Reason: {Reason}",
//                    transactionEvent.TransactionId, fraudResult.RiskScore, fraudResult.Reason);

//                await _notificationService.SendFraudAlertAsync(transactionEvent, fraudResult, cancellationToken);
//            }

//            // Send transaction notification
//            await _notificationService.SendTransactionNotificationAsync(transactionEvent, cancellationToken);

//            _logger.LogInformation("Completed fraud check for transaction {TransactionId}", transactionEvent.TransactionId);
//        }
//    }
//}
