using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.External.DTOs;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.External.Resilience
{
    public class ResilienceService : IResilienceService
    {
        private readonly AdvancedPollyPolicies _policyFactory;
        private readonly ILogger<ResilienceService> _logger;

        public ResilienceService(
            AdvancedPollyPolicies policyFactory,
            ILogger<ResilienceService> logger)
        {
            _policyFactory = policyFactory;
            _logger = logger;
        }

        public async Task<T> ExecuteWithResilienceAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            var policy = _policyFactory.CreateGenericResiliencePipeline<T>();
            var context = new Context(operationName);

            try
            {
                return await policy.ExecuteAsync(
                    async (ctx, ct) =>
                    {
                        _logger.LogInformation("Executing {OperationName} with resilience", operationName);
                        return await operation(ct);
                    },
                    context,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resilience policy failed for {OperationName}", operationName);
                throw;
            }
        }

        public async Task<HttpResponseMessage> ExecuteHttpCallWithResilienceAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> httpOperation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            var policy = _policyFactory.CreateHttpResiliencePipeline();
            var context = new Context(operationName);

            try
            {
                return await policy.ExecuteAsync(
                    async (ctx, ct) =>
                    {
                        _logger.LogInformation("Executing HTTP operation {OperationName} with resilience", operationName);
                        return await httpOperation(ct);
                    },
                    context,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP resilience policy failed for {OperationName}", operationName);
                throw;
            }
        }

        // You can keep the specialized methods as convenience methods, 
        // but they're not part of the interface
        public async Task<SimulatedCreditScoreResponse> GetCreditScoreWithResilienceAsync(
            Func<CancellationToken, Task<SimulatedCreditScoreResponse>> creditScoreOperation,
            string bvn,
            CancellationToken cancellationToken = default)
        {
            var operationName = $"CreditScoreLookup-{bvn}";
            return await ExecuteWithResilienceAsync(creditScoreOperation, operationName, cancellationToken);
        }

        public async Task<SimulatedBVNResponse> ValidateBVNWithResilienceAsync(
            Func<CancellationToken, Task<SimulatedBVNResponse>> bvnValidationOperation,
            string bvn,
            CancellationToken cancellationToken = default)
        {
            var operationName = $"BVNValidation-{bvn}";
            return await ExecuteWithResilienceAsync(bvnValidationOperation, operationName, cancellationToken);
        }
    }
}
