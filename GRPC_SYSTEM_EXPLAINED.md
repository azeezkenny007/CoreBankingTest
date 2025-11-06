# gRPC System - How It Works in Your Project

## What is gRPC?

**gRPC** = **Google Remote Procedure Call**

It's a framework for building fast, efficient APIs using:
- **Protocol Buffers** (.proto files) - Define data structure and services
- **HTTP/2** - Binary protocol (faster than REST/HTTP/1.1)
- **Any language** - gRPC works across languages

---

## REST vs gRPC

| Aspect | REST (HTTP/1.1) | gRPC (HTTP/2) |
|--------|-----------------|---------------|
| **Protocol** | Text (JSON) | Binary (Protocol Buffers) |
| **Speed** | Slower | 7-10x faster |
| **Size** | Large payloads | Compact binary |
| **Streaming** | Poll-based | Native streaming |
| **Browsers** | Works natively | Needs gRPC-web |
| **Setup** | Simple | More complex |

---

## Your Project Structure

```
CoreBankingTest.API/
├── gRPC/
│   ├── Protos/                 ← Define services & messages
│   │   ├── account.proto       ← Account service definition
│   │   ├── common.proto        ← Shared types
│   │   └── enhanced_account.proto
│   ├── Services/               ← Implement services
│   │   ├── AccountGrpcService.cs
│   │   ├── EnhancedAccountGrpcService.cs
│   │   └── TradingGrpcService.cs
│   ├── Interceptors/           ← Middleware/error handling
│   │   └── ExceptionInterceptor.cs
│   └── Mappings/               ← Proto ↔ Domain model mapping
│       └── AccountGrpcProfile.cs
└── Program.cs                  ← Configuration
```

---

## Step 1: Define Services in .proto Files

### **account.proto** - The Blueprint

```protobuf
syntax = "proto3";

package corebanking;

service AccountService {
    rpc GetAccount (GetAccountRequest) returns (AccountResponse);
    rpc CreateAccount (CreateAccountRequest) returns (CreateAccountResponse);
    rpc TransferMoney (TransferMoneyRequest) returns (TransferMoneyResponse);
    rpc GetTransactionHistory (TransactionHistoryRequest) returns (stream TransactionResponse);
}

message GetAccountRequest {
    string account_number = 1;
}

message AccountResponse {
    string account_number = 1;
    string account_type = 2;
    double balance = 3;
    string currency = 4;
}
```

**What this means:**
- `service AccountService` → Define a gRPC service
- `rpc GetAccount` → Define an RPC method
- `GetAccountRequest` → Input message
- `AccountResponse` → Output message
- `stream TransactionResponse` → Server streaming (send multiple responses)

---

## Step 2: gRPC Code Generation

When you build the project:

```csharp
// Auto-generated from account.proto
namespace CoreBankingTest.API.gRPC
{
    public abstract class AccountService.AccountServiceBase
    {
        public virtual Task<AccountResponse> GetAccount(
            GetAccountRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedError();
        }
        
        public virtual Task<CreateAccountResponse> CreateAccount(
            CreateAccountRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedError();
        }
    }
}
```

The `.csproj` file specifies this:
```xml
<ItemGroup>
    <Protobuf Include="gRPC/Protos/account.proto" GrpcServices="Server" />
</ItemGroup>
```

---

## Step 3: Implement the Service

### **AccountGrpcService.cs** - The Implementation

```csharp
public class AccountGrpcService : AccountService.AccountServiceBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    
    // Implement the RPC methods
    public override async Task<AccountResponse> GetAccount(
        GetAccountRequest request,
        ServerCallContext context)
    {
        // 1. Get account number from request
        var accountNumber = AccountNumber.Create(request.AccountNumber);
        
        // 2. Send query via MediatR (your CQRS system!)
        var query = new GetAccountDetailsQuery { AccountNumber = accountNumber };
        var result = await _mediator.Send(query);
        
        // 3. Map domain model to gRPC response
        return _mapper.Map<AccountResponse>(result.Data);
    }
    
    public override async Task<CreateAccountResponse> CreateAccount(
        CreateAccountRequest request,
        ServerCallContext context)
    {
        // 1. Parse request
        var customerId = CustomerId.Create(Guid.Parse(request.CustomerId));
        
        // 2. Create command
        var command = new CreateAccountCommand
        {
            CustomerId = customerId,
            AccountType = request.AccountType,
            InitialDeposit = (decimal)request.InitialDeposit,
            Currency = request.Currency
        };
        
        // 3. Send via MediatR
        var result = await _mediator.Send(command);
        
        // 4. Map to response
        return new CreateAccountResponse
        {
            AccountId = result.Data.ToString(),
            Message = "Account created successfully"
        };
    }
}
```

**Key Points:**
- Inherits from `AccountService.AccountServiceBase` (generated)
- Each `rpc` method becomes an `async Task` method
- Use `ServerCallContext` for context (metadata, cancellation, etc.)
- Call MediatR to execute business logic (CQRS)
- Map responses using AutoMapper

---

## Step 4: Register gRPC in Program.cs

### **Setup**

```csharp
// Configure Kestrel to listen on multiple protocols
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP/1.1 for REST/Swagger (port 5037)
    options.ListenLocalhost(5037, o =>
    {
        o.Protocols = HttpProtocols.Http1;
    });
    
    // HTTP/2 for gRPC (port 7288, HTTPS)
    options.ListenLocalhost(7288, o =>
    {
        o.UseHttps();  // Developer certificate
        o.Protocols = HttpProtocols.Http2;
    });
});

// Register gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.Interceptors.Add<ExceptionInterceptor>();  // Error handling
    options.MaxReceiveMessageSize = 16 * 1024 * 1024;  // 16MB max
    options.MaxSendMessageSize = 16 * 1024 * 1024;
});

builder.Services.AddGrpcReflection();  // For tools like grpcurl

// Map services
app.MapGrpcService<AccountGrpcService>();
app.MapGrpcService<EnhancedAccountGrpcService>();
app.MapGrpcService<TradingGrpcService>();

// Enable reflection in development
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}
```

---

## Step 5: Error Handling with Interceptors

### **ExceptionInterceptor.cs** - Middleware for gRPC

```csharp
public class ExceptionInterceptor : Interceptor
{
    private readonly ILogger<ExceptionInterceptor> _logger;
    
    // Handles unary calls (request-response)
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            // Call the service method
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;  // Already handled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gRPC call {Method}", context.Method);
            
            // Map exceptions to gRPC status codes
            var status = ex switch
            {
                ArgumentException => new Status(StatusCode.InvalidArgument, ex.Message),
                KeyNotFoundException => new Status(StatusCode.NotFound, ex.Message),
                UnauthorizedAccessException => new Status(StatusCode.PermissionDenied, ex.Message),
                _ => new Status(StatusCode.Internal, "Internal server error")
            };
            
            throw new RpcException(status);
        }
    }
    
    // Handles server streaming calls
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming call {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "Streaming error"));
        }
    }
}
```

**What it does:**
- Catches all exceptions from gRPC methods
- Maps domain exceptions to gRPC status codes
- Logs errors
- Wraps in `RpcException` to send to client

---

## Complete Flow: GetAccount Request

```
┌─────────────────────────────────┐
│  gRPC Client                    │
│  client.GetAccount(request)     │
└────────────────┬────────────────┘
                 │
        ┌────────▼────────┐
        │ Send via HTTP/2 │
        │ Binary protocol │
        └────────┬────────┘
                 │
        ┌────────▼────────────────────┐
        │   Kestrel (port 7288)       │
        │   Receive gRPC request      │
        └────────┬───────────────────┘
                 │
        ┌────────▼──────────────────────┐
        │  ExceptionInterceptor         │
        │  (Error handling middleware)  │
        └────────┬──────────────────────┘
                 │
        ┌────────▼──────────────────────┐
        │ AccountGrpcService            │
        │ GetAccount() called           │
        ├──────────────────────────────┤
        │ 1. Validate input             │
        │ 2. Create domain query        │
        │ 3. Send via MediatR           │
        │    ↓ ValidationBehavior       │
        │    ↓ Handler executes         │
        │    ↓ Query executes           │
        │    ↓ DomainEventsBehavior     │
        │ 4. Get result from CQRS       │
        │ 5. Map to AccountResponse     │
        └────────┬───────────────────────┘
                 │
        ┌────────▼────────┐
        │ Serialize to    │
        │ Protocol Buffer │
        │ (Binary)        │
        └────────┬────────┘
                 │
        ┌────────▼────────┐
        │ Send response   │
        │ via HTTP/2      │
        └────────┬────────┘
                 │
┌────────────────▼─────────────────┐
│  gRPC Client                      │
│  Receive & deserialize response   │
│  return AccountResponse           │
└───────────────────────────────────┘
```

---

## Server Streaming Example

### **.proto Definition**

```protobuf
rpc GetTransactionHistory (TransactionHistoryRequest) 
    returns (stream TransactionResponse);
    //      ↑
    //   Multiple responses
```

### **Implementation**

```csharp
public override async Task GetTransactionHistory(
    TransactionHistoryRequest request,
    IServerStreamWriter<TransactionResponse> responseStream,
    ServerCallContext context)
{
    // Get all transactions
    var transactions = await _mediator.Send(query);
    
    // Send them one by one to client
    foreach (var transaction in transactions)
    {
        var response = _mapper.Map<TransactionResponse>(transaction);
        
        // Write to stream
        await responseStream.WriteAsync(response);
        
        // Check for cancellation
        if (context.CancellationToken.IsCancellationRequested)
            break;
    }
}
```

**Client receives:**
```csharp
using var call = client.GetTransactionHistory(request);

// Receive responses one by one
await foreach (var transaction in call.ResponseStream.ReadAllAsync())
{
    Console.WriteLine(transaction);
}
```

---

## Mapping: Proto ↔ Domain Models

### **AccountGrpcProfile.cs** - AutoMapper Configuration

```csharp
public class AccountGrpcProfile : Profile
{
    public AccountGrpcProfile()
    {
        // Map domain Account → gRPC AccountResponse
        CreateMap<Account, AccountResponse>()
            .ForMember(dest => dest.AccountNumber,
                opt => opt.MapFrom(src => src.AccountNumber.Value))
            .ForMember(dest => dest.Balance,
                opt => opt.MapFrom(src => src.Balance.Amount));
        
        // Convert request to domain command
        CreateMap<CreateAccountRequest, CreateAccountCommand>();
    }
}
```

**Why mapping?**
- gRPC (Proto) ≠ Domain models
- Proto has primitives (string, double, etc.)
- Domain has Value Objects (AccountNumber, Money, etc.)
- AutoMapper handles conversions automatically

---

## gRPC Status Codes

Your interceptor uses these codes:

```csharp
var status = ex switch
{
    ArgumentException => StatusCode.InvalidArgument,  // Bad input
    KeyNotFoundException => StatusCode.NotFound,       // Not found
    UnauthorizedAccessException => StatusCode.PermissionDenied,  // Auth failed
    InvalidOperationException => StatusCode.FailedPrecondition,   // Bad state
    _ => StatusCode.Internal  // Unknown error
};
```

Standard gRPC codes:
- `OK` (0): Success
- `InvalidArgument` (3): Input validation failed
- `NotFound` (5): Resource not found
- `AlreadyExists` (6): Already exists
- `Internal` (13): Server error
- `Unavailable` (14): Service down

---

## How gRPC Integrates with Your Architecture

```
┌────────────────────────────────────────────┐
│         gRPC Client                        │
└────────────────┬───────────────────────────┘
                 │
┌────────────────▼───────────────────────────┐
│    gRPC Layer (Your API)                   │
│  - AccountGrpcService                      │
│  - ExceptionInterceptor                    │
│  - Mapping (Proto ↔ Domain)                │
└────────────────┬───────────────────────────┘
                 │
┌────────────────▼───────────────────────────┐
│    Application Layer (MediatR)             │
│  - Commands                                │
│  - Queries                                 │
│  - Validation, Logging, Events             │
└────────────────┬───────────────────────────┘
                 │
┌────────────────▼───────────────────────────┐
│    Domain Layer (Business Logic)           │
│  - Entities, Aggregates                    │
│  - Value Objects                           │
│  - Domain Events                           │
└────────────────┬───────────────────────────┘
                 │
┌────────────────▼───────────────────────────┐
│    Infrastructure Layer (Data Access)      │
│  - Repositories                            │
│  - Database                                │
│  - Outbox Pattern                          │
└────────────────────────────────────────────┘
```

---

## Benefits of gRPC in Your Project

| Benefit | Why It Matters |
|---------|----------------|
| **Speed** | HTTP/2 + binary = 7-10x faster than REST |
| **Bandwidth** | Compact messages = less network usage |
| **Streaming** | Built-in support for server/client streaming |
| **Type Safety** | Proto definitions ensure client-server compatibility |
| **Strong Typing** | Auto-generated code is strongly typed |
| **Error Handling** | Standardized status codes |
| **No Breaking Changes** | Proto supports versioning |

---

## Your gRPC Services

### **1. AccountGrpcService**

```
GetAccount(GetAccountRequest) → AccountResponse
CreateAccount(CreateAccountRequest) → CreateAccountResponse
TransferMoney(TransferMoneyRequest) → TransferMoneyResponse
GetTransactionHistory(TransactionHistoryRequest) → stream TransactionResponse
```

### **2. EnhancedAccountGrpcService**

(Similar to AccountGrpcService but with additional features)

### **3. TradingGrpcService**

(For trading/market operations)

---

## How to Call gRPC from Client

### **.NET Client**

```csharp
// Create channel (connection)
var channel = GrpcChannel.ForAddress("https://localhost:7288");

// Create client
var client = new AccountService.AccountServiceClient(channel);

// Call GetAccount
var request = new GetAccountRequest { AccountNumber = "1234567890" };
var response = await client.GetAccountAsync(request);

Console.WriteLine($"Balance: {response.Balance}");
```

### **Python Client**

```python
import grpc
from corebanking_pb2 import GetAccountRequest
from corebanking_pb2_grpc import AccountServiceStub

channel = grpc.aio.secure_channel('localhost:7288', 
    grpc.ssl_channel_credentials())
stub = AccountServiceStub(channel)

request = GetAccountRequest(account_number='1234567890')
response = await stub.GetAccount(request)
print(f"Balance: {response.balance}")
```

### **grpcurl (CLI Tool)**

```bash
# Test gRPC endpoint
grpcurl -plaintext -d '{"account_number":"1234567890"}' \
  localhost:7288 corebanking.AccountService.GetAccount
```

---

## Two Ports, Two Protocols

```
Port 5037 (HTTP/1.1):
├── REST API
├── Swagger UI
└── Controllers

Port 7288 (HTTP/2, HTTPS):
├── gRPC Services
├── AccountGrpcService
├── EnhancedAccountGrpcService
└── TradingGrpcService
```

---

## Summary

**gRPC in your project:**

1. **Define** services in `.proto` files
2. **Auto-generate** code (Server base classes, Message classes)
3. **Implement** by inheriting from generated base
4. **Call MediatR** for business logic (CQRS)
5. **Map** responses using AutoMapper
6. **Handle** errors with Interceptor
7. **Register** in Program.cs
8. **Call** from gRPC clients

**Result:** Fast, type-safe, streaming-capable API!

