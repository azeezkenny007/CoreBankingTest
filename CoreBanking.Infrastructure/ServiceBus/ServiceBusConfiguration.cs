using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{




    public class ServiceBusConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string CustomerTopicName { get; set; } = "customer-events";
        public string AccountTopicName { get; set; } = "account-events";
        public string TransactionTopicName { get; set; } = "transaction-events";
        public string AccountQueueName { get; set; } = "account-commands";
        public int MaxRetries { get; set; } = 5;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);


        // Queues for commands
        public string AccountCommandQueue { get; set; } = "account-commands";
        public string TransactionCommandQueue { get; set; } = "transaction-commands";

        // Subscriptions for topics
        public Dictionary<string, string[]> TopicSubscriptions { get; set; } = new()
        {
            ["customer-events"] = new[] { "notifications", "reporting", "compliance" },
            ["account-events"] = new[] { "notifications", "analytics", "fraud-detection" },
            ["transaction-events"] = new[] { "notifications", "fraud-detection", "reporting", "analytics" }
        };

        public int PrefetchCount { get; set; } = 10;
        public int MaxConcurrentCalls { get; set; } = 5;
    }


    // CoreBanking.Infrastructure/ServiceBus/ServiceBusAdministration.cs
    //public class ServiceBusAdministration
    //{
    //    private readonly ServiceBusAdministrationClient _adminClient;
    //    private readonly ServiceBusConfiguration _config;
    //    private readonly ILogger<ServiceBusAdministration> _logger;

    //    public ServiceBusAdministration(string connectionString, ServiceBusConfiguration config, ILogger<ServiceBusAdministration> logger)
    //    {
    //        _adminClient = new ServiceBusAdministrationClient(connectionString);
    //        _config = config;
    //        _logger = logger;
    //    }

    //    public async Task EnsureInfrastructureExistsAsync()
    //    {
    //        await EnsureTopicsExistAsync();
    //        await EnsureQueuesExistAsync();
    //        await EnsureSubscriptionsExistAsync();
    //    }

    //    private async Task EnsureTopicsExistAsync()
    //    {
    //        var topics = new[] { _config.CustomerTopicName, _config.AccountTopicName, _config.TransactionTopicName };

    //        foreach (var topicName in topics)
    //        {
    //            if (!await _adminClient.TopicExistsAsync(topicName))
    //            {
    //                await _adminClient.CreateTopicAsync(new CreateTopicOptions(topicName)
    //                {
    //                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
    //                    DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
    //                    EnableBatchedOperations = true,
    //                    MaxSizeInMegabytes = 1024
    //                });
    //                _logger.LogInformation("Created topic: {TopicName}", topicName);
    //            }
    //        }
    //    }

    //    private async Task EnsureQueuesExistAsync()
    //    {
    //        var queues = new[] { _config.AccountCommandQueue, _config.TransactionCommandQueue };

    //        foreach (var queueName in queues)
    //        {
    //            if (!await _adminClient.QueueExistsAsync(queueName))
    //            {
    //                await _adminClient.CreateQueueAsync(new CreateQueueOptions(queueName)
    //                {
    //                    DefaultMessageTimeToLive = TimeSpan.FromDays(2),
    //                    DeadLetteringOnMessageExpiration = true,
    //                    DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(5),
    //                    LockDuration = TimeSpan.FromMinutes(1),
    //                    MaxDeliveryCount = 5
    //                });
    //                _logger.LogInformation("Created queue: {QueueName}", queueName);
    //            }
    //        }
    //    }

    //    private async Task EnsureSubscriptionsExistAsync()
    //    {
    //        foreach (var (topicName, subscriptions) in _config.TopicSubscriptions)
    //        {
    //            foreach (var subscriptionName in subscriptions)
    //            {
    //                if (!await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName))
    //                {
    //                    await _adminClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, subscriptionName)
    //                    {
    //                        DefaultMessageTimeToLive = TimeSpan.FromDays(1),
    //                        MaxDeliveryCount = 3,
    //                        EnableDeadLetteringOnFilterEvaluationExceptions = true
    //                    });
    //                    _logger.LogInformation("Created subscription {Subscription} for topic {Topic}",
    //                        subscriptionName, topicName);
    //                }
    //            }
    //        }
    //    }
    //}
}
