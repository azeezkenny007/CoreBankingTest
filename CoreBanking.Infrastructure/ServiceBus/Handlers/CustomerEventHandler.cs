//using CoreBankingTest.CORE.Events;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace CoreBankingTest.DAL.ServiceBus.Handlers
//{
//    public class CustomerEventHandler : BaseMessageHandler<CustomerCreatedEvent>
//    {
//        private readonly ILogger<CustomerEventHandler> _logger;
//        private readonly INotificationService _notificationService;
//        private readonly IReportingService _reportingService;

//        public CustomerEventHandler(
//            IServiceBusClientFactory clientFactory,
//            ServiceBusConfiguration config,
//            ILogger<CustomerEventHandler> logger,
//            INotificationService notificationService,
//            IReportingService reportingService)
//            : base(clientFactory, config.CustomerTopicName, "notifications", logger)
//        {
//            _logger = logger;
//            _notificationService = notificationService;
//            _reportingService = reportingService;
//        }

//        protected override async Task HandleMessageAsync(CustomerCreatedEvent customerEvent, CancellationToken cancellationToken)
//        {
//            _logger.LogInformation("Processing CustomerCreatedEvent for {CustomerId}", customerEvent.CustomerId);

//            try
//            {
//                // Send welcome notification
//                await _notificationService.SendWelcomeEmailAsync(customerEvent.Email, customerEvent.FirstName, cancellationToken);

//                // Record in reporting system
//                await _reportingService.RecordCustomerRegistrationAsync(customerEvent, cancellationToken);

//                _logger.LogInformation("Successfully processed CustomerCreatedEvent for {CustomerId}", customerEvent.CustomerId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to process CustomerCreatedEvent for {CustomerId}", customerEvent.CustomerId);
//                throw; // This will trigger retry or dead lettering
//            }
//        }
//    }
//}
