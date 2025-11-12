using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class DeadLetterQueueMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DeadLetterQueueMonitorService> _logger;
        private readonly TimeSpan _monitoringInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

        public DeadLetterQueueMonitorService(IServiceProvider serviceProvider, ILogger<DeadLetterQueueMonitorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Dead Letter Queue monitoring service");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorDeadLetterQueuesAsync(stoppingToken);
                    await Task.Delay(_monitoringInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring dead letter queues");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retry
                }
            }

            _logger.LogInformation("Stopped Dead Letter Queue monitoring service");
        }

        private async Task MonitorDeadLetterQueuesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dlqProcessor = scope.ServiceProvider.GetRequiredService<IDeadLetterQueueProcessor>();
            var serviceBusConfig = scope.ServiceProvider.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value;

            try
            {
                // Monitor customer events DLQ
                var customerDlqMessages = await dlqProcessor.GetDeadLetterMessagesAsync(
                    serviceBusConfig.CustomerTopicName, "notifications", 10, cancellationToken);

                if (customerDlqMessages.Any())
                {
                    _logger.LogWarning("Found {Count} messages in Customer events DLQ", customerDlqMessages.Count);
                    // You could add alerting logic here
                }

                // Monitor transaction events DLQ
                var transactionDlqMessages = await dlqProcessor.GetDeadLetterMessagesAsync(
                    serviceBusConfig.TransactionTopicName, "fraud-detection", 10, cancellationToken);

                if (transactionDlqMessages.Any())
                {
                    _logger.LogWarning("Found {Count} messages in Transaction events DLQ", transactionDlqMessages.Count);
                    // You could add alerting logic here
                }

                // Add monitoring for other queues as needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DLQ monitoring cycle");
            }
        }
    }

}
