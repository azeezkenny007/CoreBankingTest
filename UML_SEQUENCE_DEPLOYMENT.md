# UML Sequence & Deployment Diagrams - CoreBanking System

## 1. Sequence Diagram: Money Transfer Flow

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                SEQUENCE DIAGRAM: MONEY TRANSFER OPERATION                             │
└────────────────────────────────────────────────────────────────────────────────────────┘

Client          APICtrl         MediatR         Handler         Domain          Repository
  │               │               │               │               │                │
  │ POST /transfer│               │               │               │                │
  ├──────────────►│               │               │               │                │
  │               │               │               │               │                │
  │               │ CreateCommand │               │               │                │
  │               ├──────────────►│               │               │                │
  │               │               │               │               │                │
  │               │        ValidationBehavior    │               │                │
  │               │        (FluentValidation)    │               │                │
  │               │        Checks: amount > 0   │               │                │
  │               │        Checks: accounts OK   │               │                │
  │               │        if fails → throw      │               │                │
  │               │               │               │               │                │
  │               │        LoggingBehavior       │               │                │
  │               │        Logs command received │               │                │
  │               │               │               │               │                │
  │               │               ├──────────────────────────────►│                │
  │               │               │    TransferMoneyCommandHandler│                │
  │               │               │               │               │                │
  │               │               │               │ GetSourceAcct │
  │               │               │               │─────────────────────────────────►│
  │               │               │               │               │                │
  │               │               │               │◄──────────────────────Account────│
  │               │               │               │               │                │
  │               │               │               │ GetDestAcct   │
  │               │               │               │─────────────────────────────────►│
  │               │               │               │               │                │
  │               │               │               │◄──────────────────────Account────│
  │               │               │               │               │                │
  │               │               │               │  Transfer($100)                │
  │               │               │               ├──────────────►│                │
  │               │               │               │               │                │
  │               │               │               │   Validate    │                │
  │               │               │               │   Debit()     │                │
  │               │               │               │   Credit()    │                │
  │               │               │               │   Emit event  │                │
  │               │               │               │               │                │
  │               │               │               │◄──────────────┤                │
  │               │               │               │  Result.OK    │                │
  │               │               │               │               │                │
  │               │               │               │ SaveChangesWithOutbox()        │
  │               │               │               ├─────────────────────────────────►│
  │               │               │               │               │                │
  │               │               │               │◄─────────────────────────────────┤
  │               │               │               │  Entities saved + OutBox entry   │
  │               │               │               │               │                │
  │               │               │◄──────────────────────────────────────────────│
  │               │               │    TransferMoneyResult                      │
  │               │               │               │               │                │
  │               │        DomainEventsBehavior   │               │                │
  │               │        Publishes events       │               │                │
  │               │        to handlers            │               │                │
  │               │               │               │               │                │
  │               │        MoneyTransferredEventHandler          │                │
  │               │        └─ Logs transfer       │               │                │
  │               │        └─ Sends SignalR       │               │                │
  │               │               │               │               │                │
  │               │◄──────────────┤               │               │                │
  │               │ 200 OK        │               │               │                │
  │               │               │               │               │                │
  │◄──────────────┤               │               │               │                │
│ Response       │               │               │               │                │
│               │               │               │               │                │
│               │               │               │               │               │
│────────────────────────────────────────────────────────────────────────────────│

KEY INTERACTIONS:

1. Validation Phase (MediatR Pipeline)
   - FluentValidation checks all rules
   - Fails fast if invalid
   - Returns 400 Bad Request

2. Execution Phase (Handler)
   - Loads aggregates from repository
   - Calls domain methods (Transfer)
   - Domain logic emits events
   - Handles all business rules

3. Persistence Phase (SaveChangesWithOutbox)
   - Extracts events from aggregates
   - Creates OutBoxMessage entries
   - Saves both in atomic transaction
   - Returns persisted state

4. Event Dispatch (DomainEventsBehavior)
   - Publishes events to MediatR handlers
   - Handlers execute side effects
   - SignalR sends real-time updates
   - Response sent to client
```

---

## 2. Sequence Diagram: Outbox Event Processing

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│              SEQUENCE DIAGRAM: OUTBOX EVENT PROCESSING (Background)                   │
└────────────────────────────────────────────────────────────────────────────────────────┘

         Time
         (T+0s)

Database    BackgroundService    OutboxProcessor    MediatR    EventHandlers    SignalR
    │             │                    │               │            │              │
    │ [OutBox     │                    │               │            │              │
    │  entry      │                    │               │            │              │
    │  stored]    │                    │               │            │              │
    │             │                    │               │            │              │
    │             │ (every 30 seconds) │               │            │              │
    │             │◄─── Timer Elapsed ─┤               │            │              │
    │             │                    │               │            │              │
    │             │ Execute polling    │               │            │              │
    │             ├───────────────────►│               │            │              │
    │             │                    │               │            │              │
    │◄─────────────────────────────────┤               │            │              │
    │ SELECT *    │                    │               │            │              │
    │ FROM OutBoxMessages              │               │            │              │
    │ WHERE ProcessedOn IS NULL        │               │            │              │
    │ LIMIT 20    │                    │               │            │              │
    │             │                    │               │            │              │
    │─────────────►│ [OutBoxMessage]   │               │            │              │
    │             │                    │               │            │              │
    │             │ [Found 1 message]  │               │            │              │
    │             │                    │               │            │              │
    │             │ ProcessAsync()     │               │            │              │
    │             ├───────────────────►│               │            │              │
    │             │                    │               │            │              │
    │             │      Deserialize   │               │            │              │
    │             │      JSON → Event  │               │            │              │
    │             │      [MoneyTransf. │               │            │              │
    │             │       Event]       │               │            │              │
    │             │                    │               │            │              │
    │             │         Publish    │               │            │              │
    │             │         Event      ├──────────────►│            │              │
    │             │                    │  Publish     │            │              │
    │             │                    │  MoneyTransf.│            │              │
    │             │                    │  Event       ├───────────►│              │
    │             │                    │              │  Handler   │              │
    │             │                    │              │  Executes  ├─────────────►│
    │             │                    │              │            │  Broadcast  │
    │             │                    │              │            │  via        │
    │             │                    │              │            │  SignalR    │
    │             │                    │              │            │              │
    │             │                    │              │◄──────────────────────────┤
    │             │                    │              │  Success   │ [Clients    │
    │             │                    │              │            │  notified]  │
    │             │                    │              │            │              │
    │◄─────────────────────────────────────────────────────────────┤              │
    │ UPDATE OutBoxMessages            │              │            │              │
    │ SET ProcessedOn = NOW()          │              │            │              │
    │ WHERE Id = @id                   │              │            │              │
    │                                  │              │            │              │
    │─────────────────────────────────►│              │            │              │
    │ [Update confirmed]               │              │            │              │
    │                                  │              │            │              │
    │                                  ├──────────────┤            │              │
    │                                  │ Loop: Check  │            │              │
    │                                  │ next message │            │              │
    │                                  │              │            │              │
    │ (T+30s, next cycle)              │              │            │              │
    │                                  │              │            │              │
    │                                  │ Timer again  │            │              │
    │◄─────────────────────────────────┤              │            │              │
    │ SELECT * (no more messages)      │              │            │              │
    │ ProcessedOn IS NULL              │              │            │              │
    │                                  │              │            │              │
    │─────────────────────────────────►│ [0 rows]     │            │              │
    │ [No messages to process]         │              │            │              │
    │ Wait 30s                         │              │            │              │
    │                                  │              │            │              │

ERROR SCENARIO (RetryCount < 3):

    │             │                    │               │            │
    │             │ ProcessAsync()     │               │            │
    │             ├───────────────────►│               │            │
    │             │                    │               │            │
    │             │  Deserialize OK    │               │            │
    │             │  Publish ERROR!    ├──────────────►│            │
    │             │  Exception thrown  │  Exception    │            │
    │             │                    │◄──────────────┤            │
    │             │◄───────────────────┤ Failed        │            │
    │             │  Error logged      │               │            │
    │             │                    │               │            │
    │◄─────────────────────────────────┤               │            │
    │ UPDATE OutBoxMessages            │               │            │
    │ SET RetryCount = RetryCount + 1  │               │            │
    │ Error = 'Exception message'      │               │            │
    │ WHERE Id = @id                   │               │            │
    │                                  │               │            │
    │─────────────────────────────────►│               │            │
    │ [Marked for retry]               │               │            │
    │                                  │               │            │
    │ (T+30s, next cycle)              │               │            │
    │◄─────────────────────────────────┤               │            │
    │ SELECT * WHERE ProcessedOn IS NULL AND RetryCount < 3
    │─────────────────────────────────►│               │            │
    │ [Message returned again]         │               │            │
    │ [Will retry up to 3 times]       │               │            │
```

---

## 3. Sequence Diagram: Account Creation with Events

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│            SEQUENCE DIAGRAM: CREATE ACCOUNT WITH DOMAIN EVENTS                        │
└────────────────────────────────────────────────────────────────────────────────────────┘

Client          Controller      MediatR         Handler         Domain          Repository
  │                 │             │               │               │                │
  │ POST /accounts  │             │               │               │                │
  │ with customer,  │             │               │               │                │
  │ account type    │             │               │               │                │
  ├────────────────►│             │               │               │                │
  │                 │             │               │               │                │
  │                 │ CreateAccountCommand        │               │                │
  │                 ├────────────►│               │               │                │
  │                 │             │               │               │                │
  │                 │     Validate:               │               │                │
  │                 │     - CustomerId exists?    │               │                │
  │                 │     - AccountNumber unique? │               │                │
  │                 │     - Amount not negative?  │               │                │
  │                 │             │               │               │                │
  │                 │     ValidationBehavior     │               │                │
  │                 │     ├─ Run validators      │               │                │
  │                 │     └─ Pass / Fail         │               │                │
  │                 │             │               │               │                │
  │                 │             ├──────────────────────────────►│                │
  │                 │             │ CreateAccountCommandHandler  │                │
  │                 │             │               │               │                │
  │                 │             │               │ GetCustomer   │
  │                 │             │               ├───────────────────────────────►│
  │                 │             │               │               │                │
  │                 │             │               │◄───────Customer────────────────│
  │                 │             │               │               │                │
  │                 │             │               │ Account.Create(                │
  │                 │             │               │    customerId,                 │
  │                 │             │               │    accountNumber,              │
  │                 │             │               │    accountType,                │
  │                 │             │               │    initialDeposit)             │
  │                 │             │               ├──────────────►│                │
  │                 │             │               │               │                │
  │                 │             │               │    Validate   │                │
  │                 │             │               │    Initialize │                │
  │                 │             │               │    Balance    │                │
  │                 │             │               │    Emit Event │                │
  │                 │             │               │    account    │                │
  │                 │             │               │    .AddDomain │                │
  │                 │             │               │    Event()    │                │
  │                 │             │               │               │                │
  │                 │             │               │◄──────Account──────────────────┤
  │                 │             │               │ [with event]  │                │
  │                 │             │               │               │                │
  │                 │             │               │ SaveChangesWithOutboxAsync()  │
  │                 │             │               ├───────────────────────────────►│
  │                 │             │               │               │                │
  │                 │             │               │   Extract event               │
  │                 │             │               │   Create OutBoxMessage        │
  │                 │             │               │   SaveChangesAsync()          │
  │                 │             │               │   (atomic tx)                 │
  │                 │             │               │               │                │
  │                 │             │               │◄───────────Saved─────────────│
  │                 │             │               │ [Account + OutBox entry]     │
  │                 │             │               │               │                │
  │                 │             │◄──────────────────────────Account────────────│
  │                 │             │ CreateAccountResult            │                │
  │                 │             │               │               │                │
  │                 │     DomainEventsBehavior    │               │                │
  │                 │     ├─ Get aggregates      │               │                │
  │                 │     ├─ Publish events     │               │                │
  │                 │     └─ All handlers exec  │               │                │
  │                 │             │               │               │                │
  │                 │     AccountCreatedEventHandler             │                │
  │                 │     ├─ Logs creation      │               │                │
  │                 │     └─ Sends notifications│               │                │
  │                 │             │               │               │                │
  │                 │ Map to DTO  │               │               │                │
  │                 │◄────────────┤               │               │                │
  │                 │ 201 Created │               │               │                │
  │                 │ + AccountDto│               │               │                │
  │                 │             │               │               │                │
  │◄────────────────┤             │               │               │                │
│ Response received │             │               │               │                │
│ Account created   │             │               │               │                │
│ with AccountId    │             │               │               │                │
│               │               │               │               │                │
│               │               │               │               │                │
└───────────────────────────────────────────────────────────────────────────────────┘

INTERNAL STATE AT EACH STEP:

Step 1: Account.Create() called
┌─────────────────────────────────┐
│ Account Instance (In Memory)    │
├─────────────────────────────────┤
│ - AccountId: Generated          │
│ - Balance: 5000                 │
│ - IsActive: true                │
│ - _domainEvents: [              │
│   AccountCreatedEvent { ... }   │
│ ]                               │
└─────────────────────────────────┘

Step 2: SaveChangesWithOutboxAsync() executes
┌─────────────────────────────────┐
│ Accounts Table (DB)             │
├─────────────────────────────────┤
│ AccountId │ Balance │ ...       │
│ ACC-123   │  5000   │           │ ← Inserted
└─────────────────────────────────┘

┌──────────────────────────────────┐
│ OutBoxMessages Table (DB)        │
├──────────────────────────────────┤
│ Id  │ Type    │ Content │Proc... │
│ msg │ Account │ {JSON}  │ NULL   │ ← Inserted
│ -1  │ Created │        │        │
└──────────────────────────────────┘

Step 3: Background Service processes OutBox (T+30s)
┌──────────────────────────────────┐
│ OutBoxMessages Table (DB)        │
├──────────────────────────────────┤
│ Id  │ Type    │ Content │Proc... │
│ msg │ Account │ {JSON}  │ 2025.. │ ← Updated
│ -1  │ Created │        │ 01:16  │
└──────────────────────────────────┘
```

---

## 4. Component Diagram (Architecture Overview)

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          COMPONENT DIAGRAM - DEPLOYMENT                               │
└────────────────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────────────┐
│                              CLIENT LAYER                                             │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────┐ │
│  │  Web Browser    │  │  Mobile App     │  │  gRPC Client     │  │  Admin CLI   │ │
│  │  (React/Vue)    │  │  (iOS/Android)  │  │  (.NET/Java)     │  │  (Console)   │ │
│  └─────────────────┘  └─────────────────┘  └──────────────────┘  └──────────────┘ │
│         │                    │                    │                    │           │
│         └────────────────────┼────────────────────┼────────────────────┘           │
│                              │                    │                                 │
└──────────────────────────────┼────────────────────┼─────────────────────────────────┘
                               │                    │
                         HTTP/JSON            gRPC/Protobuf
                         REST API              (HTTP/2)
                               │                    │
                               ▼                    ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                           API GATEWAY LAYER                                           │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │                     CoreBankingTest.API (Kestrel)                           │  │
│  │  Port: 5037 (REST/HTTP1)  │  Port: 7288 (gRPC/HTTP2)                       │  │
│  │                                                                              │  │
│  │  ┌─────────────────┐      ┌──────────────────────────────────────────────┐ │  │
│  │  │ REST Controllers │      │  gRPC Services                               │ │  │
│  │  ├─────────────────┤      ├──────────────────────────────────────────────┤ │  │
│  │  │ Account         │      │ AccountGrpcService                           │ │  │
│  │  │ Customer        │      │ EnhancedAccountGrpcService                   │ │  │
│  │  │ Transaction     │      │                                              │ │  │
│  │  └─────────────────┘      └──────────────────────────────────────────────┘ │  │
│  │         │                                  │                              │  │
│  │         └──────────────────┬───────────────┘                              │  │
│  │                            │                                              │  │
│  │  ┌───────────────────────────────────────────────────────────────────┐   │  │
│  │  │  MediatR Pipeline (Command/Query Processing)                     │   │  │
│  │  │  ├─ ValidationBehavior                                           │   │  │
│  │  │  ├─ LoggingBehavior                                              │   │  │
│  │  │  ├─ DomainEventsBehavior                                         │   │  │
│  │  │  ├─ Handlers (Accounts, Customers, Transactions)                │   │  │
│  │  │  └─ Event Handlers (AccountCreated, MoneyTransferred, etc)      │   │  │
│  │  └────────────────────┬─────────────────────────────────────────────┘   │  │
│  │                       │                                                  │  │
│  │  ┌────────────────────┴──────────────────────────────────────────────┐  │  │
│  │  │  AutoMapper (DTO Mapping)                                        │  │  │
│  │  ├────────────────────────────────────────────────────────────────┤  │  │
│  │  │  EntityMapper: Entity → DTO  │  DtoMapper: DTO → Entity       │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  │                       │                                                  │  │
│  │  ┌────────────────────┴──────────────────────────────────────────────┐  │  │
│  │  │  SignalR Hubs (Real-time Communication)                          │  │  │
│  │  ├────────────────────────────────────────────────────────────────┤  │  │
│  │  │  NotificationHub          │ TransactionHub                    │  │  │
│  │  │  EnhancedNotificationHub  │                                   │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  │                       │                                                  │  │
│  │  ┌────────────────────┴──────────────────────────────────────────────┐  │  │
│  │  │  Global Exception Handler Middleware                            │  │  │
│  │  │  ├─ Catches all exceptions                                      │  │  │
│  │  │  ├─ Logs errors                                                 │  │  │
│  │  │  └─ Returns standardized error responses                        │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                        │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└──────────────────────┬──────────────────────────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
        ▼              ▼              ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                       APPLICATION LAYER (CoreBankingTest.APP)                        │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │  Commands (Use Cases - Write Operations)                                   │   │
│  │  ├─ CreateAccountCommand    ├─ CreateCustomerCommand                       │   │
│  │  ├─ TransferMoneyCommand    ├─ WithdrawMoneyCommand                        │   │
│  │  ├─ DepositMoneyCommand     └─ [More Commands...]                          │   │
│  │  └─ Handlers process commands and emit domain events                       │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                     │                                              │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │  Queries (Use Cases - Read Operations)                                     │   │
│  │  ├─ GetAccountQuery         ├─ GetAccountsQuery                            │   │
│  │  ├─ GetCustomerQuery        ├─ GetTransactionsQuery                        │   │
│  │  └─ Handlers return DTOs without modifying state                           │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                     │                                              │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │  Validators (FluentValidation)                                             │   │
│  │  ├─ CreateAccountCommandValidator  ├─ CreateCustomerCommandValidator       │   │
│  │  ├─ TransferMoneyCommandValidator  └─ [More Validators...]                │   │
│  │  └─ Validate all inputs before command execution                           │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                     │                                              │
└────────────────────────────────────┬────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                        DOMAIN LAYER (CoreBankingTest.CORE)                           │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  Aggregates (Domain Objects)                                               │  │
│  │  ├─ Customer (AggregateRoot)      ├─ Account (AggregateRoot)              │  │
│  │  │  └─ Domain Events              │  └─ Domain Events                     │  │
│  │  │                                │                                       │  │
│  │  └─ Transaction (Entity)          └─ Encapsulates business logic          │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                     │                                              │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  Value Objects (Immutable)                                                 │  │
│  │  ├─ CustomerId, AccountId, TransactionId (Strongly-typed GUIDs)           │  │
│  │  ├─ AccountNumber (Validated string)                                       │  │
│  │  ├─ Money (Amount + Currency)                                              │  │
│  │  └─ Result (Success/Failure outcome)                                       │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                     │                                              │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  Domain Events                                                              │  │
│  │  ├─ AccountCreatedEvent    ├─ MoneyTransferredEvent                        │  │
│  │  ├─ InsufficientFundsEvent └─ [More Events...]                             │  │
│  │  └─ Record state changes                                                   │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                     │                                              │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  Enums & Interfaces                                                         │  │
│  │  ├─ AccountType (Checking, Savings, etc)                                    │  │
│  │  ├─ TransactionType (Deposit, Withdrawal, Transfer)                        │  │
│  │  ├─ ISoftDelete, IDomainEvent, IRepository (Contracts)                     │  │
│  │  └─ Domain contracts                                                        │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE LAYER (CoreBanking.Infrastructure)                  │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  Data Access (EF Core & Repositories)                                      │  │
│  │  ├─ BankingDbContext                                                        │  │
│  │  │  ├─ DbSet<Customer>, DbSet<Account>, DbSet<Transaction>                │  │
│  │  │  ├─ SaveChangesWithOutboxAsync() - Outbox Pattern Implementation        │  │
│  │  │  └─ Global Query Filters (Soft Delete)                                 │  │
│  │  │                                                                          │  │
│  │  ├─ IAccountRepository → AccountRepository                                 │  │
│  │  ├─ ICustomerRepository → CustomerRepository                               │  │
│  │  ├─ ITransactionRepository → TransactionRepository                         │  │
│  │  └─ Query with includes, filtering, eager loading                          │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                     │                                              │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  Outbox Pattern Services                                                    │  │
│  │  ├─ OutboxBackgroundService (HostedService)                                │  │
│  │  │  └─ Polls every 30 seconds                                              │  │
│  │  │                                                                          │  │
│  │  ├─ IOutboxMessageProcessor → OutboxMessageProcessor                       │  │
│  │  │  ├─ Deserializes JSON → Domain Events                                  │  │
│  │  │  ├─ Publishes via MediatR                                               │  │
│  │  │  ├─ Handles retries (max 3)                                             │  │
│  │  │  └─ Marks processed                                                     │  │
│  │  │                                                                          │  │
│  │  ├─ DomainEventDispatcher (IDomainEventDispatcher)                         │  │
│  │  │  ├─ Extracts events from aggregates                                     │  │
│  │  │  ├─ Publishes to MediatR handlers                                       │  │
│  │  │  └─ Clears events after publishing                                      │  │
│  │  │                                                                          │  │
│  │  └─ UnitOfWork (IUnitOfWork)                                               │  │
│  │     └─ Single transaction save point                                        │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                     │                                              │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  External Services                                                          │  │
│  │  ├─ HttpClients (CreditScoringServiceClient)                               │  │
│  │  ├─ Resilience Policies (Polly - Retry, CircuitBreaker)                    │  │
│  │  └─ External API integration with fault tolerance                          │  │
│  └──────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                             DATABASE LAYER                                           │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│  ┌──────────────────────────────────────────────────────────────────────────────┐  │
│  │  SQL Server Database                                                         │  │
│  │  ├─ Customers Table              ├─ Accounts Table                          │  │
│  │  │  ├─ CustomerId (PK)           │  ├─ AccountId (PK)                      │  │
│  │  │  ├─ FirstName, LastName       │  ├─ AccountNumber (Unique)              │  │
│  │  │  ├─ Email (Unique)            │  ├─ Balance (Amount)                    │  │
│  │  │  ├─ IsDeleted, DeletedAt      │  ├─ Currency                            │  │
│  │  │  └─ [Other fields]            │  ├─ CustomerId (FK)                     │  │
│  │  │                               │  ├─ RowVersion (Concurrency Token)      │  │
│  │  ├─ Transactions Table           │  └─ IsDeleted, DeletedAt                │  │
│  │  │  ├─ TransactionId (PK)        │                                         │  │
│  │  │  ├─ AccountId (FK)            ├─ OutboxMessages Table                   │  │
│  │  │  ├─ Type (Enum)               │  ├─ Id (PK)                             │  │
│  │  │  ├─ Amount, Currency          │  ├─ Type (Event class name)             │  │
│  │  │  ├─ Description, Reference    │  ├─ Content (JSON)                      │  │
│  │  │  └─ Timestamp                 │  ├─ OccurredOn, ProcessedOn             │  │
│  │  │                               │  ├─ RetryCount, Error                   │  │
│  │  │  Indexes: Account (Perf)      │  └─ [Outbox entries]                    │  │
│  │  │           Type, Date (Query)  │                                         │  │
│  │  │                               │  Migrations:                            │  │
│  │  │                               │  ├─ Initial schema                      │  │
│  │  │                               │  ├─ Add OutboxMessages                  │  │
│  │  │                               │  └─ [Versioned migrations]              │  │
│  │  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Deployment Diagram (Runtime Architecture)

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                         DEPLOYMENT DIAGRAM - RUNTIME TOPOLOGY                         │
└────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                           CLIENT TIER (End Users)                                       │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                         │
│  ┌─────────────────────────┐      ┌──────────────────────┐      ┌──────────────────┐  │
│  │  Web Browser            │      │  Mobile App          │      │  gRPC Client     │  │
│  │  (React/Angular/Vue)    │      │  (iOS/Android)       │      │  (.NET/Java)     │  │
│  │                         │      │                      │      │                  │  │
│  │  Connects via:          │      │  Connects via:       │      │  Connects via:   │  │
│  │  - HTTP/REST            │      │  - HTTP/REST         │      │  - gRPC/HTTP2    │  │
│  │  - SignalR WebSocket    │      │  - SignalR WebSocket │      │  - Protocol Buf. │  │
│  └─────────────────────────┘      └──────────────────────┘      └──────────────────┘  │
│         │                                 │                             │              │
│         └─────────────────────────────────┼─────────────────────────────┘              │
│                                           │                                            │
└───────────────────────────────────────────┼────────────────────────────────────────────┘
                                            │
                                  Internet / Network
                                            │
┌───────────────────────────────────────────┼────────────────────────────────────────────┐
│                                           │                                            │
│                             API SERVER TIER                                           │
│                                           │                                            │
├───────────────────────────────────────────┼────────────────────────────────────────────┤
│                                           │                                            │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │  CoreBankingTest.API (ASP.NET Core - Kestrel Server)                           │  │
│  │  Deployment: Docker Container / Azure App Service / On-Premise Server          │  │
│  │                                                                                 │  │
│  │  Kestrel Ports:                                                                │  │
│  │  ├─ Port 5037: HTTP/1.1 (REST API, Swagger)                                   │  │
│  │  │  └─ Protocol: HTTP                                                         │  │
│  │  │  └─ Load Balancer / Reverse Proxy: Nginx / IIS                            │  │
│  │  │                                                                             │  │
│  │  └─ Port 7288: HTTP/2 (gRPC Services)                                         │  │
│  │     └─ Protocol: gRPC / Protocol Buffers                                      │  │
│  │     └─ Load Balancer: gRPC-aware LB                                           │  │
│  │                                                                                │  │
│  │  Running Components:                                                           │  │
│  │  ├─ REST Controllers (AccountController, CustomerController)                  │  │
│  │  ├─ gRPC Services (AccountGrpcService)                                        │  │
│  │  ├─ SignalR Hubs (NotificationHub, TransactionHub)                            │  │
│  │  ├─ MediatR Pipeline with Behaviors                                           │  │
│  │  ├─ AutoMapper (Entity ↔ DTO)                                                 │  │
│  │  ├─ GlobalExceptionHandler Middleware                                         │  │
│  │  └─ Swagger/OpenAPI UI                                                        │  │
│  │                                                                                │  │
│  │  Hosted Services (Background Tasks):                                           │  │
│  │  ├─ OutboxBackgroundService                                                   │  │
│  │  │  └─ Polls DB every 30s for unprocessed events                             │  │
│  │  │                                                                            │  │
│  │  └─ TransactionBroadcastService                                              │  │
│  │     └─ Broadcasts updates via SignalR                                        │  │
│  │                                                                                │  │
│  │  Configuration:                                                               │  │
│  │  ├─ appsettings.json (Dev/Prod configs)                                      │  │
│  │  ├─ User Secrets (ConnectionStrings, API Keys)                              │  │
│  │  └─ Environment Variables                                                     │  │
│  │                                                                                │  │
│  └─────────────────────────────────────────────────────────────────────────────────┘  │
│         │                           │                            │                    │
│         │                           │                            │                    │
│         │ EF Core DbContext         │ Polly HttpClient          │ External APIs      │
│         │ Connection Pooling        │ (Retry/CircuitBreaker)     │                    │
│         ▼                           ▼                            ▼                    │
│  ┌──────────────────────────────────────────────────────────────────────────────┐   │
│  │  SERVICE LAYER                                                               │   │
│  │  ├─ DomainEventDispatcher (Publishes events to MediatR handlers)            │   │
│  │  ├─ OutboxMessageProcessor (Deserializes and processes outbox messages)    │   │
│  │  ├─ UnitOfWork (Coordinates repository operations)                          │   │
│  │  ├─ CreditScoringServiceClient (HttpClient to external service)            │   │
│  │  └─ Resilience Policies (Polly: Retry, Timeout, CircuitBreaker)            │   │
│  └──────────────────────────────────────────────────────────────────────────────┘   │
│         │                                                                            │
│         │ SQL Server Connection String                                              │
│         │ (With connection pooling)                                                 │
│         ▼                                                                            │
└──────────────────────────────────────────────────────────────────────────────────────┘
         │
         │ TCP 1433 (SQL Server)
         │ or 3306 (MySQL)
         │
┌────────┼────────────────────────────────────────────────────────────────────────────────┐
│        │                      DATABASE TIER                                            │
├────────┼────────────────────────────────────────────────────────────────────────────────┤
│        │                                                                                │
│        ▼                                                                                │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │  SQL Server Instance / Azure SQL Database / RDS                                 │  │
│  │  ├─ Database: CoreBankingDB                                                    │  │
│  │  │                                                                              │  │
│  │  │  Tables:                                                                    │  │
│  │  │  ├─ Customers                                                              │  │
│  │  │  ├─ Accounts                                                               │  │
│  │  │  ├─ Transactions                                                           │  │
│  │  │  ├─ OutboxMessages ← Stores unprocessed domain events                     │  │
│  │  │  └─ [Migration History]                                                   │  │
│  │  │                                                                              │  │
│  │  │  Indexes:                                                                   │  │
│  │  │  ├─ PK on AccountId, CustomerId, TransactionId                            │  │
│  │  │  ├─ Unique on Email (Customers)                                           │  │
│  │  │  ├─ Unique on AccountNumber (Accounts)                                    │  │
│  │  │  ├─ FK indexes on CustomerId (Accounts → Customers)                       │  │
│  │  │  ├─ FK indexes on AccountId (Transactions → Accounts)                     │  │
│  │  │  └─ Index on ProcessedOn (OutboxMessages) ← For polling                  │  │
│  │  │                                                                              │  │
│  │  │  Queries:                                                                   │  │
│  │  │  ├─ EF Core migrations applied                                            │  │
│  │  │  ├─ Global query filters (IsDeleted = false)                              │  │
│  │  │  ├─ Concurrency tokens (RowVersion on Accounts)                           │  │
│  │  │  └─ Polling query: SELECT * FROM OutboxMessages WHERE ProcessedOn IS NULL│  │
│  │  │                                                                              │  │
│  │  │  Storage:                                                                   │  │
│  │  │  ├─ Data files (.mdf)                                                      │  │
│  │  │  ├─ Log files (.ldf)                                                       │  │
│  │  │  ├─ Backup snapshots                                                       │  │
│  │  │  └─ Transaction log (for ACID guarantees)                                 │  │
│  │  │                                                                              │  │
│  │  │  Monitoring:                                                                │  │
│  │  │  ├─ Query performance metrics                                              │  │
│  │  │  ├─ Slow query logs                                                        │  │
│  │  │  ├─ Deadlock detection                                                     │  │
│  │  │  └─ Connection pool stats                                                  │  │
│  │  │                                                                              │  │
│  │  └─ Replication / Backup Strategy:                                            │  │
│  │     ├─ Primary DB (Read/Write)                                                │  │
│  │     ├─ Replicas (Read-only for reporting)                                     │  │
│  │     └─ Automated backups (daily)                                              │  │
│  │                                                                                │  │
│  └──────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘


INTERACTION FLOWS:

REST API Flow:
Client → (HTTP POST) → Nginx Reverse Proxy → Kestrel:5037 → Controller → MediatR → Repository → SQL Server

gRPC Flow:
Client → (gRPC Protobuf) → LB (gRPC-aware) → Kestrel:7288 → gRPC Service → MediatR → Repository → SQL Server

Real-time Update Flow:
Client → (WebSocket) → Kestrel:5037 → SignalR Hub ← MediatR Event Handlers ← Outbox Background Service ← SQL Server (OutboxMessages)

Outbox Processing Flow (Background):
Timer (30s) → OutboxBackgroundService → Query DB → OutboxMessageProcessor → Deserialize JSON → MediatR Publish → Handlers → Update UI via SignalR

External Service Call (with Resilience):
API → Polly HttpClient (Retry Policy) → CreditScoringServiceClient → External API (with timeout, circuit breaker)
```

