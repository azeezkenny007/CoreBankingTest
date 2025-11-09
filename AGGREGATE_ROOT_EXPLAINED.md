# Aggregate Root Pattern Explained

## What is an Aggregate Root?

An **Aggregate Root** is a Domain-Driven Design (DDD) pattern that serves as the entry point for all business logic operations within a logical group of entities. It acts as a **transactional boundary** and **enforcer of business rules**.

Think of it as the "gatekeeper" of your business domain—all changes to related data must go through it, ensuring consistency and preventing invalid states.

---

## Why Use It?

| Problem | Solution |
|---------|----------|
| Business logic scattered across multiple places | Centralize logic in one entity (the aggregate root) |
| Hard to ensure data consistency | Aggregate root enforces invariants/rules |
| Difficult to track what changed in the domain | Domain events record all state changes |
| Hard to understand business intent | Clear method names reflect domain operations |

---

## Your Aggregate Root: Account

Your `Account` class is your aggregate root:

```csharp path=/CoreBankingTest.CORE/Entities/Account.cs start=13
public class Account : AggregateRoot<AccountId>, ISoftDelete
{
    public AccountId AccountId { get; private set; }
    public AccountNumber AccountNumber { get; private set; }
    public Money Balance { get; private set; }
    // ... other properties
}
```

**Key characteristics:**
- ✅ Inherits from `AggregateRoot<AccountId>` 
- ✅ Has a unique identifier (`AccountId`)
- ✅ Contains related child entities (`Transactions`)
- ✅ Encapsulates business rules (deposit, withdraw, transfer)

---

## How the Aggregate Root Works

### 1. **The Base Class Structure**

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

**What each part does:**

| Part | Purpose |
|------|---------|
| `[NotMapped]` | Tells Entity Framework: "Don't save this to the database" |
| `_domainEvents` | Private list storing events raised during operations |
| `DomainEvents` property | Exposes events as **read-only** (can't be modified externally) |
| `AddDomainEvent()` | Protected method—only the aggregate can add events |
| `ClearDomainEvents()` | Public method—called after events are published |

---

### 2. **Creating an Aggregate (With Events)**

When you create an account, the aggregate root immediately raises a domain event:

```csharp path=/CoreBankingTest.CORE/Entities/Account.cs start=50
public static Account Create(
    CustomerId customerId,
    AccountNumber accountNumber,
    AccountType accountType,
    Money initialDeposit)
{
    // 1. VALIDATE - Ensure business rules are met
    if (initialDeposit.Amount < 0)
        throw new InvalidOperationException("Initial balance cannot be negative");

    if (initialDeposit.Amount > 1000000)
        throw new InvalidOperationException("Initial deposit too large");

    // 2. CREATE - Build the aggregate via private constructor
    var account = new Account(
        accountNumber: accountNumber,
        accountType: accountType,
        customerId: customerId
    )
    {
        Balance = initialDeposit
    };

    // 3. RAISE EVENT - Record what happened
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

**Flow:**
```
User wants to create account
    ↓
Validate business rules (balance limits)
    ↓
Create Account object
    ↓
Raise AccountCreatedEvent (stored in _domainEvents)
    ↓
Return Account with event attached (not yet saved)
```

---

### 3. **Business Operations Modify State & Raise Events**

Every important operation in the aggregate root modifies state **and** records what happened via events:

#### Example: Transfer Money

```csharp path=/CoreBankingTest.CORE/Entities/Account.cs start=139
public Result Transfer(Money amount, Account destination, string reference, string description)
{
    // VALIDATE
    if (destination == null)
        throw new ArgumentNullException(nameof(destination));
    
    if (amount.Amount <= 0)
        throw new InvalidOperationException("Transfer amount must be positive");

    if (this == destination)
        throw new InvalidOperationException("Cannot transfer to same account");

    if (!IsActive || !destination.IsActive)
        throw new InvalidOperationException("Account must be active");

    // CHECK SUFFICIENT FUNDS
    if (Balance.Amount < amount.Amount)
    {
        // RAISE EVENT - Even for failures (audit trail)
        _domainEvents.Add(new InsufficientFundsEvent(
            AccountNumber, amount, Balance, "Transfer"));

        throw new InsufficientFundsException(amount.Amount, Balance.Amount);
    }

    // EXECUTE - Modify state on both accounts
    var debitResult = Debit(amount, $"Transfer to {destination.AccountNumber}", reference);
    if (!debitResult.IsSuccess)
        return debitResult;

    var creditResult = destination.Credit(amount, $"Transfer from {AccountNumber}", reference);
    if (!creditResult.IsSuccess)
        return creditResult;

    // RAISE EVENT - Record the success
    var transactionId = TransactionId.Create();
    _domainEvents.Add(new MoneyTransferredEvent(
        transactionId, AccountNumber, destination.AccountNumber, amount, reference));

    return Result.Success();
}
```

**Key points:**
- ✅ **Validates first** - Ensures business rules before any changes
- ✅ **Modifies state** - Updates `Balance` through `Debit()` and `Credit()`
- ✅ **Raises events** - Records what happened (success OR failure)
- ✅ **Atomic operation** - Either all succeeds or all fails

---

### 4. **How Events Are Recorded**

Domain events capture "what happened" at a point in time:

```csharp path=/CoreBankingTest.CORE/Common/DomainEvent.cs start=11
public abstract record DomainEvent : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();           // Unique event ID
    public DateTime OccurredOn { get; } = DateTime.UtcNow;  // When it happened
    public string EventType => GetType().Name;              // What type of event
}
```

**Why `record` type?**
- Immutable (can't be changed once created)
- Perfect for events (they represent historical facts)
- Built-in equality comparison

Example event:
```csharp
public record AccountCreatedEvent : DomainEvent
{
    public AccountId AccountId { get; }
    public AccountNumber AccountNumber { get; }
    public CustomerId CustomerId { get; }
    public AccountType AccountType { get; }
    public Money InitialDeposit { get; }
}
```

---

## Complete Operation Flow

Here's what happens when you create and modify an account:

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. APPLICATION LAYER                                            │
│    (e.g., CreateAccountCommand)                                 │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 2. DOMAIN LAYER (AGGREGATE ROOT)                                │
│    Account.Create()                                             │
│    ├─ Validate business rules                                  │
│    ├─ Create Account instance                                  │
│    └─ AddDomainEvent(AccountCreatedEvent)                      │
│       └─ Event stored in _domainEvents collection              │
│       └─ Event NOT yet persisted                               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 3. REPOSITORY/PERSISTENCE LAYER                                │
│    await _accountRepository.AddAsync(account)                  │
│    └─ Account added to DbContext                               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 4. SAVE TO DATABASE                                             │
│    await _dbContext.SaveChangesAsync()                         │
│    ├─ Account saved to 'accounts' table                        │
│    ├─ Events converted to OutboxMessages                       │
│    └─ ClearDomainEvents() called                               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 5. EVENT PUBLISHING                                             │
│    DomainEventDispatcher.Dispatch()                            │
│    └─ Events published via MediatR                             │
│       ├─ SendNotificationHandler receives event                │
│       ├─ UpdateAuditLogHandler receives event                  │
│       └─ Other handlers receive event                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## What The Aggregate Root DOES

### 1. **Encapsulates Business Logic**

All banking operations are methods on the Account aggregate:

```csharp
// Create
var account = Account.Create(customerId, accountNumber, accountType, deposit);

// Operate
account.Deposit(amount, account);
account.Withdraw(amount, account);
account.Transfer(amount, destination, reference, description);
```

❌ **You DON'T do this** (logic outside aggregate):
```csharp
// WRONG - Logic not in aggregate
var account = await _repository.GetAsync(id);
account.Balance = new Money(5000);  // Direct modification, no validation
```

✅ **You DO this** (logic in aggregate):
```csharp
// CORRECT - Logic in aggregate
var account = await _repository.GetAsync(id);
account.Deposit(new Money(1000), account);  // Validation happens inside
```

---

### 2. **Enforces Business Rules & Invariants**

The aggregate ensures the domain's rules are never broken:

```csharp
// Rule: Cannot deposit negative amount
if (amount.Amount <= 0)
    throw new ArgumentException("Deposit amount must be positive");

// Rule: Cannot withdraw from inactive account
if (!IsActive)
    throw new InvalidOperationException("Cannot withdraw from inactive account");

// Rule: Savings account limited to 6 withdrawals
if (AccountType == AccountType.Savings && _transactions.Count(...) >= 6)
    throw new InvalidOperationException("Savings account withdrawal limit reached");

// Rule: Cannot close account with balance
if (Balance.Amount != 0)
    throw new InvalidOperationException("Cannot close account with non-zero balance");
```

---

### 3. **Tracks State Changes via Domain Events**

Every significant state change is recorded as an event:

```csharp
// When account is created
_domainEvents.Add(new AccountCreatedEvent(...));

// When transfer fails (audit trail)
_domainEvents.Add(new InsufficientFundsEvent(...));

// When transfer succeeds
_domainEvents.Add(new MoneyTransferredEvent(...));
```

**Why?** Creates an audit trail + enables other systems to react to changes.

---

### 4. **Maintains Aggregate Boundaries**

The aggregate controls what can be accessed and modified:

```csharp
// Private - can't modify from outside
private readonly List<Transaction> _transactions = new();

// Read-only - can see but can't modify
public IReadOnlyCollection<Transaction> Transactions 
    => _transactions.AsReadOnly();

// Private setter - can only change through methods
public Money Balance { get; private set; }  
```

This prevents external code from breaking business rules.

---

### 5. **Provides Clear Domain Operations**

Each method represents a real-world operation:

```csharp
account.Deposit(amount, account);           // Business operation
account.Withdraw(amount, account);          // Business operation
account.Transfer(amount, destination, ...); // Business operation
account.CloseAccount();                     // Business operation
```

Not just getters/setters, but **domain intent**.

---

## How It Operates: Step-by-Step Example

### Scenario: Transfer $100 from Account A to Account B

```csharp
// Step 1: Get the accounts
var accountA = await _repository.GetAsync(accountAId);  // Account A
var accountB = await _repository.GetAsync(accountBId);  // Account B

// Step 2: Execute transfer on Account A aggregate
var result = accountA.Transfer(
    amount: new Money(100),
    destination: accountB,
    reference: "TRF001",
    description: "Payment"
);

// Step 3: Save to database
if (result.IsSuccess)
{
    await _repository.SaveAsync(accountA);
    await _repository.SaveAsync(accountB);
}
```

**Inside Account.Transfer():**

```
1. VALIDATE
   ├─ destination != null? ✓
   ├─ amount > 0? ✓
   ├─ not same account? ✓
   ├─ both accounts active? ✓
   └─ sufficient funds? ✓

2. EXECUTE
   ├─ Debit accountA (Balance -= 100)
   ├─ Credit accountB (Balance += 100)

3. RAISE EVENT
   └─ _domainEvents.Add(new MoneyTransferredEvent(...))
      └─ Stored in memory (NOT in database yet)

4. RETURN RESULT
   └─ Result.Success()
```

**After SaveAsync():**

```csharp
// Database
accounts table:
├─ Account A: Balance = 900 (was 1000)
└─ Account B: Balance = 1100 (was 1000)

outbox_messages table:
└─ MoneyTransferredEvent serialized as JSON

// In-Memory
accountA._domainEvents = [] (cleared)
accountB._domainEvents = [] (cleared)
```

**Event Dispatcher Then:**

```csharp
1. Read: MoneyTransferredEvent from outbox
2. Publish: via MediatR to all handlers
3. Handlers receive event:
   ├─ SendNotificationHandler → sends email to customers
   ├─ UpdateAuditLogHandler → logs transaction
   └─ TriggerWorkflowHandler → starts fraud detection, etc.
```

---

## Key Takeaways

| Concept | Meaning |
|---------|---------|
| **Aggregate Root** | Entity that controls access to related entities and enforces business rules |
| **Transactional Boundary** | All changes to related data must go through this one entity |
| **Domain Events** | Record of what happened in the domain (immutable) |
| **Encapsulation** | Business logic stays inside, external code can't break it |
| **Invariants** | Rules that must always be true (aggregate enforces them) |

---

## Benefits in Your System

✅ **Consistency** - Business rules always enforced
✅ **Auditability** - Every change recorded via domain events
✅ **Loose Coupling** - Event handlers don't need to know about each other
✅ **Testability** - Easy to test: create aggregate, call method, verify state + events
✅ **Clear Intent** - Code reads like domain language, not technical
✅ **Scalability** - Events can trigger side effects in other services

---

## Summary

Your `Account` aggregate root is the **gatekeeper** of all account operations. It:

1. **Validates** that every operation is allowed
2. **Modifies** the account state (Balance, Transactions)
3. **Raises Events** to record what happened
4. **Prevents** invalid states through encapsulation
5. **Provides** a clear domain API (Deposit, Withdraw, Transfer)

When you save the aggregate, the database gets the new state **AND** an audit trail of events. This ensures your banking system stays consistent while maintaining a complete history of what happened.
