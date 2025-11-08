# Entities & Relationships - Complete Explanation

## Overview: The Three Main Entities

Your banking system has 3 core entities that work together:

```
Customer (owns multiple accounts)
   │
   ├─→ Account (has many transactions)
   │      │
   │      └─→ Transaction (records each movement)
   │
   └─→ Account
          │
          └─→ Transaction
```

---

## Entity 1: Customer

### What It Represents
A **Customer** is a person who owns bank accounts. Think of it as the identity of the person using the bank.

### Properties (Fields)

```csharp
public class Customer : ISoftDelete
{
    // ✅ IDENTITY (Who are you?)
    public CustomerId CustomerId { get; private set; }
    // - Unique identifier (Guid wrapped in value object)
    // - Example: "a1b2c3d4-1234-5678-9abc-123456789abc"
    
    // ✅ PERSONAL INFO (Your details)
    public string FirstName { get; private set; }
    // - Example: "Alice"
    
    public string LastName { get; private set; }
    // - Example: "Johnson"
    
    public string Email { get; private set; }
    // - Example: "alice.johnson@email.com"
    // - Must be UNIQUE (no two customers can have same email)
    
    public string PhoneNumber { get; private set; }
    // - Example: "555-0101"
    
    public DateTime DateOfBirth { get; private set; }
    // - Example: DateTime(1995, 3, 15)
    
    public string Address { get; private set; }
    // - Example: "13 Oshinowo Street, Abuja"
    
    // ✅ FINANCIAL/COMPLIANCE INFO
    public string BVN { get; private set; }
    // - BVN = Bank Verification Number (Nigerian ID)
    // - Example: "20000000000"
    
    public int CreditScore { get; private set; }
    // - Example: 750 (higher = more creditworthy)
    // - Used for loans, credit decisions
    
    // ✅ STATUS FLAGS
    public bool IsActive { get; private set; }
    // - Can perform transactions? (true/false)
    
    public bool IsDeleted { get; private set; }
    // - Soft delete flag (hidden but not truly deleted)
    // - Example: false (not deleted), true (deleted)
    
    public DateTime? DeletedAt { get; private set; }
    // - When was it deleted? (null = not deleted)
    
    public string DeletedBy { get; private set; }
    // - Who deleted it? (admin name, null if not deleted)
    
    public DateTime DateCreated { get; private set; }
    // - When customer created account
    // - Example: "2025-10-08 14:30:00"
    
    // ✅ RELATIONSHIPS (What accounts do you own?)
    private readonly List<Account> _accounts = new();
    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();
    // - Collection of all accounts this customer owns
    // - ONE customer can have MANY accounts
}
```

### Example Customer Data

```
CustomerId: "a1b2c3d4-1234-5678-9abc-123456789abc"
FirstName: "Alice"
LastName: "Johnson"
Email: "alice.johnson@email.com" (UNIQUE)
PhoneNumber: "555-0101"
DateOfBirth: 1995-03-15
Address: "13 Oshinowo Street, Abuja"
BVN: "20000000000"
CreditScore: 750
IsActive: true
IsDeleted: false
DeletedAt: null
DeletedBy: null
DateCreated: 2025-10-08 14:30:00

Owns Accounts:
  - Account 1: Checking Account (Balance: $500)
  - Account 2: Savings Account (Balance: $5,000)
  - Account 3: Investment Account (Balance: $10,000)
```

### Methods (What Can Customer Do?)

```csharp
// 1. Update Contact Information
public void UpdateContactInfo(string email, string phoneNumber)
{
    // Change email or phone number
    // Can only do this if customer is active
    // Error if customer is inactive
}
// Example: alice.johnson@email.com → alice.johnson.new@email.com

// 2. Deactivate Account
public void Deactivate()
{
    // Turn off this customer's account
    // Can only deactivate if ALL accounts have zero balance
    // Error if any account has money
}

// 3. Soft Delete (Admin action)
public void SoftDelete(string deletedBy)
{
    // Soft delete (hide, don't truly delete)
    // Can only delete if ALL accounts have zero balance
    // Marks: IsDeleted = true, DeletedAt = now, DeletedBy = "admin"
}
// This preserves data for compliance/audit

// 4. Add Account (Internal)
internal void AddAccount(Account account)
{
    // Internal method to link account to customer
    // Called when customer creates new account
    _accounts.Add(account);
}
```

---

## Entity 2: Account

### What It Represents
An **Account** is a bank account owned by a customer. It holds money and tracks the balance.

### Properties (Fields)

```csharp
public class Account : AggregateRoot<AccountId>, ISoftDelete
{
    // ✅ IDENTITY (Which account?)
    public AccountId AccountId { get; private set; }
    // - Unique identifier (Guid)
    // - Example: "c3d4e5f6-3456-7890-cde1-345678901cde"
    
    public AccountNumber AccountNumber { get; private set; }
    // - Human-readable account number (10 digits)
    // - Example: "1000000001"
    // - UNIQUE - no two accounts same number
    
    // ✅ ACCOUNT TYPE & CURRENCY
    public AccountType AccountType { get; private set; }
    // - Enum: Checking, Savings, Investment, Loan
    // - Different account types have different rules
    // - Example: "Checking"
    
    public string Currency { get; private set; }
    // - Which currency? NGN, USD, EUR, etc.
    // - Example: "NGN" (Nigerian Naira)
    
    // ✅ BALANCE (How much money?)
    public Money Balance { get; private set; }
    // - Contains: Amount (decimal) + Currency (string)
    // - Example: Money(500, "NGN") = $500 NGN
    
    // ✅ TIMESTAMPS
    public DateTime DateOpened { get; private set; }
    // - When account was created
    // - Example: 2025-10-20 09:15:00
    
    // ✅ RELATIONSHIPS (Owner & Transactions)
    public CustomerId CustomerId { get; private set; }
    // - Who owns this account?
    // - Foreign key to Customer
    
    public Customer Customer { get; private set; }
    // - Navigation property (reference to actual Customer object)
    // - MANY accounts point to ONE customer
    
    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();
    // - All transactions on this account
    // - ONE account can have MANY transactions
    
    // ✅ CONCURRENCY CONTROL
    public byte[] RowVersion { get; private set; }
    // - Version number for optimistic locking
    // - Changes every time account is updated
    // - Prevents two users from overwriting each other
    
    // ✅ STATUS FLAGS
    public bool IsActive { get; private set; }
    // - Can deposits/withdrawals happen? (true/false)
    
    public bool IsDeleted { get; private set; }
    // - Soft delete flag
    
    public DateTime? DeletedAt { get; private set; }
    // - When was it deleted?
    
    public string DeletedBy { get; private set; }
    // - Who deleted it?
    
    // ✅ DOMAIN EVENTS (What happened?)
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    // - Records of what happened to this account
    // - Example: AccountCreatedEvent, MoneyTransferredEvent
}
```

### Example Account Data

```
AccountId: "c3d4e5f6-3456-7890-cde1-345678901cde"
AccountNumber: "1000000001"
AccountType: "Checking"
Currency: "NGN"
Balance: Money(500, "NGN")  ← $500
DateOpened: 2025-10-20 09:15:00
CustomerId: "a1b2c3d4-1234-5678-9abc-123456789abc"
IsActive: true
IsDeleted: false
RowVersion: 0x0A (increments with each update)

Transactions on this account:
  - Transaction 1: Deposit $100
  - Transaction 2: Transfer $50 to another account
  - Transaction 3: Withdrawal $25
```

### Business Rules for Account

```
✅ ACTIVE ACCOUNT (IsActive = true)
   Can: Deposit money
   Can: Withdraw money
   Can: Transfer to others
   Can: Receive transfers
   
❌ INACTIVE ACCOUNT (IsActive = false)
   Cannot: Do anything
   Cannot: Deposit
   Cannot: Withdraw
   Cannot: Transfer
   
✅ CHECKING ACCOUNT
   Can: Unlimited deposits/withdrawals
   Rules: None special
   
✅ SAVINGS ACCOUNT
   Can: Deposits (unlimited)
   Can: Withdrawals (limited to 6 per period)
   Rule: After 6 withdrawals → blocked until period resets
   
✅ CONCURRENCY SAFETY
   When you change the account:
   RowVersion: 0x0A → 0x0B → 0x0C (etc.)
   
   If two people modify same account:
   Person A: RowVersion 0x0A → 0x0B ✓
   Person B: RowVersion 0x0A → ? ✗ (FAILS - already changed to 0x0B)
   Person B gets: "409 Conflict - Please reload"
```

### Methods (What Can Account Do?)

```csharp
// 1. Deposit Money
public Transaction Deposit(Money amount, Account account, string description)
{
    // Add money to this account
    // Increases balance
    // Creates transaction record
    // Example: balance 500 + 100 → 600
}

// 2. Withdraw Money
public Transaction Withdraw(Money amount, Account account, string description)
{
    // Remove money from this account
    // Decreases balance
    // Checks: enough balance? account active? withdrawal limit?
    // Example: balance 500 - 100 → 400
}

// 3. Debit (part of transfer)
public Result Debit(Money amount, string description, string reference)
{
    // Remove money (for sending transfers)
    // Example: balance 500 - 100 → 400
    // Returns: Result.Success() or Result.Failure()
}

// 4. Credit (part of transfer)
public Result Credit(Money amount, string description, string reference)
{
    // Add money (for receiving transfers)
    // Example: balance 1000 + 100 → 1100
    // Returns: Result.Success() or Result.Failure()
}

// 5. Transfer Money (complex)
public Result Transfer(Money amount, Account destination, 
                       string reference, string description)
{
    // Main business operation:
    // 1. Validate both accounts
    // 2. Debit source account
    // 3. Credit destination account
    // 4. Create MoneyTransferredEvent
    // 5. Return success/failure
}

// 6. Close Account
public void CloseAccount()
{
    // Permanently close account
    // Can only close if balance = 0
    // Error if balance > 0
    IsActive = false;
}

// 7. Update Balance
public void UpdateBalance(Money newBalance)
{
    // Set new balance
    // Can only do if account is active
    // Error if account inactive
}
```

---

## Entity 3: Transaction

### What It Represents
A **Transaction** is a record of a single money movement on an account. It could be:
- A deposit (money coming in)
- A withdrawal (money going out)
- A transfer (money moving to another account)

### Properties (Fields)

```csharp
public class Transaction : ISoftDelete
{
    // ✅ IDENTITY (Which transaction?)
    public TransactionId TransactionId { get; private set; }
    // - Unique identifier (Guid)
    // - Example: "t5e6f7g8-5678-9012-fgh3-456789012fgh"
    
    // ✅ WHICH ACCOUNT? (Link to Account)
    public AccountId AccountId { get; private set; }
    // - Which account is this transaction on?
    // - Foreign key to Account
    
    public Account Account { get; private set; }
    // - Navigation property (reference to Account object)
    // - MANY transactions point to ONE account
    
    // ✅ WHAT TYPE OF TRANSACTION?
    public TransactionType Type { get; private set; }
    // - Enum: Deposit, Withdrawal, Transfer
    // - Example: "Transfer"
    
    // ✅ HOW MUCH MONEY?
    public Money Amount { get; private set; }
    // - Example: Money(100, "NGN") = $100
    
    // ✅ DESCRIPTION & REFERENCE
    public string Description { get; private set; }
    // - Human-readable description
    // - Example: "Transfer to Alice's account"
    
    public string Reference { get; private set; }
    // - Unique reference code for tracking
    // - Example: "20251107021534-abc12345"
    // - Format: Timestamp + TransactionId prefix
    
    // ✅ WHEN DID THIS HAPPEN?
    public DateTime Timestamp { get; private set; }
    // - When transaction occurred
    // - Example: 2025-11-07 02:15:34
    
    // ✅ SOFT DELETE (Can mark as deleted)
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string DeletedBy { get; private set; }
}
```

### Example Transaction Data

```
TransactionId: "t5e6f7g8-5678-9012-fgh3-456789012fgh"
AccountId: "c3d4e5f6-3456-7890-cde1-345678901cde"
Type: "Transfer"
Amount: Money(100, "NGN")  ← $100
Description: "Transfer to Alice's account"
Reference: "20251107021534-abc12345"
Timestamp: 2025-11-07 02:15:34
IsDeleted: false

Linked to Account:
  - Account: 1000000001 (Checking Account)
```

### Transaction Types

```
✅ DEPOSIT
   Money comes INTO account
   Direction: Outside → Account
   Effect: Balance increases
   Example: User deposits check → Account balance +$500

✅ WITHDRAWAL  
   Money goes OUT of account
   Direction: Account → Outside
   Effect: Balance decreases
   Example: User withdraws cash → Account balance -$100

✅ TRANSFER
   Money goes from one Account to another Account
   Direction: Account A → Account B
   Effect: Account A balance decreases, Account B balance increases
   Example: Transfer $100 from Checking → Savings
            Checking: $500 → $400
            Savings: $1000 → $1100
```

---

## Value Objects (Wrapped Concepts)

### CustomerId - Strongly-Typed ID

```csharp
public record CustomerId
{
    public Guid Value { get; init; }
    // - Wraps a Guid in a record
    // - Type-safe: Can't accidentally use AccountId where CustomerId expected
    // - Example: CustomerId.Create() → CustomerId with random Guid
}

// ❌ BAD (if IDs were just strings):
public void DeleteCustomer(string customerId) { ... }
DeleteCustomer(accountId);  // Oops! Wrong type, but compiled!

// ✅ GOOD (with value objects):
public void DeleteCustomer(CustomerId customerId) { ... }
DeleteCustomer(accountId);  // COMPILER ERROR! Type mismatch!
```

### AccountId - Strongly-Typed ID

```csharp
public record AccountId
{
    public Guid Value { get; init; }
    // - Same concept as CustomerId
    // - Each account has unique Guid-based ID
}
```

### TransactionId - Strongly-Typed ID

```csharp
public record TransactionId
{
    public Guid Value { get; init; }
    // - Same concept
    // - Each transaction has unique ID
}
```

### AccountNumber - Human-Readable Account Number

```csharp
public record AccountNumber
{
    public string Value { get; init; }
    // - 10-digit string like "1000000001"
    // - UNIQUE across all accounts
    // - Value object for type safety
    // - User-friendly (easier than showing Guid)
}
```

### Money - Amount + Currency Together

```csharp
public record Money
{
    public decimal Amount { get; private set; }  // Example: 500
    public string Currency { get; private set; } = "NGN";  // Example: "NGN"
    
    // ✅ KEY BENEFIT: Currency validation
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot add different currencies");
        
        return new Money(a.Amount + b.Amount, a.Currency);
    }
    
    // Example:
    // Money(100, "NGN") + Money(50, "NGN") = Money(150, "NGN") ✓
    // Money(100, "NGN") + Money(50, "USD") = ERROR ✗
}
```

---

## Relationships: How They Connect

### 1. Customer ↔ Account (One-to-Many)

```
ONE Customer can have MANY Accounts
ONE Account belongs to ONE Customer

        Alice (Customer)
        │
        ├─→ Checking Account (Account)
        ├─→ Savings Account (Account)
        └─→ Investment Account (Account)

Database Connection:
Accounts table:
  AccountId    AccountNumber    CustomerId (FK)
  aaaa         1000000001       a1b2c3d4-... (Alice)
  bbbb         1000000002       a1b2c3d4-... (Alice)
  cccc         1000000003       a1b2c3d4-... (Alice)
```

### 2. Account ↔ Transaction (One-to-Many)

```
ONE Account can have MANY Transactions
ONE Transaction belongs to ONE Account

        Checking Account
        │
        ├─→ Transaction 1: Deposit $100
        ├─→ Transaction 2: Withdrawal $50
        ├─→ Transaction 3: Transfer $25
        └─→ Transaction 4: Deposit $200

Database Connection:
Transactions table:
  TransactionId    AccountId (FK)    Type        Amount
  tttt-1           aaaa              Deposit     100
  tttt-2           aaaa              Withdrawal  50
  tttt-3           aaaa              Transfer    25
  tttt-4           aaaa              Deposit     200
```

### 3. All Together

```
Alice (Customer) a1b2c3d4-...
│
├─ Checking Account aaaa (Balance: $400)
│  ├─ Deposit $500 (Timestamp: 2025-10-01)
│  ├─ Transfer OUT $100 (Timestamp: 2025-10-05)
│  └─ Withdrawal $100 (Timestamp: 2025-10-07)
│
├─ Savings Account bbbb (Balance: $1000)
│  ├─ Deposit $1000 (Timestamp: 2025-10-01)
│  └─ Transfer IN $100 from Checking (Timestamp: 2025-10-05)
│
└─ Investment Account cccc (Balance: $5000)
   └─ Deposit $5000 (Timestamp: 2025-10-15)
```

---

## Database Schema (How They're Stored)

### Customers Table

```sql
CREATE TABLE Customers
(
    CustomerId UNIQUEIDENTIFIER PRIMARY KEY,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PhoneNumber NVARCHAR(20) NOT NULL,
    Address NVARCHAR(MAX),
    BVN NVARCHAR(50),
    CreditScore INT,
    DateOfBirth DATETIME2,
    DateCreated DATETIME2,
    IsActive BIT NOT NULL,
    IsDeleted BIT NOT NULL,
    DeletedAt DATETIME2 NULL,
    DeletedBy NVARCHAR(100) NULL
);
```

### Accounts Table

```sql
CREATE TABLE Accounts
(
    AccountId UNIQUEIDENTIFIER PRIMARY KEY,
    AccountNumber NVARCHAR(10) NOT NULL UNIQUE,
    AccountType NVARCHAR(50) NOT NULL,
    Amount DECIMAL(18, 2) NOT NULL,           -- Balance
    Currency NVARCHAR(3) NOT NULL DEFAULT 'NGN',
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    DateOpened DATETIME2,
    IsActive BIT NOT NULL,
    IsDeleted BIT NOT NULL,
    DeletedAt DATETIME2 NULL,
    DeletedBy NVARCHAR(100) NULL,
    RowVersion ROWVERSION,                    -- Concurrency control
    
    CONSTRAINT FK_Accounts_Customers 
        FOREIGN KEY (CustomerId) 
        REFERENCES Customers(CustomerId)
);

CREATE INDEX IX_Accounts_CustomerId ON Accounts(CustomerId);
```

### Transactions Table

```sql
CREATE TABLE Transactions
(
    TransactionId UNIQUEIDENTIFIER PRIMARY KEY,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    Amount DECIMAL(18, 2) NOT NULL,
    Currency NVARCHAR(3) NOT NULL,
    Description NVARCHAR(500),
    Reference NVARCHAR(50),
    Timestamp DATETIME2,
    IsDeleted BIT NOT NULL,
    DeletedAt DATETIME2 NULL,
    DeletedBy NVARCHAR(100) NULL,
    
    CONSTRAINT FK_Transactions_Accounts 
        FOREIGN KEY (AccountId) 
        REFERENCES Accounts(AccountId)
);

CREATE INDEX IX_Transactions_AccountId ON Transactions(AccountId);
CREATE INDEX IX_Transactions_Type ON Transactions(Type);
```

---

## Data Flow Example: Complete Picture

### Scenario: Alice Transfers $100 from Checking to Savings

```
STEP 1: CUSTOMER
═════════════════
Alice (Customer)
├─ CustomerId: a1b2c3d4-...
├─ FirstName: Alice
├─ Email: alice@email.com
└─ Status: Active

STEP 2: SOURCE ACCOUNT
═══════════════════════
Checking Account (Account)
├─ AccountId: aaaa-...
├─ AccountNumber: 1000000001
├─ CustomerId: a1b2c3d4-... (Alice owns this)
├─ Balance: Money(500, "NGN")  ← Before
└─ Status: Active

STEP 3: DESTINATION ACCOUNT
════════════════════════════
Savings Account (Account)
├─ AccountId: bbbb-...
├─ AccountNumber: 1000000002
├─ CustomerId: a1b2c3d4-... (Alice owns this)
├─ Balance: Money(1000, "NGN")  ← Before
└─ Status: Active

STEP 4: PERFORM TRANSFER
═════════════════════════
Account.Transfer() executes:
├─ Debit Checking: 500 - 100 = 400
├─ Credit Savings: 1000 + 100 = 1100
└─ Create Event: MoneyTransferredEvent

AFTER TRANSFER:
═══════════════
Checking Account:
├─ Balance: Money(400, "NGN")  ← Updated
└─ Transactions:
   ├─ Transaction tttt-1 (Existing)
   ├─ Transaction tttt-2 (Existing)
   └─ Transaction tttt-3 (NEW) ← Type: Transfer OUT, Amount: 100

Savings Account:
├─ Balance: Money(1100, "NGN")  ← Updated
└─ Transactions:
   ├─ Transaction ssss-1 (Existing)
   └─ Transaction ssss-2 (NEW) ← Type: Transfer IN, Amount: 100

Database State:
├─ Customers: Alice still same
├─ Accounts: Both balances updated
└─ Transactions: 2 new records added (one for each account)
```

---

## Summary: Entity Responsibilities

| Entity | Purpose | Count |
|--------|---------|-------|
| **Customer** | Represents a person/user of the bank | 1 per person |
| **Account** | Bank account owned by customer | Many per customer |
| **Transaction** | Record of money movement | Many per account |

| Relationship | Type | Example |
|--------------|------|---------|
| Customer → Account | 1-to-Many | Alice has 3 accounts |
| Account → Transaction | 1-to-Many | Checking has 50 transactions |

| Value Object | Wraps | Purpose |
|--------------|-------|---------|
| CustomerId | Guid | Type-safe customer reference |
| AccountId | Guid | Type-safe account reference |
| TransactionId | Guid | Type-safe transaction reference |
| AccountNumber | string | Human-readable account ID |
| Money | decimal + string | Amount + Currency together |

