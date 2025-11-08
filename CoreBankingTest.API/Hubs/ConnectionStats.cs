namespace CoreBankingTest.API.Hubs
{
    public class ConnectionStats
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int TotalMessagesToday { get; set; }
        public TimeSpan Uptime { get; set; }
    }
}
