using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Models
{
    public class DeadLetterMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public string DeadLetterReason { get; set; } = string.Empty;
        public string DeadLetterErrorDescription { get; set; } = string.Empty;
        public DateTimeOffset EnqueuedTime { get; set; }
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, string> Properties { get; set; } = new();
        public int DeliveryCount { get; set; }
    }
}
