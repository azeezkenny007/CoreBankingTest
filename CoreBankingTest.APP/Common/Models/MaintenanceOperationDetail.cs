using CoreBankingTest.CORE.ValueObjects;

namespace CoreBanking.Application.Common.Models;

public class MaintenanceOperationDetail
{
    public AccountId AccountId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static MaintenanceOperationDetail Success(AccountId accountId, string operationType)
    {
        return new MaintenanceOperationDetail
        {
            AccountId = accountId,
            OperationType = operationType,
            IsSuccess = true,
            Timestamp = DateTime.UtcNow
        };
    }

    public static MaintenanceOperationDetail Failure(AccountId accountId, string operationType, string errorMessage)
    {
        return new MaintenanceOperationDetail
        {
            AccountId = accountId,
            OperationType = operationType,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };
    }
}
