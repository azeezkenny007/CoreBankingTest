namespace CoreBankingTest.API.Models.Requests
{
    public record CreateAccountRequest
    {
        public Guid CustomerId { get; init; }
        public string AccountType { get; init; } = string.Empty;
        public decimal InitialDeposit { get; init; }
        public string Currency { get; init; } = "NGN";
    }
}
