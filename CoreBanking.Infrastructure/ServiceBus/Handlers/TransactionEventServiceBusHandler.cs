using CoreBankingTest.CORE.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus.Handlers
{
    public class TransactionEventServiceBusHandler : BaseMessageHandler<MoneyTransferredEvent>
    {
        public TransactionEventServiceBusHandler(
            IServiceBusClientFactory clientFactory,
            ServiceBusConfiguration config,
            ILogger<TransactionEventServiceBusHandler> logger,
            IMediator mediator)
            : base(clientFactory, config.TransactionTopicName, "fraud-detection", logger, mediator)
        {
        }
    }
}
