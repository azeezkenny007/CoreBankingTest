using Azure.Messaging.ServiceBus;
using CoreBankingTest.CORE.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class ServiceBusEventPublisher : IEventPublisher
    {
        private readonly IBankingServiceBusSender _serviceBusSender;
        private readonly ServiceBusConfiguration _config;
        private readonly ILogger<ServiceBusEventPublisher> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public ServiceBusEventPublisher(
            IBankingServiceBusSender serviceBusSender,
            ServiceBusConfiguration config,
            ILogger<ServiceBusEventPublisher> logger)
        {
            _serviceBusSender = serviceBusSender;
            _config = config;
            _logger = logger;

            // Add retry policy for transient failures
            _retryPolicy = Policy
                .Handle<Exception>(ex => IsTransientException(ex))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, delay, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} for event publishing after {Delay}ms. Error: {Error}",
                            retryCount, delay.TotalMilliseconds, exception.Message);
                    });
        }

        public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
            where TEvent : IDomainEvent
        {
            var topicName = GetTopicNameForEvent(typeof(TEvent));
            var eventType = domainEvent.GetType().Name;

            var (eventData, properties) = CreateMessageData(domainEvent, eventType);

            // Use retry policy for resilience
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _serviceBusSender.SendMessageAsync(topicName, eventData, properties, cancellationToken);

                _logger.LogInformation(
                    "Published {EventType} with ID {EventId} to {Topic}",
                    eventType, domainEvent.EventId, topicName);
            });
        }

        // Keep your existing method for backward compatibility
        public async Task PublishAsync(string topicName, string eventType, string eventData, CancellationToken cancellationToken = default)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["EventId"] = Guid.NewGuid().ToString(),
                ["OccurredOn"] = DateTime.UtcNow,
                ["Source"] = "CoreBanking"
            };

            await _serviceBusSender.SendMessageAsync(topicName, eventData, properties, cancellationToken);
            _logger.LogDebug("Published custom event {EventType} to {Topic}", eventType, topicName);
        }

        // NEW: Batch publishing for better performance
        public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> domainEvents, CancellationToken cancellationToken = default)
            where TEvent : IDomainEvent
        {
            var eventsByTopic = domainEvents.GroupBy(e => GetTopicNameForEvent(e.GetType()));

            foreach (var topicGroup in eventsByTopic)
            {
                var topicName = topicGroup.Key;
                var eventsList = topicGroup.ToList();

                // For small batches, use individual sends
                if (eventsList.Count <= 10)
                {
                    var tasks = eventsList.Select(e => PublishAsync(e, cancellationToken));
                    await Task.WhenAll(tasks);
                }
                else
                {
                    // For larger batches, you might need to implement batch sending
                    // This depends on your IServiceBusSender implementation
                    _logger.LogWarning("Large batch detected ({Count} events). Consider implementing batch send in IServiceBusSender.",
                        eventsList.Count);

                    // Fallback to individual sends
                    foreach (var domainEvent in eventsList)
                    {
                        await PublishAsync(domainEvent, cancellationToken);
                    }
                }
            }
        }

        private (string eventData, Dictionary<string, object> properties) CreateMessageData<TEvent>(
            TEvent domainEvent, string eventType) where TEvent : IDomainEvent
        {
            var eventData = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["EventId"] = domainEvent.EventId.ToString(),
                ["OccurredOn"] = domainEvent.OccurredOn,
                ["Source"] = "CoreBanking",
                ["Version"] = "1.0",
                ["AggregateType"] = GetAggregateType(domainEvent),
                ["AggregateId"] = GetAggregateId(domainEvent),
                ["TimeToLive"] = GetTimeToLive(eventType).ToString() // Add as property since your sender might handle TTL
            };

            return (eventData, properties);
        }

        private TimeSpan GetTimeToLive(string eventType)
        {
            return eventType switch
            {
                var et when et.Contains("Transaction") => TimeSpan.FromDays(30),
                var et when et.Contains("Account") => TimeSpan.FromDays(90),
                var et when et.Contains("Customer") => TimeSpan.FromDays(365),
                _ => TimeSpan.FromDays(7)
            };
        }

        private bool IsTransientException(Exception ex)
        {
            // Common transient exceptions in messaging
            return ex is TimeoutException ||
                   ex is OperationCanceledException ||
                   (ex is InvalidOperationException && ex.Message.Contains("timeout")) ||
                   (ex.Message.Contains("retry", StringComparison.OrdinalIgnoreCase));
        }

        private string GetTopicNameForEvent(Type eventType)
        {
            // Use your existing logic but with configurable topic names
            if (eventType.Name.Contains("Customer")) return _config.CustomerTopicName;
            if (eventType.Name.Contains("Account")) return _config.AccountTopicName;
            if (eventType.Name.Contains("Transaction")) return _config.TransactionTopicName;
            return "general-events";
        }

        private string GetAggregateType<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            return domainEvent.GetType().Name.Replace("Event", "");
        }

        private string GetAggregateId<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            // Implement based on your domain event structure
            // For now, use a placeholder or extract from event
            return domainEvent.EventId.ToString();
        }
    }
}
