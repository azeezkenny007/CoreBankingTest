using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public interface IServiceBusClientFactory
    {
        ServiceBusClient CreateClient();
        ServiceBusSender CreateSender(string queueOrTopicName);
        ServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options = null);
        ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options = null);
        ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options = null);
    }
}
