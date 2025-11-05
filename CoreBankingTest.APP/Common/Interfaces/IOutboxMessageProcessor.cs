using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Interfaces
{
    public interface IOutboxMessageProcessor
    {
        Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default);
    }
}
