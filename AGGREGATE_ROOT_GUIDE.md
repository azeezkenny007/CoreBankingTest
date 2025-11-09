# Aggregate Root Pattern Guide

## Overview

The **Aggregate Root** is a Domain-Driven Design (DDD) pattern that enforces business transaction boundaries and manages domain events. In your banking system, the `Account` entity serves as an aggregate root that encapsulates all business logic related to accounts and their operations.

---

## Architecture Components

### 1. **IAggregateRoot Interface** (Contract)

```csharp path=/CoreBankingTest.CORE/Interfaces/IAggregateRoot.cs start=9
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
```

**Purpose:**
- Defines the contract that all aggregate roots must follow
- Ensures consistency across different aggregate roots in the system
- Provides a way for the infrastructure layer to access domain events

**Key Members:**
- `DomainEvents` - Read-only collection of events that occurred during the aggregate's lifetime
- `ClearDomainEvents()` - Clears events after they've been published

---

### 2. **AggregateRoot<TId> Abstract Base Class**

```csharp path=/CoreBankingTest.CORE/Common/AggregateRoot.cs start=11
public abstract class AggregateRoot<TId> where TId : notnull
{
    [NotMapped]
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**Design Decisions:**

| Feature | Why? |
|---------|------|
| `[NotMapped]` attribute | Prevents Entity Framework from persisting events to main tables (events go to outbox) |
| `TId` generic parameter | Each aggregate has a unique identifier type (e.g., `AccountId`) |
| `_domainEvents` private list | Encapsulation - prevents external code from directly manipulating events |
| `IReadOnlyCollection` exposure | Forces immutability - consumers can only read, not modify |
| `protected AddDomainEvent()` | Only the aggregate and its subclasses can raise events |

---

## How It Works in Your System

### Step 1: Account Creation (Aggregate Creation)

```csharp path=/CoreBankingTest.CORE/Entities/Account.cs start=50
public static Account Create(
    CustomerId customerId,
    AccountNumber accountNumber,
    AccountType accountType,
    Money initialDeposit)
{
    // Domain validation
    if (initialDeposit.Amount < 0)
        throw new InvalidOperationException("Initial balance cannot be negative");

    if (initialDeposit.Amount > 1000000)
        throw new InvalidOperationException("Initial deposit too large");

    // Create account using private constructor
    var account = new Account(
        accountNumber: accountNumber,
        accountType: accountType,
        customerId: customerId
    )
    {
        Balance = initialDeposit
    };

    // Raise domain event if needed
    account.AddDomainEvent(new AccountCreatedEvent(
        accountId: account.AccountId,
        accountNumber: account.AccountNumber,
        customerId: account.CustomerId,
        accountType: account.AccountType,
        initialDeposit: account.Balance
    ));

    return account;
}
```

**What Happens:**
1. Validates business rules (balance limits)
2. Creates the account instance via private constructor
3. **Raises an `AccountCreatedEvent`** - This event is stored in `_domainEvents`
4. Returns the aggregate with the event attached (not yet published)

---

### Step 2: Business Operations (Event Generation)

```csharp path=/CoreBankingTest.CORE/Entities/Account.cs start=139
public Result Transfer(Money amount, Account destination, string reference, string description)
{
    // Validations...
    if (Balance.Amount < amount.Amount)
    {
        // Raise insufficient funds event
        _domainEvents.Add(new InsufficientFundsEvent(
            AccountNumber, amount, Balance, "Transfer"));

        throw new InsufficientFundsException(amount.Amount, Balance.Amount);
    }

    // Execute the transfer atomically
    var debitResult = Debit(amount, $"Transfer to {destination.AccountNumber}", reference);
    if (!debitResult.IsSuccess)
        return debitResult;

    var creditResult = destination.Credit(amount, $"Transfer from {AccountNumber}", reference);
    if (!creditResult.IsSuccess)
        return creditResult;

    // Raise money transferred event
    var transactionId = TransactionId.Create();
    _domainEvents.Add(new MoneyTransferredEvent(
        transactionId, AccountNumber, destination.AccountNumber, amount, reference));

    return Result.Success();
}
```

**Key Points:**
- Business operations modify the aggregate's state (`Balance -= amount`)
- **Important operations raise domain events** to signal what happened
- Events are accumulated in `_domainEvents` during the operation
- If the operation fails, events still raised (e.g., `InsufficientFundsEvent`) for audit trail

---

### Step 3: Domain Event Definition

```csharp path=/CoreBankingTest.CORE/Events/AccountCreatedEvent.cs start=14
public record AccountCreatedEvent : DomainEvent
{
    public AccountId AccountId { get; }
    public AccountNumber AccountNumber { get; }
    public CustomerId CustomerId { get; }
    public AccountType AccountType { get; }
    public Money InitialDeposit { get; }

    public AccountCreatedEvent(AccountId accountId, AccountNumber accountNumber, 
        CustomerId customerId, AccountType accountType, Money initialDeposit)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        CustomerId = customerId;
        AccountType = accountType;
        InitialDeposit = initialDeposit;
    }
}
```

**Base Class - DomainEvent:**

```csharp path=/CoreBankingTest.CORE/Common/DomainEvent.cs start=11
public abstract record DomainEvent : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}
```

**Why `record` type?**
- Immutable by design
- Built-in equality comparison
- Perfect for events (should never change once created)

---

### Step 4: Persistence & Event Dispatching

#### In DbContext (Outbox Pattern):

```csharp path=/CoreBanking.Infrastructure/Data/BankingDbContext.cs start=188
public async Task SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
{
    // Convert domain events to outbox messages
    var events = ChangeTracker.Entries<AggregateRoot<AccountId>>()
        .SelectMany(x => x.Entity.DomainEvents)
        .Select(domainEvent => new OutBoxMessage
        {
            Id = Guid.NewGuid(),
            Type = domainEvent.GetType().Name,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredOn = domainEvent.OccurredOn
        })
        .ToList();

    // Clear domain events from aggregates
    ChangeTracker.Entries<AggregateRoot<AccountId>>()
        .ToList()
        .ForEach(entry => entry.Entity.ClearDomainEvents());

    // Save changes in single transaction
    await base.SaveChangesAsync(cancellationToken);

    // Add outbox messages after saving
    if (events.Any())
    {
        await OutboxMessages.AddRangeAsync(events, cancellationToken);
        await base.SaveChangesAsync(cancellationToken);
    }
}
```

**Process:**
1. **Track all aggregates** with unsaved domain events
2. **Serialize events** to JSON and store in `OutboxMessages` table
3. **Clear events** from the aggregate (prevents re-publishing)
4. **Save to database** in a single transaction (guaranteed consistency)

---

#### Event Dispatcher (Publishing):

```csharp path=/CoreBanking.Infrastructure/Services/DomainEventDispatcher.cs start=31
public async Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default)
{
    var domainEntities = _context.ChangeTracker
        .Entries<IAggregateRoot>()
        .Where(x => x.Entity.DomainEvents.Any())
        .ToList();

    var domainEvents = domainEntities
        .SelectMany(x => x.Entity.DomainEvents)
        .ToList();

    foreach (var domainEvent in domainEvents)
    {
        _logger.LogInformation("Dispatching domain event: {EventType}", domainEvent.GetType().Name);
        await _publisher.Publish(domainEvent, cancellationToken);
    }

    domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());
}
```

**What Happens:**
1. **Finds all aggregates** with pending domain events
2. **Publishes each event** via MediatR (which routes to appropriate handlers)
3. **Clears events** to prevent duplicate publishing

---

## Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER                            │
│  (e.g., CreateAccountCommand Handler)                           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   DOMAIN LAYER (CORE)                           │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Account Aggregate Root                                   │  │
│  │  • Validates business rules                               │  │
│  │  • Modifies state (Balance, etc.)                         │  │
│  │  • Raises domain events via AddDomainEvent()              │  │
│  │    └─► Events stored in _domainEvents collection         │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              INFRASTRUCTURE LAYER (DATA ACCESS)                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  SaveChangesWithOutboxAsync()                             │  │
│  │  1. Finds all aggregates with DomainEvents               │  │
│  │  2. Serializes events → OutboxMessages table             │  │
│  │  3. Calls ClearDomainEvents()                            │  │
│  │  4. Saves ALL to database in ONE transaction             │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  DomainEventDispatcher.DispatchDomainEventsAsync()        │  │
│  │  1. Reads events from OutboxMessages                     │  │
│  │  2. Publishes via MediatR                                │  │
│  │  3. Routes to Event Handlers                             │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   EVENT HANDLERS                                │
│  (Could be in same or different services)                       │
│  • SendNotificationHandler                                      │
│  • UpdateAuditLogHandler                                        │
│  • TriggerWorkflowHandler                                       │
│  etc.                                                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Real-World Example: Creating an Account

### 1. **Command Handler** (Application Layer)

```csharp path=null start=null
public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
{
    public async Task<Result<AccountDto>> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        // 1. Get customer
        var customer = await _customerRepository.GetAsync(request.CustomerId, ct);
        
        // 2. Create aggregate (domain event raised internally)
        var account = Account.Create(
            customerId: customer.CustomerId,
            accountNumber: AccountNumber.Create(request.AccountNumber),
            accountType: request.AccountType,
            initialDeposit: new Money(request.InitialDeposit)
        );
        // ← Now account._domainEvents contains [AccountCreatedEvent]
        
        // 3. Save aggregate
        await _accountRepository.AddAsync(account, ct);
        
        // 4. Persist to database AND convert events to outbox
        await _dbContext.SaveChangesWithOutboxAsync(ct);
        
        return Result.Success(AccountMapper.ToDto(account));
    }
}
```

### 2. **What Happens in Database**

**accounts table:**
```
| AccountId | AccountNumber | Balance | CustomerId | ...
| abc123    | 1000000001    | 5000.00 | cust001    | ...
```

**outbox_messages table:**
```
| Id   | Type                 | Content                              | OccurredOn
| xyz1 | AccountCreatedEvent  | {"AccountId":"abc123",...}          | 2025-11-09T00:50:34Z
```

### 3. **Event Dispatcher Publishes**

```csharp path=null start=null
// MediatR routes to all registered handlers
await _publisher.Publish(new AccountCreatedEvent(...));

// Handlers receive the event:
public class SendAccountCreationNotificationHandler 
    : INotificationHandler<AccountCreatedEvent>
{
    public async Task Handle(AccountCreatedEvent notification, CancellationToken ct)
    {
        // Send welcome email
        await _emailService.SendWelcomeEmailAsync(
            notification.CustomerId, 
            notification.AccountNumber, 
            ct);
    }
}

public class LogAccountCreationHandler 
    : INotificationHandler<AccountCreatedEvent>
{
    public async Task Handle(AccountCreatedEvent notification, CancellationToken ct)
    {
        // Log to audit trail
        await _auditLog.LogAsync(
            "Account created", 
            notification.OccurredOn, 
            ct);
    }
}
```

---

## Key Benefits

| Benefit | Why It Matters |
|---------|---|
| **Encapsulation** | Business logic stays within the aggregate, external code can't break invariants |
| **Event Sourcing** | Complete audit trail of everything that happened |
| **Loose Coupling** | Handlers don't need to know about each other; they just respond to events |
| **Transactional Consistency** | Database + events saved together (Outbox pattern ensures no lost events) |
| **Testability** | Easy to test: create aggregate, verify state changes and raised events |
| **Scalability** | Events can be published to multiple services without blocking |

---

## Common Pitfalls & Solutions

### ❌ **Pitfall 1: Directly Modifying Aggregate State**

```csharp path=null start=null
// DON'T DO THIS!
var account = await _repository.GetAsync(id);
account.Balance = new Money(5000);  // No event raised!
await _repository.SaveAsync(account);
```

**Solution:** Use aggregate methods that handle events:

```csharp path=null start=null
// DO THIS!
var account = await _repository.GetAsync(id);
account.Deposit(new Money(1000), account);  // Raises event internally
await _repository.SaveAsync(account);
```

---

### ❌ **Pitfall 2: Cross-Aggregate Transactions**

```csharp path=null start=null
// DON'T DO THIS!
var from = await _repository.GetAsync(fromId);
var to = await _repository.GetAsync(toId);
from.Balance -= amount;
to.Balance += amount;
// ← Breaks transactional boundary
```

**Solution:** Keep operations within a single aggregate or use a process manager:

```csharp path=null start=null
// DO THIS!
public Result Transfer(Money amount, Account destination, ...)
{
    var debitResult = this.Debit(amount, ...);
    var creditResult = destination.Credit(amount, ...);
    // Both aggregates' events are raised atomically
}
```

---

### ❌ **Pitfall 3: Forgetting to Clear Events**

```csharp path=null start=null
// Without ClearDomainEvents(), events get re-published every save!
var account = await _repository.GetAsync(id);
// _domainEvents still has events from previous save

account.Deposit(new Money(500));
await _repository.SaveAsync(account);  // Events published again!
```

**Solution:** Always clear after publishing (the outbox pattern handles this automatically)

---

## Best Practices

✅ **DO:**
- Keep domain logic inside the aggregate
- Raise events for important state changes
- Use private constructors and factory methods (like `Create()`)
- Mark domain properties as private setters
- Use value objects for complex properties (Money, AccountNumber, etc.)

❌ **DON'T:**
- Make domain events public properties that can be modified externally
- Allow business logic to live outside aggregates
- Skip validation in factory methods
- Create aggregates without domain events for significant operations
- Use events for read-model updates only (they're for domain logic)

---

## Summary

Your aggregate root pattern works by:

1. **Account aggregate encapsulates** all banking operations
2. **Each operation raises domain events** to record what happened
3. **Events are stored in-memory** during the aggregate's lifetime
4. **On save, events are converted to outbox messages** (Outbox pattern)
5. **Event dispatcher publishes events** to handlers
6. **Handlers perform side effects** (emails, logs, notifications, etc.)
7. **Events are cleared** to prevent re-publishing

This architecture ensures **consistency, auditability, and loose coupling** across your banking system.
