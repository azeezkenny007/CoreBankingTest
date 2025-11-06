using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using CoreBankingTest.API.gRPC;
using Grpc.Core;
using Corebanking;


namespace CoreBankingTest.GrpcClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting gRPC LiveTrading test...");

            // Adjust the address if needed
            var channel = GrpcChannel.ForAddress("https://localhost:7288");
            var client = new EnhancedAccountService.EnhancedAccountServiceClient(channel);

            using var call = client.LiveTrading();

            // Task to read server responses as they stream in
            var responseReaderTask = Task.Run(async () =>
            {
                await foreach (var execution in call.ResponseStream.ReadAllAsync())
                {
                    Console.WriteLine($"Execution [{execution.ExecutionId}] - " +
                        $"{execution.Symbol} {execution.Status} @ {execution.Price}");
                }
            });

            // Send sample trading orders
            await call.RequestStream.WriteAsync(new TradingOrder
            {
                OrderId = "O-1001",
                Symbol = "AAPL",
                OrderType = "buy",
                Quantity = 10,
                Price = 150.25,
                AccountNumber = "ACC123",
                OrderTime = Timestamp.FromDateTime(DateTime.UtcNow)
            });

            await call.RequestStream.WriteAsync(new TradingOrder
            {
                OrderId = "O-1002",
                Symbol = "TSLA",
                OrderType = "sell",
                Quantity = 5,
                Price = 245.10,
                AccountNumber = "ACC123",
                OrderTime = Timestamp.FromDateTime(DateTime.UtcNow)
            });

            await call.RequestStream.CompleteAsync(); // done sending
            await responseReaderTask; // wait for responses

            Console.WriteLine("Streaming completed.");
        }
    }
}
