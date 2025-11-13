namespace CoreBanking.Application.Common.Models;

public class AccountMaintenanceResult
{
    public int ProcessedCount { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public List<MaintenanceOperationDetail> OperationDetails { get; set; } = new();
    public bool IsSuccess => FailedOperations == 0;

    public static AccountMaintenanceResult Success(int processed, int successful)
    {
        return new AccountMaintenanceResult
        {
            ProcessedCount = processed,
            SuccessfulOperations = successful,
            FailedOperations = 0
        };
    }

    public static AccountMaintenanceResult Failure(int processed, int successful, int failed)
    {
        return new AccountMaintenanceResult
        {
            ProcessedCount = processed,
            SuccessfulOperations = successful,
            FailedOperations = failed
        };
    }
}
