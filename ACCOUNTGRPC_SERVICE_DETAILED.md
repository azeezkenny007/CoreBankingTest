# AccountGrpcService - Detailed Pipeline Breakdown

## What is AccountGrpcService?

It's the **bridge between gRPC (network layer) and your CQRS system (business logic)**.

It inherits from `AccountService.AccountServiceBase` (auto-generated from `.proto` file) and implements 2 methods:
1. `GetAccount()` - Retrieve account info
2. `CreateAccount()` - Create new account

---

## CLASS SETUP

```csharp
public class AccountGrpcService : AccountService.AccountServiceBase
{
    private readonly IMediator _mediator;      // ← CQRS dispatcher
    private readonly IMapper _mapper;          // ← AutoMapper (convert models)
    private readonly ILogger<AccountGrpcService> _logger;  // ← Logging
    
    public AccountGrpcService(IMediator mediator, IMapper mapper, ILogger<AccountGrpcService> logger)
    {
        _mediator = mediator;
        _mapper = mapper;
        _logger = logger;
    }
}
```

**What each dependency does:**
- `_mediator`: Routes commands/queries to handlers
- `_mapper`: Converts domain models ↔ gRPC models
- `_logger`: Logs what's happening

---

# METHOD 1: GetAccount()

## Full Code

```csharp
public override async Task<AccountResponse> GetAccount(
    GetAccountRequest request,
    ServerCallContext context)
{
    _logger.LogInformation("gRPC GetAccount called for {AccountNumber}", request.AccountNumber);
    
    // Validate input
    if (string.IsNullOrWhiteSpace(request.AccountNumber))
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Account number is required"));
    
    try
    {
        var accountNumber = AccountNumber.Create(request.AccountNumber);
        var query = new GetAccountDetailsQuery { AccountNumber = accountNumber };
        var result = await _mediator.Send(query);
        
        if (result.IsSuccess)
        {
            return _mapper.Map<AccountResponse>(result.Data);
        }
        else
        {
            throw new RpcException(new Status(StatusCode.NotFound, string.Join("; ", result.Errors)));
        }
    }
    catch (Exception ex) when (ex is not RpcException)
    {
        _logger.LogError(ex, "Error retrieving account {AccountNumber}", request.AccountNumber);
        throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
    }
}
```

---

## GetAccount Pipeline - Step by Step

### **STEP 1: Method Called**

```csharp
public override async Task<AccountResponse> GetAccount(
    GetAccountRequest request,
    ServerCallContext context)
```

**Inputs:**
- `request`: Contains `AccountNumber = "1234567890"`
- `context`: Server context (metadata, cancellation token, etc.)

**Example:**
```
Client calls: GetAccount(GetAccountRequest { AccountNumber = "1234567890" })
```

---

### **STEP 2: Log the Request**

```csharp
_logger.LogInformation("gRPC GetAccount called for {AccountNumber}", request.AccountNumber);
```

**Output in logs:**
```
[INFO] gRPC GetAccount called for 1234567890
```

---

### **STEP 3: Validate Input**

```csharp
if (string.IsNullOrWhiteSpace(request.AccountNumber))
    throw new RpcException(new Status(StatusCode.InvalidArgument, "Account number is required"));
```

**What it checks:**
- Is account number empty? 
- Is it null?
- Is it just whitespace?

**If invalid:**
```
❌ Throws RpcException
   Status: InvalidArgument (gRPC error code 3)
   Message: "Account number is required"
   → Returns error to client immediately
```

**If valid:**
```
✅ Continue to next step
```

---

### **STEP 4: Create Domain Value Object**

```csharp
var accountNumber = AccountNumber.Create(request.AccountNumber);
```

**What happens:**
- Takes the string `"1234567890"` from gRPC request
- Converts to domain `AccountNumber` value object
- The `AccountNumber.Create()` validates format (must be 10 digits)

**Example:**
```
Input: "1234567890" (gRPC string)
    ↓
AccountNumber.Create("1234567890")
    ↓
Output: AccountNumber { Value = "1234567890" } (domain object)
```

**If format invalid:**
```
❌ Throws ArgumentException
   Message: "Account number must be 10 digits"
   → Caught by catch block below
```

---

### **STEP 5: Create CQRS Query**

```csharp
var query = new GetAccountDetailsQuery { AccountNumber = accountNumber };
```

**What it is:**
- A query object (read operation)
- Part of CQRS pattern
- Will be sent to MediatR

**Example:**
```
GetAccountDetailsQuery
{
    AccountNumber = AccountNumber { Value = "1234567890" }
}
```

---

### **STEP 6: Send to MediatR**

```csharp
var result = await _mediator.Send(query);
```

**This is where the PIPELINE HAPPENS!**

```
_mediator.Send(query)
    ↓
1. ValidationBehavior
   └─ Validates query (but queries usually have minimal validation)
    ↓
2. LoggingBehavior
   └─ Logs: "Handling GetAccountDetailsQuery"
    ↓
3. GetAccountDetailsQueryHandler.Handle()
   ├─ Load account from repository
   ├─ Query database: SELECT * FROM Accounts WHERE AccountNumber = "1234567890"
   ├─ If found: Map to AccountDetailsDto
   ├─ If not found: Return Result.Failure()
   └─ Return Result<AccountDetailsDto>
    ↓
4. DomainEventsBehavior
   └─ (No events for queries, skip)
    ↓
5. LoggingBehavior
   └─ Logs: "GetAccountDetailsQuery completed"
    ↓
Return: Result<AccountDetailsDto>
```

**What `result` contains:**
```
Result<AccountDetailsDto>
{
    IsSuccess = true,
    Data = AccountDetailsDto
    {
        AccountNumber = "1234567890",
        AccountType = "Checking",
        Balance = 5000.00,
        Currency = "NGN",
        CustomerName = "John Doe",
        DateOpened = 2025-11-01,
        IsActive = true
    }
}
```

---

### **STEP 7: Check Success**

```csharp
if (result.IsSuccess)
{
    return _mapper.Map<AccountResponse>(result.Data);
}
else
{
    throw new RpcException(new Status(StatusCode.NotFound, string.Join("; ", result.Errors)));
}
```

**Two paths:**

**Path A: Success (IsSuccess = true)**
```
✅ Continue to mapping
```

**Path B: Failure (IsSuccess = false)**
```
❌ Throw RpcException
   Status: NotFound (gRPC error code 5)
   Message: error messages joined with "; "
   Example: "Account not found; Invalid account state"
   → Returns error to client
```

---

### **STEP 8: Map Domain to gRPC**

```csharp
return _mapper.Map<AccountResponse>(result.Data);
```

**What AutoMapper does:**

```
AccountDetailsDto (Domain)
{
    AccountNumber = "1234567890",
    AccountType = "Checking",
    Balance = 5000.00,
    Currency = "NGN",
    CustomerName = "John Doe",
    DateOpened = 2025-11-01,
    IsActive = true
}
        ↓
    AutoMapper mapping
        ↓
AccountResponse (gRPC)
{
    account_number = "1234567890",
    account_type = "Checking",
    balance = 5000.0,
    currency = "NGN",
    customer_name = "John Doe",
    date_opened = {timestamp},
    is_active = true
}
```

**Configuration (AccountGrpcProfile.cs):**
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

### **STEP 9: Exception Handling**

```csharp
catch (Exception ex) when (ex is not RpcException)
{
    _logger.LogError(ex, "Error retrieving account {AccountNumber}", request.AccountNumber);
    throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
}
```

**Catches any exception that's NOT already an RpcException:**

**Example errors caught:**
- `ArgumentException` - Invalid account number format
- `NullReferenceException` - Null pointer
- `InvalidOperationException` - Business logic error
- Database connection error
- Any unexpected exception

**What happens:**
```
Unexpected error occurs
    ↓
Log it: "Error retrieving account 1234567890"
    ↓
Convert to RpcException
   Status: Internal (gRPC error code 13)
   Message: "Internal server error"
    ↓
Send to client
```

---

### **STEP 10: Return Response**

```csharp
return _mapper.Map<AccountResponse>(result.Data);
```

**Returns:**
```
AccountResponse
{
    account_number = "1234567890",
    account_type = "Checking",
    balance = 5000.0,
    currency = "NGN",
    ...
}
```

**What happens next:**
- Response is serialized to binary (Protocol Buffer)
- Sent over HTTP/2 to client
- Client deserializes and uses data

---

## GetAccount Full Pipeline Visual

```
CLIENT REQUEST
    ↓
GetAccount(GetAccountRequest)
    ├─ Step 1: Method receives request
    ├─ Step 2: Log "gRPC GetAccount called for 1234567890"
    ├─ Step 3: Validate account number is not empty ✓
    ├─ Step 4: Create AccountNumber value object ✓
    ├─ Step 5: Create GetAccountDetailsQuery
    ├─ Step 6: Send to MediatR
    │          ├─ ValidationBehavior validates ✓
    │          ├─ LoggingBehavior logs start
    │          ├─ GetAccountDetailsQueryHandler
    │          │  ├─ Load account from repository
    │          │  ├─ Query database
    │          │  └─ Return Result<AccountDetailsDto>
    │          ├─ DomainEventsBehavior (skip for queries)
    │          └─ LoggingBehavior logs end
    ├─ Step 7: Check result.IsSuccess ✓
    ├─ Step 8: Map AccountDetailsDto → AccountResponse
    └─ Step 9-10: Return AccountResponse to client
         ↓
         HTTP/2 + Binary
         ↓
CLIENT RESPONSE
```

---

---

# METHOD 2: CreateAccount()

## Full Code

```csharp
public override async Task<CreateAccountResponse> CreateAccount(
    CreateAccountRequest request,
    ServerCallContext context)
{
    _logger.LogInformation("gRPC CreateAccount called for customer {CustomerId}", request.CustomerId);
    
    if (!Guid.TryParse(request.CustomerId, out var customerGuid))
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid customer ID format"));
    
    try
    {
        var command = new CreateAccountCommand
        {
            CustomerId = CustomerId.Create(customerGuid),
            AccountType = request.AccountType,
            InitialDeposit = (decimal)request.InitialDeposit,
            Currency = request.Currency
        };
        
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return new CreateAccountResponse
            {
                AccountId = result.Data.ToString(),
                AccountNumber = "",
                Message = "Account created successfully"
            };
        }
        else
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors)));
        }
    }
    catch (Exception ex) when (ex is not RpcException)
    {
        _logger.LogError(ex, "Error creating account for customer {CustomerId}", request.CustomerId);
        throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
    }
}
```

---

## CreateAccount Pipeline - Step by Step

### **STEP 1: Method Called**

```csharp
public override async Task<CreateAccountResponse> CreateAccount(
    CreateAccountRequest request,
    ServerCallContext context)
```

**Inputs:**
- `request.CustomerId`: "a1b2c3d4-1234-5678-9abc-123456789abc"
- `request.AccountType`: "Checking"
- `request.InitialDeposit`: 1000.0
- `request.Currency`: "NGN"

---

### **STEP 2: Log the Request**

```csharp
_logger.LogInformation("gRPC CreateAccount called for customer {CustomerId}", request.CustomerId);
```

**Output:**
```
[INFO] gRPC CreateAccount called for customer a1b2c3d4-1234-5678-9abc-123456789abc
```

---

### **STEP 3: Parse and Validate Customer ID**

```csharp
if (!Guid.TryParse(request.CustomerId, out var customerGuid))
    throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid customer ID format"));
```

**What it does:**
- Try to parse the string as a GUID
- If fails: throw error

**Example:**
```
Input: "a1b2c3d4-1234-5678-9abc-123456789abc"
    ↓
Guid.TryParse()
    ↓
✓ Success: customerGuid = Guid("a1b2c3d4-1234-5678-9abc-123456789abc")
```

**If invalid:**
```
Input: "not-a-guid"
    ↓
Guid.TryParse()
    ↓
❌ Fail: Throw RpcException
   Status: InvalidArgument
   Message: "Invalid customer ID format"
```

---

### **STEP 4: Create CQRS Command**

```csharp
var command = new CreateAccountCommand
{
    CustomerId = CustomerId.Create(customerGuid),
    AccountType = request.AccountType,
    InitialDeposit = (decimal)request.InitialDeposit,
    Currency = request.Currency
};
```

**What it creates:**
```
CreateAccountCommand
{
    CustomerId = CustomerId { Value = Guid("a1b2c3d4-...") },
    AccountType = "Checking",
    InitialDeposit = 1000.00,
    Currency = "NGN"
}
```

**Note:** This is a COMMAND (write operation), different from Query!

---

### **STEP 5: Send to MediatR**

```csharp
var result = await _mediator.Send(command);
```

**This triggers the COMMAND PIPELINE:**

```
_mediator.Send(command)
    ↓
1. ValidationBehavior
   ├─ Run CreateAccountCommandValidator
   ├─ Check: CustomerId not empty? ✓
   ├─ Check: AccountType is valid? ✓
   ├─ Check: InitialDeposit >= 0? ✓
   └─ If any fails: Return error immediately (short-circuit)
    ↓
2. LoggingBehavior
   └─ Logs: "Handling CreateAccountCommand"
    ↓
3. CreateAccountCommandHandler.Handle()
   ├─ Validate customer exists in database
   ├─ Generate unique account number
   ├─ Create Account aggregate
   ├─ Add to repository
   ├─ Save to database (includes OutboxMessages)
   ├─ Emit AccountCreatedEvent
   └─ Return Result<Guid> with new account ID
    ↓
4. DomainEventsBehavior
   ├─ Find AccountCreatedEvent
   ├─ Publish to MediatR handlers
   └─ AccountCreatedEventHandler executes
      ├─ Send welcome email
      ├─ Update analytics
      └─ etc.
    ↓
5. LoggingBehavior
   └─ Logs: "CreateAccountCommand completed"
    ↓
Return: Result<Guid>
```

**What `result` contains:**
```
Result<Guid>
{
    IsSuccess = true,
    Data = Guid("c3d4e5f6-3456-7890-cde1-345678901cde")  // New account ID
}
```

---

### **STEP 6: Check Success**

```csharp
if (result.IsSuccess)
{
    return new CreateAccountResponse
    {
        AccountId = result.Data.ToString(),
        AccountNumber = "",
        Message = "Account created successfully"
    };
}
else
{
    throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors)));
}
```

**Path A: Success**
```
✓ Return CreateAccountResponse with:
  - AccountId: "c3d4e5f6-3456-7890-cde1-345678901cde"
  - AccountNumber: "" (TODO: should be populated)
  - Message: "Account created successfully"
```

**Path B: Failure**
```
❌ Throw RpcException
   Status: InvalidArgument
   Message: error messages
   Example: "Customer not found; Account number already exists"
```

---

### **STEP 7: Exception Handling**

```csharp
catch (Exception ex) when (ex is not RpcException)
{
    _logger.LogError(ex, "Error creating account for customer {CustomerId}", request.CustomerId);
    throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
}
```

Same as GetAccount - catches unexpected errors and converts to RpcException.

---

### **STEP 8: Return Response**

```csharp
return new CreateAccountResponse
{
    AccountId = result.Data.ToString(),
    AccountNumber = "",
    Message = "Account created successfully"
};
```

**Returns:**
```
CreateAccountResponse
{
    account_id = "c3d4e5f6-3456-7890-cde1-345678901cde",
    account_number = "",
    message = "Account created successfully"
}
```

---

## CreateAccount Full Pipeline Visual

```
CLIENT REQUEST
    ↓
CreateAccount(CreateAccountRequest)
    ├─ Step 1: Method receives request
    ├─ Step 2: Log "gRPC CreateAccount called for customer ..."
    ├─ Step 3: Parse CustomerId GUID ✓
    ├─ Step 4: Create CreateAccountCommand
    ├─ Step 5: Send to MediatR
    │          ├─ ValidationBehavior validates ✓
    │          ├─ LoggingBehavior logs start
    │          ├─ CreateAccountCommandHandler
    │          │  ├─ Validate customer exists
    │          │  ├─ Generate account number
    │          │  ├─ Create Account aggregate
    │          │  ├─ Save to database
    │          │  ├─ Emit AccountCreatedEvent
    │          │  └─ Return Result<Guid>
    │          ├─ DomainEventsBehavior
    │          │  ├─ Find AccountCreatedEvent
    │          │  └─ Publish to handlers
    │          └─ LoggingBehavior logs end
    ├─ Step 6: Check result.IsSuccess ✓
    ├─ Step 7: Return CreateAccountResponse
    └─ Step 8: Send to client
         ↓
         HTTP/2 + Binary
         ↓
CLIENT RESPONSE
```

---

## Summary Table

| Step | GetAccount | CreateAccount |
|------|-----------|---------------|
| Validate | Account # not empty | Customer ID is valid GUID |
| Create VO | AccountNumber value object | CustomerId value object |
| Create C/Q | GetAccountDetailsQuery | CreateAccountCommand |
| Send MediatR | Query (read) | Command (write) |
| Handler | Query database | Create aggregate, save, emit event |
| Behaviors | Validation, Logging | Validation, Logging, Events |
| Return | AccountResponse (mapped) | CreateAccountResponse (new) |
| Error Code | NotFound (404) | InvalidArgument (400) |

---

## Key Takeaways

✅ **GetAccount flow:**
```
Validate → Create VO → Create Query → Send MediatR → Map DTO → Return Response
```

✅ **CreateAccount flow:**
```
Validate GUID → Create VO → Create Command → Send MediatR → 
Validate → Save DB → Emit Events → Dispatch Events → Return Response
```

✅ **Both use MediatR pipeline:**
```
Validation → Handler → Domain Events (for commands) → Logging
```

✅ **Error handling:**
- Validate early (before MediatR)
- RpcException for gRPC errors
- Catch unexpected exceptions → convert to RpcException

