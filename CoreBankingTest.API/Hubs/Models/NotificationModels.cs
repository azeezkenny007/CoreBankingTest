namespace CoreBankingTest.API.Hubs.Models
{
    public record TransactionNotification
    {
        public string TransactionId { get; init; } = string.Empty;
        public string AccountNumber { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public decimal RunningBalance { get; init; }
    }

    public record BalanceUpdate
    {
        public string AccountNumber { get; init; } = string.Empty;
        public decimal NewBalance { get; init; }
        public string Currency { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; }
    }

    public record SystemAlert
    {
        public string AlertId { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty; // info, warning, error
        public DateTime Timestamp { get; init; }
    }

    public record FraudAlert
    {
        public string AlertId { get; init; } = string.Empty;
        public string AccountNumber { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public DateTime DetectedAt { get; init; }
    }

    public record ConnectionState
    {
        public bool IsConnected { get; init; }
        public DateTime StateChangedAt { get; init; }
        public string? Message { get; init; }
    }
}
