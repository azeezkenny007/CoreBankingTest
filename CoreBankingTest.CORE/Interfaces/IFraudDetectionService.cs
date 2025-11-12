using CoreBankingTest.CORE.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface IFraudDetectionService
    {
        Task<FraudDetectionResult> CheckTransactionAsync(MoneyTransferredEvent transactionEvent, CancellationToken cancellationToken);
    }

    public class FraudDetectionResult
    {
        public bool IsSuspicious { get; set; }
        public decimal RiskScore { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
