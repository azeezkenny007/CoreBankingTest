using Azure.Messaging.ServiceBus;
using CoreBankingTest.DAL.ServiceBus;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ServiceBusSender = Azure.Messaging.ServiceBus.ServiceBusSender;
//using ServiceBusSender = Azure.Messaging.ServiceBus.ServiceBusSender;

namespace CoreBanking.Infrastructure.ServiceBus
{
    public class ServiceBusClientFactory : IServiceBusClientFactory, IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly ILogger<ServiceBusClientFactory> _logger;
        private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
        private readonly ConcurrentDictionary<string, ServiceBusReceiver> _receivers = new();

        public ServiceBusClientFactory(string connectionString, ILogger<ServiceBusClientFactory> logger)
        {
            _client = new ServiceBusClient(connectionString, new ServiceBusClientOptions
            {
                RetryOptions = new ServiceBusRetryOptions
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = 5,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(30)
                }
            });
            _logger = logger;
        }

        public ServiceBusClient CreateClient() => _client;

        public ServiceBusSender CreateSender(string queueOrTopicName)
        {
            return _senders.GetOrAdd(queueOrTopicName, name =>
            {
                var sender = _client.CreateSender(name);
                _logger.LogDebug("Created Service Bus sender for {Destination}", name);
                return sender;
            });
        }

        public ServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options = null)
        {
            var key = $"{queueName}-{options?.ReceiveMode}";
            return _receivers.GetOrAdd(key, _ =>
            {
                var receiver = _client.CreateReceiver(queueName, options ?? new ServiceBusReceiverOptions());
                _logger.LogDebug("Created Service Bus receiver for {Queue}", queueName);
                return receiver;
            });
        }

        public ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options = null)
        {
            var processor = _client.CreateProcessor(queueName, options ?? new ServiceBusProcessorOptions());
            _logger.LogDebug("Created Service Bus processor for queue: {Queue}", queueName);
            return processor;
        }

        public ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options = null)
        {
            var processor = _client.CreateProcessor(topicName, subscriptionName, options ?? new ServiceBusProcessorOptions());
            _logger.LogDebug("Created Service Bus processor for topic: {Topic}, subscription: {Subscription}",
                topicName, subscriptionName);
            return processor;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var sender in _senders.Values)
            {
                await sender.DisposeAsync();
            }
            foreach (var receiver in _receivers.Values)
            {
                await receiver.DisposeAsync();
            }
            await _client.DisposeAsync();
        }

        CoreBankingTest.DAL.ServiceBus.ServiceBusSender IServiceBusClientFactory.CreateSender(string queueOrTopicName)
        {
            throw new NotImplementedException();
        }
    }
}
