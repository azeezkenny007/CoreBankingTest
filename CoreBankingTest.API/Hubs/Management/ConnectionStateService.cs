using CoreBankingTest.API.Hubs.Models;
using CoreBankingTest.CORE.Enums;
using System.Collections.Concurrent;

namespace CoreBankingTest.API.Hubs.Management
{
    public class ConnectionStateService
    {
        private readonly ConcurrentDictionary<string, ConnectionTracking> _connections = new();
        private readonly ILogger<ConnectionStateService> _logger;
        private readonly Timer _healthTimer;

        public ConnectionStateService(ILogger<ConnectionStateService> logger)
        {
            _logger = logger;
            _healthTimer = new Timer(CheckConnectionHealth, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void ConnectionEstablished(string connectionId, string accountNumber, string? userId)
        {
            var state = new ConnectionTracking
            {
                ConnectionId = connectionId,
                AccountNumber = accountNumber,
                UserId = userId,
                EstablishedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                Status = ConnectionStatus.Connected
            };

            _connections[connectionId] = state;
            _logger.LogInformation("Connection established: {ConnectionId} for account {AccountNumber}",
                connectionId, accountNumber);
        }

        public void ConnectionActivity(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var state))
            {
                state.LastActivity = DateTime.UtcNow;
                state.MessagesSent++;
            }
        }

        public void ConnectionTerminated(string connectionId, ConnectionStatus status = ConnectionStatus.Disconnected)
        {
            if (_connections.TryRemove(connectionId, out var state))
            {
                state.Status = status;
                state.TerminatedAt = DateTime.UtcNow;

                _logger.LogInformation("Connection terminated: {ConnectionId} with status {Status}",
                    connectionId, status);
            }
        }

        public IEnumerable<ConnectionTracking> GetActiveConnections() => _connections.Values;

        public IEnumerable<ConnectionTracking> GetConnectionsForAccount(string accountNumber) =>
            _connections.Values.Where(c => c.AccountNumber == accountNumber);

        private void CheckConnectionHealth(object? state)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-2); // 2 minutes inactivity threshold

            var staleConnections = _connections.Values
                .Where(c => c.LastActivity < cutoff && c.Status == ConnectionStatus.Connected)
                .ToList();

            foreach (var connection in staleConnections)
            {
                _logger.LogWarning("Marking stale connection {ConnectionId} as inactive", connection.ConnectionId);
                connection.Status = ConnectionStatus.Inactive;
            }
        }
    }
}
