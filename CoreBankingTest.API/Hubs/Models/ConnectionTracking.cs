using CoreBankingTest.CORE.Enums;
using System.Data;

namespace CoreBankingTest.API.Hubs.Models
{

    public class ConnectionTracking
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public DateTime EstablishedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public ConnectionStatus Status { get; set; }
        public DateTime? TerminatedAt { get; set; }
        public int MessagesSent { get; set; }
        public int MessagesReceived { get; set; }
    }
}
