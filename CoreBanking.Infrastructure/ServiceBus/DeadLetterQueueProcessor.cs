using Azure.Messaging.ServiceBus;
using CoreBankingTest.CORE.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class DeadLetterQueueProcessor : IDeadLetterQueueProcessor
    {
        private readonly IServiceBusClientFactory _clientFactory;
        private readonly ILogger<DeadLetterQueueProcessor> _logger;
        private readonly IBankingServiceBusSender _bankingServiceBusSender;

        public DeadLetterQueueProcessor(
            IServiceBusClientFactory clientFactory,
            IBankingServiceBusSender bankingServiceBusSender,
            ILogger<DeadLetterQueueProcessor> logger)
        {
            _clientFactory = clientFactory;
            _bankingServiceBusSender = bankingServiceBusSender;
            _logger = logger;
        }

        public async Task<List<DeadLetterMessage>> GetDeadLetterMessagesAsync(
            string queueOrTopicName,
            string subscriptionName,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            var deadLetterMessages = new List<DeadLetterMessage>();
            ServiceBusReceiver dlqReceiver = null;

            try
            {
                var dlqEntityPath = string.IsNullOrEmpty(subscriptionName)
                    ? $"{queueOrTopicName}/$deadletterqueue"  // Queue DLQ
                    : $"{queueOrTopicName}/Subscriptions/{subscriptionName}/$deadletterqueue";  // Topic Subscription DLQ

                dlqReceiver = _clientFactory.CreateReceiver(dlqEntityPath, new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock
                });

                var messages = await dlqReceiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken);

                foreach (var message in messages)
                {
                    var deadLetterMessage = new DeadLetterMessage
                    {
                        MessageId = message.MessageId,
                        DeadLetterReason = message.DeadLetterReason,
                        DeadLetterErrorDescription = message.DeadLetterErrorDescription,
                        EnqueuedTime = message.EnqueuedTime,
                        Content = Encoding.UTF8.GetString(message.Body),
                        Properties = message.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty),
                        DeliveryCount = message.DeliveryCount
                    };

                    deadLetterMessages.Add(deadLetterMessage);

                    // Abandon the message to keep it in DLQ for now
                    await dlqReceiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                }

                return deadLetterMessages;
            }
            finally
            {
                if (dlqReceiver != null)
                {
                    await dlqReceiver.DisposeAsync();
                }
            }
        }

        public async Task<int> ReprocessDeadLetterMessagesAsync(
            string sourceQueue,
            string destinationQueue,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            var processedCount = 0;
            ServiceBusReceiver dlqReceiver = null;

            try
            {
                var dlqPath = $"{sourceQueue}/$deadletterqueue";
                dlqReceiver = _clientFactory.CreateReceiver(dlqPath, new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock
                });

                var messages = await dlqReceiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(30), cancellationToken);

                foreach (var message in messages)
                {
                    try
                    {
                        // Create a new message with the same body but updated properties
                        var reprocessedMessage = new ServiceBusMessage(message.Body)
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            CorrelationId = message.CorrelationId,
                            Subject = message.Subject,
                            ContentType = message.ContentType
                        };

                        // Copy application properties, marking as reprocessed
                        foreach (var prop in message.ApplicationProperties)
                        {
                            reprocessedMessage.ApplicationProperties[prop.Key] = prop.Value;
                        }
                        reprocessedMessage.ApplicationProperties["Reprocessed"] = true;
                        reprocessedMessage.ApplicationProperties["OriginalMessageId"] = message.MessageId;
                        reprocessedMessage.ApplicationProperties["ReprocessedAt"] = DateTime.UtcNow;

                        // Send the reprocessed message
                        await _bankingServiceBusSender.SendMessageAsync(destinationQueue, reprocessedMessage, cancellationToken);

                        // Complete the original DLQ message
                        await dlqReceiver.CompleteMessageAsync(message, cancellationToken);

                        processedCount++;
                        _logger.LogInformation("Reprocessed DLQ message {OriginalMessageId} as {NewMessageId}",
                            message.MessageId, reprocessedMessage.MessageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reprocess DLQ message {MessageId}", message.MessageId);
                        await dlqReceiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                    }
                }

                return processedCount;
            }
            finally
            {
                if (dlqReceiver != null) await dlqReceiver.DisposeAsync();
            }
        }

        public async Task ProcessDeadLetterMessagesAsync(string queueOrTopicName, string subscriptionName, CancellationToken cancellationToken)
        {
            var dlqMessages = await GetDeadLetterMessagesAsync(queueOrTopicName, subscriptionName, 100, cancellationToken);

            foreach (var dlqMessage in dlqMessages)
            {
                _logger.LogWarning(
                    "DLQ Message: ID={MessageId}, Reason={Reason}, Error={Error}, Enqueued={EnqueuedTime}",
                    dlqMessage.MessageId,
                    dlqMessage.DeadLetterReason,
                    dlqMessage.DeadLetterErrorDescription,
                    dlqMessage.EnqueuedTime);

                // Here you could implement custom logic for different types of DLQ messages
                // For example, send alerts, update monitoring systems, etc.
            }
        }
    }
}
