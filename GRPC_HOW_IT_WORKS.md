# gRPC - Step-by-Step: How It Actually Works

## Simple Example: User Calls GetAccount

Let's trace a real request from start to finish.

---

## STEP 1: Client Wants Account Info

**What the client does:**

```csharp
// Client code (could be .NET, Python, Node.js, etc.)

// Create connection to server
var channel = GrpcChannel.ForAddress("https://localhost:7288");

// Create client from generated code
var client = new AccountService.AccountServiceClient(channel);

// Create request
var request = new GetAccountRequest 
{ 
    AccountNumber = "1234567890" 
};

// Send request
var response = await client.GetAccountAsync(request);

// Use response
Console.WriteLine($"Balance: {response.Balance}");
Console.WriteLine($"Account Type: {response.AccountType}");
```

**What happens:**
- Client creates a **GetAccountRequest** object
- Sets `AccountNumber = "1234567890"`

---

## STEP 2: Serialize to Binary

**What gRPC does (automatic):**

```
GetAccountRequest object
{
    AccountNumber = "1234567890"
}
        ↓
   Serialize (Protocol Buffer)
        ↓
Binary data: [0x0A, 0x0A, 0x31, 0x32, 0x33, ...]
```

**Why binary?**
- Smaller than JSON
- Faster to encode/decode
- Binary format defined in `.proto` file

**Example .proto definition:**
```protobuf
message GetAccountRequest {
    string account_number = 1;  // Field 1 = account_number
}
```

---

## STEP 3: Send Over HTTP/2

**What happens:**

```
Binary data
    ↓
HTTP/2 Request
    ├─ Method: corebanking.AccountService/GetAccount
    ├─ Header: content-type: application/grpc
    ├─ Body: [binary data]
    └─ Port: 7288
    ↓
Network
    ↓
Server (Kestrel on port 7288)
```

---

## STEP 4: Server Receives Request

**In Program.cs setup:**

```csharp
// Server is listening on port 7288
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7288, o =>
    {
        o.UseHttps();
        o.Protocols = HttpProtocols.Http2;  // ← gRPC requires HTTP/2
    });
});

// gRPC services registered
app.MapGrpcService<AccountGrpcService>();
```

**Server routing:**
```
HTTP/2 request arrives at port 7288
    ↓
Kestrel sees: corebanking.AccountService/GetAccount
    ↓
Route to AccountGrpcService.GetAccount() method
```

---

## STEP 5: Exception Interceptor (Error Handling)

**The interceptor wraps the call:**

```csharp
public class ExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            _logger.LogInformation("Calling: {Method}", context.Method);
            
            // Call the actual service method
            return await continuation(request, context);
            
            // If no error, response returns here
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Method}", context.Method);
            
            // Convert domain exceptions to gRPC errors
            var status = ex switch
            {
                ArgumentException => new Status(StatusCode.InvalidArgument, ex.Message),
                KeyNotFoundException => new Status(StatusCode.NotFound, ex.Message),
                _ => new Status(StatusCode.Internal, "Server error")
            };
            
            throw new RpcException(status);
        }
    }
}
```

**Flow:**
```
Request arrives
    ↓
ExceptionInterceptor.UnaryServerHandler()
    ├─ Log: "Calling: corebanking.AccountService/GetAccount"
    ├─ Call: continuation() → actual service method
    └─ If error → catch → convert to RpcException
```

---

## STEP 6: Service Method Executes

**AccountGrpcService.cs:**

```csharp
public class AccountGrpcService : AccountService.AccountServiceBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    
    // This method is called
    public override async Task<AccountResponse> GetAccount(
        GetAccountRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetAccount called for {AccountNumber}", 
            request.AccountNumber);
        
        // STEP 6.1: Validate
        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            throw new RpcException(new Status(StatusCode.InvalidArgument, 
                "Account number is required"));
        
        // STEP 6.2: Create domain value object
        var accountNumber = AccountNumber.Create(request.AccountNumber);
        
        // STEP 6.3: Create MediatR query
        var query = new GetAccountDetailsQuery 
        { 
            AccountNumber = accountNumber 
        };
        
        // STEP 6.4: Send to MediatR
        var result = await _mediator.Send(query);
        
        // STEP 6.5: Check result
        if (result.IsSuccess)
        {
            // STEP 6.6: Map domain model to gRPC response
            return _mapper.Map<AccountResponse>(result.Data);
        }
        else
        {
            throw new RpcException(new Status(StatusCode.NotFound, 
                string.Join("; ", result.Errors)));
        }
    }
}
```

**This is the bridge between gRPC and your CQRS system!**

---

## STEP 7: Call MediatR (CQRS Pipeline)

**Inside gRPC service, we send a query:**

```csharp
var query = new GetAccountDetailsQuery { AccountNumber = accountNumber };
var result = await _mediator.Send(query);
```

**This triggers your entire CQRS pipeline:**

```
MediatR.Send(query)
    ↓
1. ValidationBehavior
   └─ Validate query inputs ✓
    ↓
2. LoggingBehavior
   └─ Log: "Handling GetAccountDetailsQuery"
    ↓
3. GetAccountDetailsQueryHandler (your code)
   ├─ Load account from repository
   ├─ Map to DTO
   └─ Return Result<AccountDetailsDto>
    ↓
4. DomainEventsBehavior
   └─ (No events for queries)
    ↓
5. LoggingBehavior
   └─ Log: "GetAccountDetailsQuery completed"
    ↓
Return: Result<AccountDetailsDto>
```

**Inside the handler:**
```csharp
public class GetAccountDetailsQueryHandler : 
    IRequestHandler<GetAccountDetailsQuery, Result<AccountDetailsDto>>
{
    public async Task<Result<AccountDetailsDto>> Handle(
        GetAccountDetailsQuery request,
        CancellationToken cancellationToken)
    {
        // Query the database
        var account = await _accountRepository.GetByAccountNumberAsync(
            request.AccountNumber);
        
        if (account == null)
            return Result<AccountDetailsDto>.Failure("Account not found");
        
        // Map entity to DTO
        var dto = _mapper.Map<AccountDetailsDto>(account);
        
        return Result<AccountDetailsDto>.Success(dto);
    }
}
```

---

## STEP 8: Map Domain Model to gRPC Response

**The result comes back from CQRS:**

```csharp
// result.Data is AccountDetailsDto with:
// {
//    AccountNumber: "1234567890",
//    AccountType: "Checking",
//    Balance: 5000.00,
//    Currency: "NGN"
// }

// Back in gRPC service:
return _mapper.Map<AccountResponse>(result.Data);
```

**AutoMapper converts:**

```
AccountDetailsDto (Domain)
├─ AccountNumber (string)
├─ AccountType (AccountType enum)
├─ Balance (decimal)
└─ Currency (string)
        ↓
    AutoMapper
        ↓
AccountResponse (gRPC)
├─ account_number (string)
├─ account_type (string)
├─ balance (double)
└─ currency (string)
```

**AccountGrpcProfile.cs (mapping configuration):**
```csharp
public class AccountGrpcProfile : Profile
{
    public AccountGrpcProfile()
    {
        CreateMap<AccountDetailsDto, AccountResponse>()
            .ForMember(dest => dest.AccountNumber,
                opt => opt.MapFrom(src => src.AccountNumber))
            .ForMember(dest => dest.Balance,
                opt => opt.MapFrom(src => (double)src.Balance));
    }
}
```

---

## STEP 9: Return Response to Interceptor

**Interceptor receives response:**

```csharp
// In ExceptionInterceptor
return await continuation(request, context);  // ← Gets response here

// AccountResponse object returned:
// {
//    account_number: "1234567890",
//    account_type: "Checking",
//    balance: 5000.0,
//    currency: "NGN",
//    customer_name: "John Doe",
//    date_opened: ...,
//    is_active: true
// }
```

**Interceptor logs success:**
```csharp
// No exception, so finally block logs:
_logger.LogInformation("Request completed: {Method}", context.Method);
```

---

## STEP 10: Serialize Response to Binary

**Before sending back to client:**

```
AccountResponse object
{
    account_number: "1234567890",
    account_type: "Checking",
    balance: 5000.0,
    currency: "NGN"
}
        ↓
   Serialize (Protocol Buffer)
        ↓
Binary data: [0x0A, 0x0A, 0x31, 0x32, 0x33, ...]
```

---

## STEP 11: Send Response Over HTTP/2

**Server sends back:**

```
HTTP/2 Response
├─ Status: 200 OK (or error code)
├─ Header: content-type: application/grpc
├─ Trailer: grpc-status: 0 (success)
├─ Body: [binary data of AccountResponse]
└─ Port: 7288
    ↓
Network
    ↓
Client receives
```

---

## STEP 12: Client Receives & Deserializes

**Client code:**

```csharp
var response = await client.GetAccountAsync(request);
//              ↑
//     This completes here

// response is now an AccountResponse object with:
// {
//    AccountNumber = "1234567890",
//    AccountType = "Checking",
//    Balance = 5000.0,
//    Currency = "NGN",
//    CustomerName = "John Doe",
//    IsActive = true
// }

// Use the data
Console.WriteLine($"Balance: {response.Balance}");  // Output: 5000
Console.WriteLine($"Account Type: {response.AccountType}");  // Output: Checking
```

---

## Complete Timeline

```
Time    Event
────────────────────────────────────────────────────
T0      Client calls: client.GetAccountAsync(request)
        
T1      Binary serialize: GetAccountRequest → bytes
        
T2      HTTP/2 send to port 7288
        
T3      Server receives, routes to AccountGrpcService
        
T4      ExceptionInterceptor.UnaryServerHandler() starts
        
T5      Validate input ✓
        
T6      Create domain query
        
T7      _mediator.Send(query) → CQRS pipeline
        
T8      ValidationBehavior validates
        
T9      GetAccountDetailsQueryHandler executes
        
T10     Query database
        
T11     Map DTO
        
T12     Return Result<AccountDetailsDto>
        
T13     Back in gRPC service
        
T14     AutoMapper: AccountDetailsDto → AccountResponse
        
T15     Return AccountResponse from GetAccount()
        
T16     ExceptionInterceptor catches response (no error)
        
T17     Binary serialize: AccountResponse → bytes
        
T18     HTTP/2 send response to client
        
T19     Client receives, deserializes bytes → AccountResponse
        
T20     Client uses response.Balance
```

Total time: **~50-200ms** (depending on database query)

---

## Request/Response Format Example

### **Request (Sent by Client)**

```
Human readable:
{
    "account_number": "1234567890"
}

Protocol Buffer (binary):
0x0A 0x0A 0x31 0x32 0x33 0x34 0x35 0x36 
0x37 0x38 0x39 0x30

Size: ~12 bytes (vs JSON: ~30 bytes)
```

### **Response (Sent by Server)**

```
Human readable:
{
    "account_number": "1234567890",
    "account_type": "Checking",
    "balance": 5000.0,
    "currency": "NGN",
    "customer_name": "John Doe",
    "date_opened": {...},
    "is_active": true
}

Protocol Buffer (binary):
0x0A 0x0A 0x31 0x32 0x33 0x34... (compact binary)

Size: ~100 bytes (vs JSON: ~200+ bytes)
```

---

## Error Handling Example

### **If Account Not Found**

```csharp
// In handler
if (account == null)
    return Result<AccountDetailsDto>.Failure("Account not found");

// Back in gRPC service
if (!result.IsSuccess)
{
    throw new RpcException(new Status(StatusCode.NotFound, 
        string.Join("; ", result.Errors)));
}

// ExceptionInterceptor catches it? No, already RpcException
// Send to client as gRPC error

// Client receives:
// RpcException with StatusCode.NotFound
// Message: "Account not found"
```

**Client handles error:**
```csharp
try
{
    var response = await client.GetAccountAsync(request);
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
{
    Console.WriteLine("Account not found: " + ex.Status.Detail);
}
```

---

## What Makes It Fast?

### **Binary Protocol (Not JSON)**

```
REST (JSON):
POST /api/accounts/details
Content-Type: application/json
Accept-Encoding: gzip, deflate

{
    "account_number": "1234567890"
}

Size: ~80 bytes

---

gRPC (Protocol Buffer):
POST /corebanking.AccountService/GetAccount
Content-Type: application/grpc

[binary: 0x0A 0x0A 0x31 0x32...]

Size: ~12 bytes
```

### **HTTP/2 (Multiplexing)**

```
REST (HTTP/1.1):
Request 1 ───→
            ←─── Response 1
                          Request 2 ───→
                                      ←─── Response 2

gRPC (HTTP/2):
Request 1 ───→
Request 2 ───→  (Sent simultaneously!)
Response 1 ←───
Response 2 ←───  (Received simultaneously!)
```

---

## Summary: The Full Picture

```
1. CLIENT                      → Creates GetAccountRequest
                                  Serializes to binary
                                  Sends over HTTP/2

2. SERVER (Kestrel)            ← Receives on port 7288
                                  Routes to AccountGrpcService

3. INTERCEPTOR                 ← Wraps for error handling

4. gRPC SERVICE                ← Validates input
                                  Creates domain query
                                  Calls MediatR

5. CQRS PIPELINE               ← Validates
                                  Routes to handler
                                  Executes query
                                  Logs

6. HANDLER                     ← Queries database
                                  Maps to DTO
                                  Returns result

7. gRPC SERVICE                ← Maps DTO to response
                                  Returns AccountResponse

8. INTERCEPTOR                 ← Logs success
                                  Serializes to binary
                                  Sends over HTTP/2

9. CLIENT                      ← Receives response
                                  Deserializes from binary
                                  Uses data
```

**Everything is wired together: gRPC + CQRS + Clean Architecture!**

