using Polly;

namespace CoreBankingTest.API.Extensions
{
    public static class PollyContextExtensions
    {
        public static ILogger? GetLogger(this Context context)
        {
            return context.TryGetValue("Logger", out var logger) ? logger as ILogger : null;
        }
    }
}
