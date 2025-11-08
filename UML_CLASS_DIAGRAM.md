# UML Class Diagrams - CoreBanking System

## 1. Domain Model Class Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                            DOMAIN LAYER - CLASS DIAGRAM                                 │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────┐
│   AggregateRoot<TId>     │ (Abstract Base)
├──────────────────────────┤
│ - Id: TId                │
│ - DomainEvents: List     │
├──────────────────────────┤
│ + AddDomainEvent()       │
│ + ClearDomainEvents()    │
└──────────────────────────┘
         ▲                    ▲
         │ inherits           │ inherits
         │                    │
    ┌────┴──────────────┐     ┌─────────────────────────┐
    │                   │     │                         │
┌───┴──────────────┐  ┌─┴────────────────┐  ┌──────────┴──────────┐
│   Customer       │  │    Account       │  │   (Other Aggregates)│
├──────────────────┤  ├──────────────────┤  └─────────────────────┘
│ - CustomerId*    │  │ - AccountId*     │
│ - FirstName      │  │ - AccountNumber  │
│ - LastName       │  │ - AccountType    │
│ - Email (UK)     │  │ - Balance        │
│ - PhoneNumber    │  │ - CustomerId (FK)│
│ - Address        │  │ - RowVersion     │
│ - DateOfBirth    │  │ - IsActive       │
│ - CreditScore    │  │ - IsDeleted      │
│ - IsActive       │  │ - DeletedAt      │
│ - IsDeleted      │  │ - DeletedBy      │
├──────────────────┤  ├──────────────────┤
│ + Create():      │  │ + Create():      │
│   Customer       │  │   Account        │
│                  │  │ + Deposit()      │
│                  │  │ + Withdraw()     │
│                  │  │ + Transfer()     │
│                  │  │ + Debit()        │
│                  │  │ + Credit()       │
│                  │  │ + CloseAccount() │
│                  │  │ + UpdateBalance()│
└───┬──────────────┘  └──────┬───────────┘
    │                         │
    │ owns 1..* (One-to-Many) │
    │◇───────────────────────►│
    │                         │
    │                    ┌────┴────────────────┐
    │                    │                     │
    │              ┌─────┴────────────┐   ┌───┴─────────────────┐
    │              │   Transaction    │   │ (Navigation Props)  │
    │              ├──────────────────┤   └─────────────────────┘
    │              │ - TransactionId* │
    │              │ - AccountId (FK) │
    │              │ - Type           │
    │              │ - Amount (Money) │
    │              │ - Description    │
    │              │ - Reference      │
    │              │ - Timestamp      │
    │              ├──────────────────┤
    │              │ + Create()       │
    │              └──────────────────┘
    │
    ▼ owns 1..*

┌─────────────────────────────────────────────────────────────────────┐
│                         VALUE OBJECTS                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌────────────────────┐  ┌────────────────────┐  ┌──────────────┐ │
│  │   CustomerId       │  │   AccountId        │  │  Money       │ │
│  ├────────────────────┤  ├────────────────────┤  ├──────────────┤ │
│  │ - Value: Guid      │  │ - Value: Guid      │  │ - Amount     │ │
│  │ (Immutable)        │  │ (Immutable)        │  │ - Currency   │ │
│  └────────────────────┘  └────────────────────┘  │ (Immutable)  │ │
│                                                  └──────────────┘ │
│  ┌────────────────────┐  ┌────────────────────┐  ┌──────────────┐ │
│  │ AccountNumber      │  │ TransactionId      │  │   Result     │ │
│  ├────────────────────┤  ├────────────────────┤  ├──────────────┤ │
│  │ - Value: string    │  │ - Value: Guid      │  │ - IsSuccess  │ │
│  │ (10 digits)        │  │ (Immutable)        │  │ - Error      │ │
│  │ (Immutable)        │  │                    │  │ (Immutable)  │ │
│  └────────────────────┘  └────────────────────┘  └──────────────┘ │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                         DOMAIN EVENTS                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────────┐   │
│  │ AccountCreated   │  │ MoneyTransferred │  │ InsufficientF. │   │
│  │ Event            │  │ Event            │  │ Event          │   │
│  ├──────────────────┤  ├──────────────────┤  ├────────────────┤   │
│  │ - AccountId      │  │ - TransactionId  │  │ - AccountNum   │   │
│  │ - AccountNumber  │  │ - SourceAccount  │  │ - Amount Req.  │   │
│  │ - CustomerId     │  │ - DestAccount    │  │ - Balance      │   │
│  │ - AccountType    │  │ - Amount         │  │ - TransType    │   │
│  │ - InitialBalance │  │ - Reference      │  │                │   │
│  │ - OccurredOn     │  │ - OccurredOn     │  │                │   │
│  └──────────────────┘  └──────────────────┘  └────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                         ENUMS & INTERFACES                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────────┐   │
│  │ AccountType      │  │ TransactionType  │  │ ISoftDelete    │   │
│  ├──────────────────┤  ├──────────────────┤  ├────────────────┤   │
│  │ - Checking       │  │ - Deposit        │  │ - IsDeleted    │   │
│  │ - Savings        │  │ - Withdrawal     │  │ - DeletedAt    │   │
│  │ - Investment     │  │ - Transfer       │  │ - DeletedBy    │   │
│  │ - Loan           │  │                  │  │                │   │
│  └──────────────────┘  └──────────────────┘  └────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2. Infrastructure & Repository Pattern

```
┌──────────────────────────────────────────────────────────────────────────┐
│                 INFRASTRUCTURE LAYER - REPOSITORY PATTERN                │
└──────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────┐
│   BankingDbContext (EF Core)           │
├────────────────────────────────────────┤
│ - DbSet<Customer>                      │
│ - DbSet<Account>                       │
│ - DbSet<Transaction>                   │
│ - DbSet<OutBoxMessage>                 │
├────────────────────────────────────────┤
│ + OnModelCreating()                    │
│ + SaveChangesWithOutboxAsync()         │
└────────┬───────────────────────────────┘
         │ uses
         │
    ┌────┴────────────────┬──────────────┬──────────────┐
    │                     │              │              │
┌───┴────────────────┐ ┌──┴────────────┐│ ┌────────────┐│
│ IAccountRepository │ │ICustomerRepo. ││ │ITransaction││
├───────────────────┤ ├───────────────┤│ ├────────────┤│
│ + GetByIdAsync()  │ │ + GetByIdAsync│ │ + GetByIdA..│
│ + GetAllAsync()   │ │ + GetAllAsync │ │ + GetByAcct │
│ + GetByAccount... │ │ + AddAsync()  │ │ + AddAsync()│
│ + GetByCustomer..│ │ + UpdateAsync │ │ + UpdateAsync
│ + AddAsync()      │ │ + DeleteAsync │ │             │
│ + UpdateAsync()   │ │               │ │             │
│ + DeleteAsync()   │ │               │ │             │
│ + UpdateBalance..│ │               │ │             │
│ + SaveChanges..  │ │               │ │             │
└───────┬───────────┘ └───┬───────────┘ └────┬────────┘
        │                 │                    │
    implements         implements          implements
        │                 │                    │
┌───────┴────────────────┬┴────────────────┬──┴────────────┐
│                        │                 │               │
┌───────────────────────┐│┌────────────────┐│┌─────────────┐│
│ AccountRepository     ││ CustomerRepository   TransactionRepository
├──────────────────────┤││ ├────────────────┤ ├─────────────┤
│ - _context: DbContext││ │ - _context     │ │ - _context  │
├──────────────────────┤││ ├────────────────┤ ├─────────────┤
│ All IAccountRepository ││ All ICustomerRep│ │ All ITrans. │
│ methods implemented    ││ methods impl.   │ │ methods impl│
│                        ││                │ │             │
│ Uses:                  ││ Uses:          │ │ Uses:       │
│ - Include()            ││ - Include()    │ │ - Include() │
│ - FirstOrDefaultAsync()││ - ToListAsync()│ │ - ToListAsync
│ - ToListAsync()        ││ - SaveChanges..│ │ - SaveChanges
└───────┬────────────────┴┴────────────────┴─┴─────────────┘
        │
        │ calls
        │
┌───────┴────────────────────────────────────┐
│            UnitOfWork (IUnitOfWork)        │
├────────────────────────────────────────────┤
│ - _context: BankingDbContext               │
├────────────────────────────────────────────┤
│ + SaveChangesAsync(cancellationToken)      │
│ + SaveEntitiesAsync(cancellationToken)     │
└────────────────────────────────────────────┘
```

---

## 3. Application Layer - CQRS Pattern

```
┌──────────────────────────────────────────────────────────────────────────┐
│                  APPLICATION LAYER - CQRS PATTERN                        │
└──────────────────────────────────────────────────────────────────────────┘

                    ┌────────────────────────────┐
                    │      MediatR Pipeline      │
                    ├────────────────────────────┤
                    │ 1. ValidationBehavior      │
                    │ 2. LoggingBehavior         │
                    │ 3. DomainEventsBehavior    │
                    └────────────────────────────┘
                              ▲
                              │
                              │
        ┌─────────────────────┴──────────────────────┐
        │                                            │
┌───────┴────────────────────┐       ┌──────────────┴──────────┐
│      COMMANDS (Write)       │       │    QUERIES (Read)        │
├────────────────────────────┤       ├────────────────────────┤
│                            │       │                        │
│ CreateAccountCommand       │       │ GetAccountQuery        │
├────────────────────────────┤       ├────────────────────────┤
│ - CustomerId               │       │ - AccountId            │
│ - AccountNumber            │       ├────────────────────────┤
│ - AccountType              │       │ + Handler              │
│ - InitialDeposit           │       │  ↓ Returns AccountDto  │
├────────────────────────────┤       │                        │
│ Validator:                 │       │ GetAccountsQuery       │
│ CreateAccountCommandValidator      ├────────────────────────┤
│                            │       │ - CustomerId           │
│ Handler:                   │       ├────────────────────────┤
│ CreateAccountCommandHandler        │ + Handler              │
│  ↓ Creates Account                 │  ↓ Returns List<AccountDto>
│  ↓ Emits AccountCreatedEvent       │                        │
│  ↓ Calls SaveChangesWithOutbox     │                        │
│                            │       │                        │
│ TransferMoneyCommand       │       │ GetTransactionsQuery   │
├────────────────────────────┤       ├────────────────────────┤
│ - SourceAccountId          │       │ - AccountId            │
│ - DestinationAccountId     │       │ - FromDate             │
│ - Amount                   │       │ - ToDate               │
│ - Reference                │       ├────────────────────────┤
├────────────────────────────┤       │ + Handler              │
│ Validator:                 │       │  ↓ Returns List<TransactionDto>
│ TransferMoneyCommandValidator      │                        │
│                            │       │ GetCustomerQuery       │
│ Handler:                   │       ├────────────────────────┤
│ TransferMoneyCommandHandler        │ - CustomerId           │
│  ↓ Loads source & dest accounts   ├────────────────────────┤
│  ↓ Calls account.Transfer()       │ + Handler              │
│  ↓ Emits MoneyTransferredEvent    │  ↓ Returns CustomerDto │
│  ↓ Calls SaveChangesWithOutbox    │                        │
│                            │       │                        │
│ CreateCustomerCommand      │       │                        │
│ WithdrawMoneyCommand       │       │                        │
│ [More commands...]         │       │ [More queries...]      │
│                            │       │                        │
└────────────────────────────┘       └────────────────────────┘

                    ┌────────────────────────────┐
                    │   EVENT HANDLERS (MediatR) │
                    ├────────────────────────────┤
                    │ AccountCreatedEventHandler │
                    │  ↓ Logs account creation   │
                    │                            │
                    │ MoneyTransferredEventHandler
                    │  ↓ Logs transfer          │
                    │  ↓ Calls audit service    │
                    │                            │
                    │RealTimeNotification...    │
                    │ ↓ Broadcasts via SignalR  │
                    │                            │
                    │InsufficientFundsEventHandler
                    │  ↓ Logs insufficient funds│
                    │                            │
                    │ [More handlers...]        │
                    │                            │
                    └────────────────────────────┘
```

---

## 4. Outbox Pattern - Event Processing

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    OUTBOX PATTERN - EVENT PROCESSING                     │
└──────────────────────────────────────────────────────────────────────────┘

STEP 1: Domain Event Created
┌─────────────────────────────────────┐
│   Business Operation                │
│   (Transfer Money, Create Account)  │
│            │                        │
│            ▼                        │
│   Account raises DomainEvent        │
│   Event added to _domainEvents list │
└─────────────────────────────────────┘
            │
            ▼
STEP 2: Save with Outbox Pattern
┌──────────────────────────────────────────────┐
│  SaveChangesWithOutboxAsync()                │
│                                              │
│  1. Extract all DomainEvents from aggregates│
│     └─ MoneyTransferredEvent (JSON)         │
│                                              │
│  2. Create OutBoxMessage entities           │
│     └─ Id, Type, Content, OccurredOn        │
│                                              │
│  3. Clear DomainEvents from aggregates      │
│     └─ _domainEvents.Clear()                │
│                                              │
│  4. SaveChangesAsync() ← ATOMIC TRANSACTION │
│     ├─ Save Account modifications           │
│     ├─ Save OutBoxMessage row               │
│     └─ All-or-nothing commit               │
│                                              │
│  5. Return to handler                       │
└──────────────────────────────────────────────┘
            │
            ▼
STEP 3: Background Service Polling
┌──────────────────────────────────────────────┐
│  OutboxBackgroundService                    │
│  (Hosted Service - runs every 30 seconds)   │
│                                              │
│  1. Query unprocessed messages:             │
│     SELECT * FROM OutboxMessages            │
│     WHERE ProcessedOn IS NULL               │
│     LIMIT 20                                │
│                                              │
│  2. If found, call ProcessOutboxMessagesAsync
│     └─ Pass messages to processor           │
│                                              │
│  3. If error, wait 30s and retry            │
│                                              │
└──────────────────────────────────────────────┘
            │
            ▼
STEP 4: Event Deserialization & Publishing
┌──────────────────────────────────────────────┐
│  OutboxMessageProcessor                     │
│  (IOutboxMessageProcessor)                  │
│                                              │
│  For each OutBoxMessage:                    │
│                                              │
│  1. Deserialize Content (JSON)              │
│     └─ JsonSerializer.Deserialize()         │
│     └─ Returns MoneyTransferredEvent        │
│                                              │
│  2. Publish via MediatR                     │
│     └─ _mediator.Publish(domainEvent)      │
│     └─ All registered handlers execute      │
│                                              │
│  3. On Success:                             │
│     └─ message.ProcessedOn = DateTime.Now   │
│     └─ message.RetryCount = 0               │
│     └─ SaveChangesAsync()                   │
│                                              │
│  4. On Failure:                             │
│     └─ message.RetryCount++                 │
│     └─ if (RetryCount < 3) retry            │
│     └─ else store error & mark done         │
│                                              │
│  5. Next polling cycle processes next batch │
│                                              │
└──────────────────────────────────────────────┘
            │
            ▼
STEP 5: Real-time Notifications
┌──────────────────────────────────────────────┐
│  Event Handlers (MediatR)                   │
│                                              │
│  ├─ AccountCreatedEventHandler              │
│  │   ├─ Logs event                          │
│  │   └─ Updates audit trail                 │
│  │                                           │
│  ├─ MoneyTransferredEventHandler            │
│  │   ├─ Logs transfer                       │
│  │   └─ Updates analytics                   │
│  │                                           │
│  └─ RealTimeNotificationEventHandler        │
│      ├─ Gets SignalR hub context            │
│      ├─ Broadcasts to connected clients:    │
│      │  ├─ TransactionHub (transaction data)│
│      │  ├─ NotificationHub (notifications)  │
│      │  └─ EnhancedNotificationHub (details)│
│      └─ Clients update UI in real-time      │
│                                              │
└──────────────────────────────────────────────┘

DATABASE STATE AT EACH STAGE:

Accounts Table:
┌─────────────────────────────────────────┐
│ AccountId │ Balance │ RowVersion │      │
├─────────────────────────────────────────┤
│ ACC-001   │  500.00 │   v1      │      │
│ ACC-002   │ 1000.00 │   v1      │      │
└─────────────────────────────────────────┘

After Transfer (Step 2):
┌──────────────────────────────────────────┐
│ AccountId │ Balance │ RowVersion │       │
├──────────────────────────────────────────┤
│ ACC-001   │  400.00 │   v2       │       │
│ ACC-002   │ 1100.00 │   v2       │       │
└──────────────────────────────────────────┘

OutBoxMessages Table (Step 2):
┌──────────────────────────────────────┐
│ Id  │ Type    │ Content │ProcessedOn │
├──────────────────────────────────────┤
│ msg │ Money   │ {JSON}  │   NULL     │
│ -1  │ Trans.. │        │            │
└──────────────────────────────────────┘

After Processing (Step 4):
┌──────────────────────────────────────┐
│ Id  │ Type    │ Content │ProcessedOn │
├──────────────────────────────────────┤
│ msg │ Money   │ {JSON}  │  2025-11-07│
│ -1  │ Trans.. │        │  01:15:30  │
└──────────────────────────────────────┘
```

---

## 5. API Request/Response Flow

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    API LAYER - REQUEST FLOW DIAGRAM                      │
└──────────────────────────────────────────────────────────────────────────┘

CLIENT (Web/Mobile/gRPC Client)
       │
       │ HTTP POST /api/accounts/transfer
       │ or gRPC: AccountService.Transfer()
       │
       ▼
┌─────────────────────────────────────┐
│   API Controller / gRPC Service     │
│  (AccountController or             │
│   AccountGrpcService)              │
├─────────────────────────────────────┤
│ 1. Parse request                   │
│ 2. Map to Command                  │
│ 3. Validate basic structure        │
│ 4. Send to MediatR                 │
└────────┬────────────────────────────┘
         │
         ▼
    ┌─────────────────────────────────────┐
    │  MediatR Pipeline - VALIDATION      │
    │                                     │
    │  ValidationBehavior                 │
    │  ├─ Run FluentValidation validators │
    │  ├─ Check amount > 0                │
    │  ├─ Check accounts exist            │
    │  ├─ Check not same account          │
    │  └─ On error: throw ValidationEx.  │
    │                                     │
    │  If error → Return BadRequest(400) │
    │                                     │
    └────────┬────────────────────────────┘
             │ Validation Passed
             ▼
    ┌─────────────────────────────────────┐
    │  MediatR Pipeline - LOGGING         │
    │                                     │
    │  LoggingBehavior                    │
    │  ├─ Log: "TransferMoneyCommand"    │
    │  ├─ Log: Source & Dest AccountIds   │
    │  ├─ Log: Amount                     │
    │  └─ Continue to handler             │
    │                                     │
    └────────┬────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────┐
    │  TransferMoneyCommandHandler         │
    │                                      │
    │  1. Get source account by ID         │
    │     └─ await _repo.GetByIdAsync()   │
    │                                      │
    │  2. Get destination account by ID    │
    │     └─ await _repo.GetByIdAsync()   │
    │                                      │
    │  3. Call Account.Transfer()          │
    │     └─ Domain logic executes        │
    │     └─ MoneyTransferredEvent created │
    │     └─ Returns Result(Success/Fail) │
    │                                      │
    │  4. If Result.IsSuccess:            │
    │     └─ await _unitOfWork.Save...()  │
    │     └─ Calls SaveChangesWithOutbox  │
    │     └─ Returns new account state    │
    │                                      │
    │  5. If Result.IsFailed:             │
    │     └─ throw DomainException()      │
    │                                      │
    └────────┬────────────────────────────┘
             │
             ▼
    ┌──────────────────────────────────────┐
    │  MediatR Pipeline - EVENT DISPATCH   │
    │                                      │
    │  DomainEventsBehavior                │
    │  ├─ After handler completes         │
    │  ├─ Get aggregates with events      │
    │  ├─ Publish each event via MediatR  │
    │  └─ All handlers execute:           │
    │      ├─ MoneyTransferedEventHandler │
    │      ├─ RealTimeNotificationHandler │
    │      └─ Any custom handlers         │
    │                                      │
    └────────┬────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  Response Mapper                    │
│                                     │
│  1. Map Account to AccountDto       │
│  2. Build response payload          │
│  3. Return to controller            │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  API Controller                     │
│                                     │
│  ├─ On Success: Return 200 + DTO   │
│  └─ On Exception:                   │
│      └─ GlobalExceptionHandler      │
│         ├─ Log error                │
│         ├─ Map to error response    │
│         └─ Return 400/500 + message │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  HTTP Response                      │
│                                     │
│  200 OK + TransferResponseDto:      │
│  {                                  │
│    "sourceAccountId": "...",        │
│    "destinationAccountId": "...",   │
│    "amount": 100,                   │
│    "status": "SUCCESS"              │
│  }                                  │
│                                     │
│  OR                                 │
│                                     │
│  400 Bad Request:                   │
│  {                                  │
│    "error": "Insufficient funds",   │
│    "code": "ERR_INSUFFICIENT_FUNDS" │
│  }                                  │
│                                     │
└─────────────────────────────────────┘
         │
         ▼
    CLIENT (Updated UI)
```

---

## 6. Concurrency & Soft Delete Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│           CONCURRENCY CONTROL & SOFT DELETE ARCHITECTURE                 │
└──────────────────────────────────────────────────────────────────────────┘

CONCURRENCY CONTROL (Optimistic Locking):

Account Table:
┌────────────────────────────────────────┐
│ AccountId │ Balance │ RowVersion       │
├────────────────────────────────────────┤
│ ACC-001   │  500.00 │ 0x00000000000A  │ ← v10
│ ACC-002   │ 1000.00 │ 0x00000000000B  │ ← v11
└────────────────────────────────────────┘

Scenario: Two concurrent transfers from ACC-001

USER A                                USER B
│                                     │
├─ Load Account ACC-001              ├─ Load Account ACC-001
│  Balance: 500                      │  Balance: 500
│  RowVersion: 0x0000...0A           │  RowVersion: 0x0000...0A
│                                     │
├─ Transfer $200                     ├─ Transfer $300
│  Balance: 300                      │  Balance: 200
│  RowVersion: 0x0000...0B           │  
│                                     │
└─ UPDATE WHERE RowVersion = 0x0A    │
   └─ SUCCESS ✓                      │
      RowVersion: 0x0000...0B        │
                                     │
                                     └─ UPDATE WHERE RowVersion = 0x0A
                                        └─ FAILS ✗
                                           (RowVersion is now 0x0B)
                                           DbUpdateConcurrencyException thrown
                                           User B gets error: "The record was modified
                                           by another user. Please reload and try again"

ISoftDelete Interface:
┌────────────────────────────────────┐
│       ISoftDelete                  │
├────────────────────────────────────┤
│ + IsDeleted: bool                  │
│ + DeletedAt: DateTime?             │
│ + DeletedBy: string?               │
└────────────────────────────────────┘
    ▲
    │ implemented by
    │
    ├─ Customer
    ├─ Account
    └─ Transaction


SOFT DELETE - Logical Deletion:

BEFORE DELETE:
┌──────────────────────────────────────────┐
│ AccountId │ Balance │ IsDeleted │        │
├──────────────────────────────────────────┤
│ ACC-001   │  500.00 │    false  │        │
│ ACC-002   │ 1000.00 │    false  │        │
│ ACC-003   │  750.00 │    false  │        │
└──────────────────────────────────────────┘

DELETE command: DeleteAccount(ACC-001)
    ├─ Instead of: DELETE FROM Accounts WHERE AccountId = ACC-001
    └─ Execute:   UPDATE Accounts
                  SET IsDeleted = true,
                      DeletedAt = NOW(),
                      DeletedBy = 'admin'
                  WHERE AccountId = ACC-001

AFTER DELETE:
┌──────────────────────────────────────────┐
│ AccountId │ Balance │ IsDeleted │        │
├──────────────────────────────────────────┤
│ ACC-001   │  500.00 │    true   │ ← Marked deleted
│ ACC-002   │ 1000.00 │    false  │
│ ACC-003   │  750.00 │    false  │
└──────────────────────────────────────────┘


GLOBAL QUERY FILTER (Auto-applied to all queries):

In BankingDbContext.OnModelCreating():

modelBuilder.Entity<Customer>()
    .HasQueryFilter(c => !c.IsDeleted);

modelBuilder.Entity<Account>()
    .HasQueryFilter(a => !a.IsDeleted);

modelBuilder.Entity<Transaction>()
    .HasQueryFilter(t => !t.Account.IsDeleted);


ANY QUERY automatically becomes:

SELECT * FROM Accounts            ────────►  SELECT * FROM Accounts
WHERE ...                                    WHERE ... AND IsDeleted = false

var accounts = await _repo.GetAllAsync();
    ├─ Executes: SELECT * FROM Accounts 
    │           WHERE IsDeleted = false  ← FILTER AUTO-APPLIED
    └─ Returns: Only active accounts

var account = await _repo.GetByIdAsync(accId);
    ├─ Executes: SELECT * FROM Accounts 
    │           WHERE AccountId = @id 
    │           AND IsDeleted = false    ← FILTER AUTO-APPLIED
    └─ Returns: Account only if not deleted


BENEFITS:

1. Soft Delete:
   ├─ Preserves audit trail
   ├─ Allows recovery/restoration
   ├─ Maintains referential integrity
   └─ Data not truly lost

2. Concurrency Control:
   ├─ Prevents lost updates
   ├─ No database locks needed
   ├─ Detects conflicts immediately
   └─ User can retry/reload
```

