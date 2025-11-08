using Corebanking;
using Grpc.Core;
using MediatR;

namespace CoreBankingTest.API.gRPC.Services
{
    public class TradingGrpcService : EnhancedAccountService.EnhancedAccountServiceBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TradingGrpcService> _logger;

        public TradingGrpcService(IMediator mediator, ILogger<TradingGrpcService> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public override async Task LiveTrading(IAsyncStreamReader<TradingOrder> requestStream,
            IServerStreamWriter<TradingExecution> responseStream, ServerCallContext context)
        {
            var sessionId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting trading session {SessionId}", sessionId);

            try
            {
                // Handle incoming orders
                var readTask = Task.Run(async () =>
                {
                    await foreach (var order in requestStream.ReadAllAsync(context.CancellationToken))
                    {
                        _logger.LogInformation("Received trading order: {OrderId} for {Symbol}",
                            order.OrderId, order.Symbol);

                        // Process order (simulated)
                        await ProcessTradingOrder(order, responseStream, context.CancellationToken);
                    }
                });

                // Send market data updates
                var marketDataTask = Task.Run(async () =>
                {
                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var marketUpdate = GenerateMarketDataUpdate();
                            await responseStream.WriteAsync(marketUpdate, context.CancellationToken);
                            await Task.Delay(1000, context.CancellationToken); // 1 second updates
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });

                await Task.WhenAny(readTask, marketDataTask);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in trading session {SessionId}", sessionId);
                throw new RpcException(new Status(StatusCode.Internal, "Trading session error"));
            }
            finally
            {
                _logger.LogInformation("Ended trading session {SessionId}", sessionId);
            }
        }

        private async Task ProcessTradingOrder(TradingOrder order,
            IServerStreamWriter<TradingExecution> responseStream, CancellationToken cancellationToken)
        {
            try
            {
                // Simulate order processing delay
                await Task.Delay(500, cancellationToken);

                var execution = new TradingExecution
                {
                    ExecutionId = Guid.NewGuid().ToString(),
                    OrderId = order.OrderId,
                    Symbol = order.Symbol,
                    Quantity = order.Quantity,
                    Price = order.Price * 1.001, // Simulate slight price improvement
                    Status = "Filled",
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
                };

                await responseStream.WriteAsync(execution, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing trading order {OrderId}", order.OrderId);

                var failedExecution = new TradingExecution
                {
                    ExecutionId = Guid.NewGuid().ToString(),
                    OrderId = order.OrderId,
                    Symbol = order.Symbol,
                    Status = "Failed",
                    ErrorMessage = "Order processing failed",
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
                };

                await responseStream.WriteAsync(failedExecution, cancellationToken);
            }
        }

        private TradingExecution GenerateMarketDataUpdate()
        {
            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN" };
            var random = new Random();
            var symbol = symbols[random.Next(symbols.Length)];

            return new TradingExecution
            {
                ExecutionId = Guid.NewGuid().ToString(),
                Symbol = symbol,
                Price = 100 + random.NextDouble() * 100,
                Status = "MarketData",
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
            };
        }
    }
}
