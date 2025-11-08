using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CoreBankingTest.API.Hubs
{
    public class TransactionHub : Hub
    {
        private readonly ILogger<TransactionHub> _logger;
        private readonly IMediator _mediator;

        public TransactionHub(ILogger<TransactionHub> logger, IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        // Client can request real-time transaction feed
        public async Task RequestTransactionFeed(string accountNumber, int? maxTransactions = null)
        {
            try
            {
                var query = new GetTransactionHistoryQuery
                {
                    AccountNumber = AccountNumber.Create(accountNumber),
                    PageSize = maxTransactions ?? 50
                };

                var result = await _mediator.Send(query);

                if (result.IsSuccess)
                {
                    await Clients.Caller.SendAsync("TransactionFeed", new
                    {
                        Success = true,
                        AccountNumber = accountNumber,
                        Transactions = result.Data!.Transactions,
                        LastUpdated = DateTime.UtcNow
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("TransactionFeedError",
                        $"Failed to load transactions: {string.Join(", ", result.Errors)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction feed for {AccountNumber}", accountNumber);
                await Clients.Caller.SendAsync("TransactionFeedError", "Internal server error");
            }
        }

        // Admin method to broadcast system alerts
        public async Task BroadcastSystemAlert(string message, string severity = "info")
        {
            // In production, you'd check user roles/permissions here
            _logger.LogInformation("Broadcasting system alert: {Message} (Severity: {Severity})",
                message, severity);

            await Clients.All.SendAsync("SystemAlert", new
            {
                Message = message,
                Severity = severity,
                Timestamp = DateTime.UtcNow,
                AlertId = Guid.NewGuid()
            });
        }
    }
}
