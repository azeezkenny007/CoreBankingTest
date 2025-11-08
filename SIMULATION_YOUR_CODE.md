# Simulation: Money Transfer in Your Code - Step by Step

## Scenario: User Transfers $100 from Account A to Account B

```
Account A (Source): 
  - AccountNumber: "1000000001"
  - Balance: $500
  - RowVersion: 0x0A

Account B (Destination):
  - AccountNumber: "1000000002"  
  - Balance: $1000
  - RowVersion: 0x0B

USER ACTION: "Transfer $100"
```

---

## STEP 1: User Clicks Transfer Button

```http
POST /api/accounts/transfer
Content-Type: application/json

{
  "sourceAccountNumber": "1000000001",
  "destinationAccountNumber": "1000000002",
  "amount": 100,
  "reference": "TRANSFER-001",
  "description": "Personal transfer"
}
```

---

## STEP 2: API Controller Receives Request

```csharp
// In your API Controller
[HttpPost("transfer")]
public async Task<IActionResult> Transfer(TransferMoneyRequest request)
{
    // The controller creates a command object
    var command = new TransferMoneyCommand
    {
        SourceAccountNumber = AccountNumber.Create(request.SourceAccountNumber),
        DestinationAccountNumber = AccountNumber.Create(request.DestinationAccountNumber),
        Amount = new Money(request.Amount),
        Reference = request.Reference,
        Description = request.Description
    };
    
    // Send to MediatR
    var result = await _mediator.Send(command);
    
    return result.IsSuccess 
        ? Ok(result)
        : BadRequest(result);
}

// ✅ MediatR receives: TransferMoneyCommand
```

---

## STEP 3: MediatR Pipeline - Validation

```csharp
// ValidationBehavior runs first
// It checks: TransferMoneyCommandValidator

if (request.Amount.Amount <= 0)
    throw new ValidationException("Amount must be positive");

if (request.SourceAccountNumber == request.DestinationAccountNumber)
    throw new ValidationException("Cannot transfer to same account");

// ✅ All validations pass → Continue to handler
```

---

## STEP 4: MediatR Pipeline - Logging

```csharp
// LoggingBehavior logs the incoming command
_logger.LogInformation(
    "Handling command: TransferMoneyCommand with amount: {Amount}",
    request.Amount.Amount
);

// ✅ Logs: "Handling command: TransferMoneyCommand with amount: 100"
```

---

## STEP 5: Handler Executes - Load Accounts

```csharp
// YOUR CODE: TransferMoneyCommandHandler

public async Task<Result> Handle(
    TransferMoneyCommand request, 
    CancellationToken cancellationToken)
{
    try
    {
        // LINE 46-47: Load accounts from database
        var sourceAccount = await _accountRepository
            .GetByAccountNumberAsync(
                new AccountNumber(request.SourceAccountNumber)
            );
        
        var destAccount = await _accountRepository
            .GetByAccountNumberAsync(
                new AccountNumber(request.DestinationAccountNumber)
            );

        // ✅ From database:
        //    sourceAccount = Account {
        //      AccountNumber: "1000000001",
        //      Balance: 500,
        //      IsActive: true,
        //      RowVersion: 0x0A,
        //      _domainEvents: []  ← Empty list
        //    }
        //
        //    destAccount = Account {
        //      AccountNumber: "1000000002",
        //      Balance: 1000,
        //      IsActive: true,
        //      RowVersion: 0x0B,
        //      _domainEvents: []  ← Empty list
        //    }

        // LINE 49-50: Null checks
        if (sourceAccount == null) 
            return Result.Failure("Source account not found");
        if (destAccount == null) 
            return Result.Failure("Destination account not found");

        // ✅ Both accounts exist → Continue
```

---

## STEP 6: Call Account.Transfer()

```csharp
// STILL IN: TransferMoneyCommandHandler.Handle()
// LINE 53-58

sourceAccount.Transfer(
    amount: request.Amount,           // Money(100)
    destination: destAccount,
    reference: request.Reference,      // "TRANSFER-001"
    description: request.Description   // "Personal transfer"
);

// ✅ Now jumps to Account.Transfer() method (YOUR DOMAIN LAYER)
```

---

## STEP 7: Account.Transfer() - Business Logic

```csharp
// IN: CoreBankingTest.CORE.Entities.Account.cs
// LINE 139-191

public Result Transfer(
    Money amount, 
    Account destination, 
    string reference, 
    string description)
{
    // LINE 142-149: Validation checks
    if (destination == null)
        throw new ArgumentNullException(...);

    if (amount.Amount <= 0)
        throw new InvalidOperationException(...);

    if (this == destination)
        throw new InvalidOperationException(...);

    // ✅ All pass

    // LINE 152-156: Check account status
    if (!IsActive)
        throw new InvalidOperationException("Source account is not active");

    if (!destination.IsActive)
        throw new InvalidOperationException("Destination account is not active");

    // ✅ Both accounts are active

    // LINE 159-166: Check sufficient funds
    if (Balance.Amount < amount.Amount)
    {
        // If fails, create event for insufficient funds
        _domainEvents.Add(new InsufficientFundsEvent(
            AccountNumber, amount, Balance, "Transfer"
        ));
        throw new InsufficientFundsException(amount.Amount, Balance.Amount);
    }

    // ✅ Balance check passes (500 >= 100)

    // LINE 168-173: Special business rules for Savings accounts
    if (AccountType == AccountType.Savings &&
        _transactions.Count(t => t.Type == TransactionType.Withdrawal) >= 6)
    {
        return Result.Failure("Savings account withdrawal limit reached");
    }

    // ✅ Not a savings account issue (or under limit)

    // ⭐⭐⭐ STEP 7A: DEBIT SOURCE ACCOUNT ⭐⭐⭐
    // LINE 176
    var debitResult = Debit(
        amount,                                    // Money(100)
        $"Transfer to {destination.AccountNumber}", // "Transfer to 1000000002"
        reference                                   // "TRANSFER-001"
    );

    if (!debitResult.IsSuccess)
        return debitResult;

    // ✅ After Debit():
    //    sourceAccount.Balance: 500 → 400
    //    sourceAccount._transactions: [Transaction {...}]

    // ⭐⭐⭐ STEP 7B: CREDIT DESTINATION ACCOUNT ⭐⭐⭐
    // LINE 180
    var creditResult = destination.Credit(
        amount,                                    // Money(100)
        $"Transfer from {AccountNumber}",          // "Transfer from 1000000001"
        reference                                   // "TRANSFER-001"
    );

    if (!creditResult.IsSuccess)
        return creditResult;

    // ✅ After Credit():
    //    destAccount.Balance: 1000 → 1100
    //    destAccount._transactions: [Transaction {...}]

    // ⭐⭐⭐ STEP 7C: CREATE THE EVENT ⭐⭐⭐
    // LINE 185-187
    var transactionId = TransactionId.Create();  // New Guid: "trans-abc123"
    
    _domainEvents.Add(new MoneyTransferredEvent(
        transactionId,                            // "trans-abc123"
        AccountNumber,                            // "1000000001" (source)
        destination.AccountNumber,                // "1000000002" (dest)
        amount,                                   // Money(100)
        reference                                 // "TRANSFER-001"
    ));

    // ✅ EVENT CREATED AND STORED IN MEMORY
    //
    //    sourceAccount._domainEvents now contains:
    //    [
    //      MoneyTransferredEvent {
    //        TransactionId: "trans-abc123",
    //        SourceAccountNumber: "1000000001",
    //        DestinationAccountNumber: "1000000002",
    //        Amount: Money(100),
    //        Reference: "TRANSFER-001",
    //        TransferDate: DateTime.Now
    //      }
    //    ]

    // LINE 190: Return success
    return Result.Success();
}

// ✅ Back to handler - transfer completed successfully
```

---

## STEP 8: Back to Handler - Save to Database

```csharp
// BACK IN: TransferMoneyCommandHandler.Handle()
// LINE 60

await _unitOfWork.SaveChangesAsync(cancellationToken);

// ✅ This calls: SaveChangesWithOutboxAsync()
```

---

## STEP 9: SaveChangesWithOutboxAsync() - The Magic

```csharp
// IN: BankingDbContext.cs

public async Task SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
{
    // ========================================
    // STEP A: EXTRACT DOMAIN EVENTS
    // ========================================
    
    // Get all domain events from all aggregates
    var events = ChangeTracker
        .Entries<AggregateRoot<AccountId>>()
        .SelectMany(x => x.Entity.DomainEvents)
        .Select(domainEvent => new OutBoxMessage
        {
            Id = Guid.NewGuid(),                    // "msg-xyz789"
            Type = domainEvent.GetType().Name,      // "MoneyTransferredEvent"
            Content = JsonSerializer.Serialize(
                domainEvent, 
                domainEvent.GetType()
            ),
            // ✅ Serialized to JSON:
            // {
            //   "transactionId": "trans-abc123",
            //   "sourceAccountNumber": "1000000001",
            //   "destinationAccountNumber": "1000000002",
            //   "amount": 100,
            //   "reference": "TRANSFER-001",
            //   "transferDate": "2025-11-07T02:06:03"
            // }
            
            OccurredOn = domainEvent.OccurredOn      // DateTime.Now
        })
        .ToList();

    // ✅ events = [OutBoxMessage {...}]

    // ========================================
    // STEP B: CLEAR EVENTS FROM AGGREGATES
    // ========================================
    
    ChangeTracker.Entries<AggregateRoot<AccountId>>()
        .ToList()
        .ForEach(entry => entry.Entity.ClearDomainEvents());

    // ✅ sourceAccount._domainEvents.Clear()
    // ✅ destAccount._domainEvents.Clear()
    // ✅ Both now have empty _domainEvents lists

    // ========================================
    // STEP C: FIRST SaveChangesAsync()
    // ========================================
    
    await base.SaveChangesAsync(cancellationToken);

    // ✅ DATABASE UPDATED:
    //    Accounts table:
    //      UPDATE Accounts 
    //      SET Balance = 400, RowVersion = 0x0B
    //      WHERE AccountId = 'account-a-id' AND RowVersion = 0x0A
    //      
    //      UPDATE Accounts 
    //      SET Balance = 1100, RowVersion = 0x0C
    //      WHERE AccountId = 'account-b-id' AND RowVersion = 0x0B

    // ========================================
    // STEP D: INSERT OUTBOX MESSAGES
    // ========================================
    
    if (events.Any())
    {
        await OutboxMessages.AddRangeAsync(events, cancellationToken);
        
        // ✅ OutboxMessages table:
        //    INSERT INTO OutboxMessages
        //    (Id, Type, Content, OccurredOn, ProcessedOn, RetryCount, Error)
        //    VALUES
        //    ('msg-xyz789', 'MoneyTransferredEvent', '{...json...}', 
        //     '2025-11-07T02:06:03', NULL, 0, NULL)

        // ========================================
        // STEP E: SECOND SaveChangesAsync()
        // ========================================
        
        await base.SaveChangesAsync(cancellationToken);

        // ✅ OUTBOX MESSAGE INSERTED
    }
}

// ✅ ATOMIC TRANSACTION COMPLETE
//    Both accounts updated AND event persisted
//    OR nothing changed at all (if error)
```

---

## STEP 10: DomainEventsBehavior - Publish to Handlers

```csharp
// In MediatR Pipeline
// After handler completes successfully

// Get the events that were just created
var events = aggregates.SelectMany(a => a.DomainEvents).ToList();

// For each event, publish to MediatR
foreach (var @event in events)
{
    // This is: MoneyTransferredEvent
    await _mediator.Publish(@event, cancellationToken);
}

// ✅ MediatR finds all handlers for MoneyTransferredEvent
//    Handlers registered: MoneyTransferedEventHandler
//    
//    Calls: await handler.Handle(MoneyTransferredEvent, cancellationToken)
```

---

## STEP 11: MoneyTransferedEventHandler Executes (SIDE EFFECT 1)

```csharp
// YOUR CODE: CoreBankingTest.APP.Accounts.EventHandlers

public class MoneyTransferedEventHandler : 
    INotificationHandler<MoneyTransferredEvent>
{
    private readonly ILogger<MoneyTransferedEventHandler> _logger;

    public async Task Handle(
        MoneyTransferredEvent notification,  // The event from STEP 7C
        CancellationToken cancellationToken)
    {
        // LINE 28-29: Log the event
        _logger.LogInformation(
            "Processing money transferred event for transaction {TransactionId}",
            notification.TransactionId  // "trans-abc123"
        );

        // ✅ SIDE EFFECT 1: Log entry created
        //    Log output: "Processing money transferred event for transaction trans-abc123"

        // LINE 32-38: These are commented out, but would do more work:
        // await _notificationService.SendTransferNotificationAsync(...)
        // await _reportingService.RecordTransactionAsync(...)

        // LINE 41: Log success
        _logger.LogInformation("Successfully processed money transferred event");

        // ✅ SIDE EFFECT 2: Log entry created
        //    Log output: "Successfully processed money transferred event"

        await Task.CompletedTask;
    }
}

// ✅ Handler completed
//    Accounts were already updated in database
//    Event was already persisted
//    Now just logged the event
```

---

## STEP 12: Return Response to Client

```csharp
// Back in API Controller

var result = await _mediator.Send(command);

if (result.IsSuccess)
    return Ok(new TransferResponseDto
    {
        Message = "Transfer successful",
        SourceBalance = 400,
        DestinationBalance = 1100,
        TransactionId = "trans-abc123",
        Timestamp = DateTime.Now
    });

// ✅ HTTP 200 OK
{
  "message": "Transfer successful",
  "sourceBalance": 400,
  "destinationBalance": 1100,
  "transactionId": "trans-abc123",
  "timestamp": "2025-11-07T02:06:03"
}
```

---

## STEP 13: What's Now in the Database?

```sql
-- Accounts table
SELECT AccountNumber, Balance, RowVersion FROM Accounts;

/*
  AccountNumber    Balance    RowVersion
  1000000001       400        0x0B          ← Updated from 500
  1000000002       1100       0x0C          ← Updated from 1000
*/

-- OutboxMessages table
SELECT Id, Type, Content, OccurredOn, ProcessedOn FROM OutboxMessages;

/*
  Id              Type                    Content                      OccurredOn          ProcessedOn
  msg-xyz789      MoneyTransferredEvent   {...json event data...}      2025-11-07 02:06:03 NULL
*/
```

---

## STEP 14: Background Service Wakes Up (T+30 seconds)

```csharp
// OutboxBackgroundService runs every 30 seconds

public class OutboxBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // ✅ Query unprocessed messages
            var messages = await _context.OutboxMessages
                .Where(m => m.ProcessedOn == null)  // NULL = not processed yet
                .Take(20)
                .ToListAsync(stoppingToken);

            // ✅ Found: msg-xyz789 (our MoneyTransferredEvent)

            if (messages.Any())
            {
                // Process each message
                await _processor.ProcessOutboxMessagesAsync(
                    messages, 
                    stoppingToken
                );
            }

            // ✅ Wait 30 seconds and check again
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

---

## STEP 15: OutboxMessageProcessor - Finalize Event

```csharp
// OutboxMessageProcessor processes the event

public async Task ProcessOutboxMessagesAsync(
    IReadOnlyList<OutBoxMessage> messages,
    CancellationToken cancellationToken)
{
    foreach (var message in messages)
    {
        try
        {
            // ✅ STEP 1: Deserialize JSON back to event object
            var eventType = Type.GetType($"CoreBankingTest.CORE.Events.{message.Type}");
            var @event = (DomainEvent)JsonSerializer.Deserialize(
                message.Content,
                eventType
            );

            // ✅ @event is now: MoneyTransferredEvent {
            //      TransactionId: "trans-abc123",
            //      SourceAccountNumber: "1000000001",
            //      DestinationAccountNumber: "1000000002",
            //      Amount: Money(100),
            //      Reference: "TRANSFER-001"
            //    }

            // ✅ STEP 2: Publish to MediatR handlers
            await _mediator.Publish(@event, cancellationToken);

            // ✅ MoneyTransferedEventHandler runs AGAIN
            //    (if needed for external systems)

            // ✅ STEP 3: Mark as processed
            message.ProcessedOn = DateTime.Now;
            message.RetryCount = 0;

            // ✅ STEP 4: Save to database
            await _context.SaveChangesAsync(cancellationToken);

            // ✅ UPDATE OutboxMessages
            //    SET ProcessedOn = '2025-11-07 02:06:33'
            //    WHERE Id = 'msg-xyz789'
        }
        catch (Exception ex)
        {
            // ✅ STEP 5: Handle errors with retry logic
            message.RetryCount++;
            message.Error = ex.Message;

            if (message.RetryCount < 3)
            {
                // Will retry next cycle
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
```

---

## FINAL STATE - Everything Complete

```
✅ ACCOUNTS (Persisted in Database)
   Account A: Balance = 400, RowVersion = 0x0B
   Account B: Balance = 1100, RowVersion = 0x0C

✅ EVENT (Persisted in Database)
   OutboxMessages: ProcessedOn = '2025-11-07 02:06:33'

✅ LOGS (Side Effects Created)
   - "Processing money transferred event for transaction trans-abc123"
   - "Successfully processed money transferred event"

✅ TIMELINE
   T+0ms:  User clicks transfer
   T+25ms: Accounts updated, event created
   T+30ms: Response sent to user (200 OK)
   T+30s:  Background service processes event
   T+30s:  Event marked as complete

✅ EVENT JOURNEY
   1. Created in Account.Transfer()
   2. Stored in _domainEvents list (memory)
   3. Extracted and serialized to JSON
   4. Inserted into OutboxMessages table
   5. Published to MediatR handlers
   6. MoneyTransferedEventHandler logged it
   7. Background service deserialized it
   8. Marked as ProcessedOn in database
```

---

## Summary: What Happens at Each Stage

| Stage | Action | Result |
|-------|--------|--------|
| **1-2** | User → Request | TransferMoneyCommand created |
| **3-4** | MediatR Pipeline | Validated + Logged |
| **5** | Handler loads accounts | Account A & B loaded |
| **6-7** | Account.Transfer() | Balances updated, event created (in memory) |
| **8-9** | SaveChangesWithOutboxAsync() | Accounts + Event both saved to DB (ATOMIC) |
| **10-11** | Handler publishes event | MoneyTransferedEventHandler logs event |
| **12** | Response sent | 200 OK to user |
| **13-14** | Database state | Accounts updated, Event waiting to be marked |
| **15** | Background service | Event processed and marked complete |

---

## Side Effects in YOUR Code

```
SIDE EFFECT 1: Logging (from MoneyTransferedEventHandler)
├─ _logger.LogInformation("Processing money transferred event...")
└─ Result: Log entry created

SIDE EFFECT 2: SignalR Broadcast (NOT IMPLEMENTED YET)
├─ Would notify connected clients in real-time
└─ Code is commented out but ready to implement

SIDE EFFECT 3: Email Notification (NOT IMPLEMENTED YET)
├─ Would send confirmation email to both parties
└─ Code is commented out but ready to implement

SIDE EFFECT 4: Reporting/Analytics (NOT IMPLEMENTED YET)
├─ Would record transaction for reporting
└─ Code is commented out but ready to implement
```

All the infrastructure is ready - you just need to uncomment and configure the services!

