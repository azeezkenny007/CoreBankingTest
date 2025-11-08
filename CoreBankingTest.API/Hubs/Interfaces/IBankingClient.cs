using CoreBankingTest.API.Hubs.Models;
using System.Data;
using ConnectionState = CoreBankingTest.API.Hubs.Models.ConnectionState;

namespace CoreBankingTest.API.Hubs.Interfaces
{
    public interface IBankingClient
    {
        // Transaction notifications
        Task ReceiveTransactionNotification(TransactionNotification notification);
        Task ReceiveBalanceUpdate(BalanceUpdate update);

        // System alerts
        Task ReceiveSystemAlert(SystemAlert alert);

        // Fraud detection
        Task ReceiveFraudAlert(FraudAlert alert);

        // Connection management
        Task ConnectionStateChanged(ConnectionState state);
    }
}
