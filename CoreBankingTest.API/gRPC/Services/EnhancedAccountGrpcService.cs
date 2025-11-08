using AutoMapper;
using Corebanking;
using CoreBankingTest.APP.Accounts.Commands.TransferMoney;
using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using CoreBankingTest.CORE.ValueObjects;
using Grpc.Core;
using MediatR;

namespace CoreBankingTest.API.gRPC.Services
{
    public class EnhancedAccountGrpcService : EnhancedAccountService.EnhancedAccountServiceBase
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;
        private readonly ILogger<EnhancedAccountGrpcService> _logger;

        public EnhancedAccountGrpcService(IMediator mediator, IMapper mapper,
            ILogger<EnhancedAccountGrpcService> logger)
        {
            _mediator = mediator;
            _mapper = mapper;
            _logger = logger;
        }




        // Server streaming for real-time transaction monitoring
        public override async Task StreamTransactions(global::Corebanking.StreamTransactionsRequest request,
            IServerStreamWriter<global::Corebanking.TransactionResponse> responseStream, ServerCallContext context)
        {
            _logger.LogInformation("Starting transaction stream for account {AccountNumber}",
                request.AccountNumber);

            var accountNumber = AccountNumber.Create(request.AccountNumber);
            var cancellationToken = context.CancellationToken;

            try
            {
                // Initial batch of recent transactions
                var historyQuery = new GetTransactionHistoryQuery
                {
                    AccountNumber = accountNumber,
                    PageSize = request.InitialBatchSize
                };

                var historyResult = await _mediator.Send(historyQuery, cancellationToken);

                if (historyResult.IsSuccess)
                {
                    foreach (var transaction in historyResult.Data!.Transactions)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        await responseStream.WriteAsync(_mapper.Map<global::Corebanking.TransactionResponse>(transaction),
                            cancellationToken);
                        await Task.Delay(50, cancellationToken); // Stagger sends
                    }
                }

                // Simulate real-time updates (in real system, this would connect to event stream)
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check for new transactions (polling simulation)
                        // In production, this would hook into a real event stream
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                        // For demo purposes, occasionally send a simulated transaction
                        if (DateTime.UtcNow.Second % 15 == 0) // Every 15 seconds for demo
                        {
                            var simulatedTransaction = new global::Corebanking.TransactionResponse
                            {
                                TransactionId = Guid.NewGuid().ToString(),
                                Type = "Deposit",
                                Amount = new Random().Next(100, 500),
                                Currency = "USD",
                                Description = "Simulated real-time transaction",
                                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
                            };

                            await responseStream.WriteAsync(simulatedTransaction, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in transaction stream for account {AccountNumber}",
                    request.AccountNumber);
                throw new RpcException(new Status(StatusCode.Internal, "Streaming error occurred"));
            }

            _logger.LogInformation("Transaction stream ended for account {AccountNumber}",
                request.AccountNumber);
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

        // Client streaming for batch operations
        public override async Task<global::Corebanking.BatchTransferResponse> BatchTransfer(
            IAsyncStreamReader<global::Corebanking.TransferMoneyRequest> requestStream, ServerCallContext context)
        {
            _logger.LogInformation("Starting batch transfer processing");

            var results = new List<global::Corebanking.BatchTransferResult>();
            var successfulTransfers = 0;
            var failedTransfers = 0;

            try
            {
                await foreach (var transferRequest in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    try
                    {
                        var command = new TransferMoneyCommand
                        {
                            SourceAccountNumber = AccountNumber.Create(transferRequest.SourceAccountNumber),
                            DestinationAccountNumber = AccountNumber.Create(transferRequest.DestinationAccountNumber),
                            Amount = new CORE.ValueObjects.Money((decimal)transferRequest.Amount, transferRequest.Currency),
                            Reference = transferRequest.Reference,
                            Description = transferRequest.Description
                        };

                        var result = await _mediator.Send(command, context.CancellationToken);

                        results.Add(new global::Corebanking.BatchTransferResult
                        {
                            Reference = transferRequest.Reference,
                            Success = result.IsSuccess,
                            Message = result.IsSuccess ? "Transfer completed" : string.Join("; ", result.Errors)
                        });

                        if (result.IsSuccess) successfulTransfers++;
                        else failedTransfers++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch transfer with reference {Reference}",
                            transferRequest.Reference);

                        results.Add(new global::Corebanking.BatchTransferResult
                        {
                            Reference = transferRequest.Reference,
                            Success = false,
                            Message = "Processing error"
                        });
                        failedTransfers++;
                    }
                }

                return new global::Corebanking.BatchTransferResponse
                {
                    TotalProcessed = results.Count,
                    Successful = successfulTransfers,
                    Failed = failedTransfers
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch transfer processing");
                throw new RpcException(new Status(StatusCode.Internal, "Batch processing failed"));
            }
        }
    }
}
