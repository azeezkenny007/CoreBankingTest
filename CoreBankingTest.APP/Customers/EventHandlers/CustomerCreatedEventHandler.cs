using CoreBankingTest.CORE.Events;
using CoreBankingTest.CORE.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Customers.EventHandlers
{
    public class CustomerCreatedEventHandler : INotificationHandler<CustomerCreatedEvent>
    {
        private readonly ILogger<CustomerCreatedEventHandler> _logger;
        private readonly INotificationBroadcaster _notificationBroadcaster;

        public CustomerCreatedEventHandler(
            ILogger<CustomerCreatedEventHandler> logger,
            INotificationBroadcaster notificationBroadcaster)
        {
            _logger = logger;
            _notificationBroadcaster = notificationBroadcaster;
        }

        public async Task Handle(CustomerCreatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing CustomerCreatedEvent for {CustomerId}", notification.CustomerId);

            try
            {
                // Use the interface - no API layer reference!
                await _notificationBroadcaster.BroadcastCustomerCreatedAsync(
                    notification.CustomerId.Value,
                    $"{notification.FirstName} {notification.LastName}",
                    notification.Email,
                    notification.CreditScore);

                _logger.LogInformation(
                    "Real-time notification sent for new customer: {FirstName} {LastName} ({Email})",
                    notification.FirstName, notification.LastName, notification.Email);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process CustomerCreatedEvent for {CustomerId}", notification.CustomerId);
                // In event handling, we typically don't throw to avoid breaking the event pipeline
                // The event will be retried or go to dead letter queue
            }
        }
    }
}
