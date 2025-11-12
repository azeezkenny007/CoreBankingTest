using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public abstract class BaseMessageHandler<TMessage> : IAsyncDisposable
    {
        private readonly ServiceBusProcessor _processor;
        private readonly ILogger<BaseMessageHandler<TMessage>> _logger;
        private bool _disposed = false;
        protected readonly IMediator _mediator;

        protected BaseMessageHandler(
            IServiceBusClientFactory clientFactory,
            string queueOrTopicName,
            string subscriptionName,
            ILogger<BaseMessageHandler<TMessage>> logger,
            IMediator mediator,
            ServiceBusProcessorOptions processorOptions = null)
        {
            _logger = logger;
            _mediator = mediator;

            processorOptions ??= new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 5,
                PrefetchCount = 10,
                AutoCompleteMessages = false
            };

            // Corrected processor creation
            if (string.IsNullOrEmpty(subscriptionName))
            {
                // Processing from a QUEUE
                _processor = clientFactory.CreateProcessor(queueOrTopicName, processorOptions);
            }
            else
            {
                // Processing from a TOPIC SUBSCRIPTION
                _processor = clientFactory.CreateProcessor(queueOrTopicName, subscriptionName, processorOptions);
            }

            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            await _processor.StartProcessingAsync(cancellationToken);
            _logger.LogInformation("Started processing messages for {HandlerType}", typeof(TMessage).Name);
        }

        public async Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            _logger.LogInformation("Stopped processing messages for {HandlerType}", typeof(TMessage).Name);
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            try
            {
                var message = args.Message;
                _logger.LogDebug("Processing message {MessageId} for {HandlerType}",
                    message.MessageId, typeof(TMessage).Name);

                // Deserialize message
                var messageBody = Encoding.UTF8.GetString(message.Body);
                var domainEvent = JsonSerializer.Deserialize<TMessage>(messageBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (domainEvent != null)
                {
                    // Use MediatR to publish to your existing CQRS handlers
                    await _mediator.Publish(domainEvent, args.CancellationToken);
                    await args.CompleteMessageAsync(message, args.CancellationToken);

                    _logger.LogInformation("Successfully processed message {MessageId}", message.MessageId);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize message {MessageId}. Sending to dead letter queue.",
                        message.MessageId);
                    await args.DeadLetterMessageAsync(message,
                        "DeserializationFailed",
                        "Failed to deserialize message body",
                        args.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", args.Message.MessageId);

                if (args.Message.DeliveryCount >= 5)
                {
                    await args.DeadLetterMessageAsync(args.Message,
                        "ProcessingError",
                        $"Failed after {args.Message.DeliveryCount} attempts: {ex.Message}",
                        args.CancellationToken);
                }
                else
                {
                    throw;
                }
            }
        }

        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception,
                "Error in message processor for {HandlerType}. Source: {ErrorSource}",
                typeof(TMessage).Name, args.ErrorSource);
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _processor.DisposeAsync();
                _disposed = true;
            }
        }
    }
}
