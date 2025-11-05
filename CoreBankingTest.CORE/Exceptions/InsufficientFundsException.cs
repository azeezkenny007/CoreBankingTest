namespace CoreBanking.Core.Exceptions
{
    public class InsufficientFundsException : Exception
    {
        public decimal RequiredAmount { get; }
        public decimal AvailableBalance { get; }

        public InsufficientFundsException()
            : base("Insufficient funds for this operation") { }

        public InsufficientFundsException(string message)
            : base(message) { }

        public InsufficientFundsException(decimal requiredAmount, decimal availableBalance)
            : base($"Insufficient funds. Required: {requiredAmount:C}, Available: {availableBalance:C}")
        {
            RequiredAmount = requiredAmount;
            AvailableBalance = availableBalance;
        }

        public InsufficientFundsException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
