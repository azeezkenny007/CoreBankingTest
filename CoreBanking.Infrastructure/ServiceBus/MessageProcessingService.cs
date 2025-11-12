using CoreBankingTest.DAL.ServiceBus.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class MessageProcessingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MessageProcessingService> _logger;
        private readonly List<IAsyncDisposable> _processors = new();

        public MessageProcessingService(IServiceProvider serviceProvider, ILogger<MessageProcessingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting message processing service");

            // Start all message handlers
            await StartMessageHandlersAsync(stoppingToken);

            _logger.LogInformation("All message handlers started");

            // Keep the service running until stopped
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            _logger.LogInformation("Stopping message processing service");
        }

        private async Task StartMessageHandlersAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                // Start customer event handler
                var customerHandler = scope.ServiceProvider.GetService<CustomerEventServiceBusHandler>();
                if (customerHandler != null)
                {
                    await customerHandler.StartProcessingAsync(stoppingToken);
                    _processors.Add(customerHandler);
                    _logger.LogInformation("Started CustomerEventServiceBusHandler");
                }

                // Start transaction event handler
                var transactionHandler = scope.ServiceProvider.GetService<TransactionEventServiceBusHandler>();
                if (transactionHandler != null)
                {
                    await transactionHandler.StartProcessingAsync(stoppingToken);
                    _processors.Add(transactionHandler);
                    _logger.LogInformation("Started TransactionEventServiceBusHandler");
                }

                // Add other handlers as needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting message handlers");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping all message processors");

            foreach (var processor in _processors)
            {
                try
                {
                    await processor.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing message processor");
                }
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
