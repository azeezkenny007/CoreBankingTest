using CoreBankingTest.APP.External.HttpClients;
using CoreBankingTest.APP.External.Interfaces;
using Polly;
using Polly.Extensions.Http;

namespace CoreBankingTest.API.Extensions
{
    public static class HttpClientExtensions
    {
        public static IServiceCollection AddExternalHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            // Typed client for Credit Scoring Service
            services.AddHttpClient<ICreditScoringServiceClient, CreditScoringServiceClient>(client  =>
            {
                var baseUrl = configuration["ExternalServices:CreditScoring:BaseUrl"]
                    ?? "https://api.creditscoring.example.com";

                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "CoreBanking/1.0");

                // Set reasonable timeouts
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Security configurations
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // In production, implement proper certificate validation
                    return true; // For development only
                }
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            // Named client for BVN Validation Service
            services.AddHttpClient("BVNValidation", client =>
            {
                var baseUrl = configuration["ExternalServices:BVNValidation:BaseUrl"]
                    ?? "https://api.bvnvalidation.example.com";

                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("X-API-Key", configuration["ExternalServices:BVNValidation:ApiKey"] ?? string.Empty);
                client.Timeout = TimeSpan.FromSeconds(45);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogWarning("Retry {RetryCount} after {Delay}ms for {RequestUri}. Status: {StatusCode}",
                            retryCount, timespan.TotalMilliseconds,
                            outcome.Result?.RequestMessage?.RequestUri,
                            (int)(outcome.Result?.StatusCode ?? 0));
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogWarning("Circuit breaker opened for {Duration}ms", breakDelay.TotalMilliseconds);
                    },
                    onReset: (context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        // Optional: Log when circuit breaker is half-open
                    });
        }
    }
}
