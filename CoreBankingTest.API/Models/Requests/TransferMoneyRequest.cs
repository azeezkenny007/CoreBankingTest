namespace CoreBankingTest.API.Models.Requests
{
    public record TransferMoneyRequest
    {
        public string DestinationAccountNumber { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "NGN";
        public string Reference { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }
}
