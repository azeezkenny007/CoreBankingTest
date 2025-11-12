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
    public class CustomerEventServiceBusHandler : BaseMessageHandler<CustomerCreatedEvent>
    {
        public CustomerEventServiceBusHandler(
            IServiceBusClientFactory clientFactory,
            ServiceBusConfiguration config,
            ILogger<CustomerEventServiceBusHandler> logger,
            IMediator mediator)
            : base(clientFactory, config.CustomerTopicName, "notifications", logger, mediator)
        {
        }
        // No need to override HandleMessageAsync - base class uses MediatR
    }
}
