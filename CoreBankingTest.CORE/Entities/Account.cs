using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using CoreBankingTest.CORE.Events;
using CoreBankingTest.CORE.Common;
using CoreBanking.Core.Exceptions;




namespace CoreBankingTest.CORE.Entities
{
    public class Account : AggregateRoot<AccountId>, ISoftDelete
    {
        public AccountId AccountId { get; private set; }
        public AccountNumber AccountNumber { get; private set; }
        public AccountType AccountType { get; private set; }
        public Money Balance { get; private set; }
        public CustomerId CustomerId { get; private set; }
        public Customer Customer { get; private set; }
        public DateTime DateOpened { get; private set; }
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
        public bool IsActive { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public string? DeletedBy { get; private set; }

        // Domain events collection
        private readonly List<DomainEvent> _domainEvents = new();
        public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        // Navigation properties - private to enforce aggregate boundary
        private readonly List<Transaction> _transactions = new();
        public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

        private Account() { } // EF Core needs this

        // Option 1: Keep existing constructor for internal use
        private Account(AccountNumber accountNumber, AccountType accountType, CustomerId customerId)
        {
            AccountId = AccountId.Create();
            AccountNumber = accountNumber;
            AccountType = accountType;
            CustomerId = customerId;
            Balance = new Money(0);
            DateOpened = DateTime.UtcNow;
            IsActive = true;
        }

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
                Balance = initialDeposit // Set initial balance after construction
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

        // Core banking operations - these are the aggregate's public API
        public Transaction Deposit(Money amount, Account account, string description = "Deposit")
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot deposit to inactive account");

            if (amount.Amount <= 0)
                throw new ArgumentException("Deposit amount must be positive");

            Balance += amount;

            var transaction = new Transaction(
                accountId: AccountId,
                account: account,
                type: TransactionType.Deposit,
                amount: amount,
                description: description
            );

            _transactions.Add(transaction);
            return transaction;
        }

        public Transaction Withdraw(Money amount, Account account, string description = "Withdrawal")
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot withdraw from inactive account");

            if (amount.Amount <= 0)
                throw new ArgumentException("Withdrawal amount must be positive");

            if (Balance.Amount < amount.Amount)
                throw new InsufficientFundsException(amount.Amount, Balance.Amount);

            // Special business rule for Savings accounts
            if (AccountType == AccountType.Savings && _transactions.Count(t => t.Type == TransactionType.Withdrawal) >= 6)
                throw new InvalidOperationException("Savings account withdrawal limit reached");

            Balance -= amount;

            var transaction = new Transaction(
                accountId: AccountId,
                account: account,
                type: TransactionType.Withdrawal,
                amount: amount,
                description: description
            );

            _transactions.Add(transaction);
            return transaction;
        }

      

        public Result Transfer(Money amount, Account destination, string reference, string description)
        {
            // Validate inputs
            if (destination == null)
                throw new ArgumentNullException(nameof(destination), "Destination account cannot be null");

            if (amount.Amount <= 0)
                throw new InvalidOperationException("Transfer amount must be positive");

            if (this == destination)
                throw new InvalidOperationException("Cannot transfer to the same account");

            // Check source account conditions
            if (!IsActive)
                throw new InvalidOperationException("Source account is not active");

            if (!destination.IsActive)
                throw new InvalidOperationException("Destination account is not active");

            // Check sufficient funds
            if (Balance.Amount < amount.Amount)
            {
                // Raise insufficient funds event
                _domainEvents.Add(new InsufficientFundsEvent(
                    AccountNumber, amount, Balance, "Transfer"));

                throw new InsufficientFundsException(amount.Amount, Balance.Amount);
            }

            // Special business rules for Savings accounts
            if (AccountType == AccountType.Savings &&
                _transactions.Count(t => t.Type == TransactionType.Withdrawal) >= 6)
            {
                return Result.Failure("Savings account withdrawal limit reached");
            }

            // Execute the transfer as an atomic operation
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

            // Return success result
            return Result.Success();
        }


        // Add to CoreBanking.Core/Entities/Account.cs

        public Result Debit(Money amount, string description, string reference)
        {
            if (IsDeleted)
                return Result.Failure("Cannot debit a deleted account");

            if (amount.Amount <= 0)
                return Result.Failure("Debit amount must be positive");

            if (Balance.Amount < amount.Amount)
                return Result.Failure("Insufficient funds");

            // Apply debit
            Balance -= amount;

            // Record transaction (matches your Transaction constructor)
            var transaction = new Transaction(
                AccountId,                            // AccountId
                TransactionType.Withdrawal,    // Transaction type
                amount,                        // Amount
                description,                   // Description
                this,                          // Account reference
                reference                      // Optional reference
            );

            _transactions.Add(transaction);

            // Raise domain event
            //AddDomainEvent(new AccountDebitedEvent(Id, amount, reference));

            return Result.Success();
        }

        public Result Credit(Money amount, string description, string reference)
        {
            if (IsDeleted)
                return Result.Failure("Cannot credit a deleted account");

            if (amount.Amount <= 0)
                return Result.Failure("Credit amount must be positive");

            // Apply credit
            Balance += amount;

            // Record transaction (matches your Transaction constructor)
            var transaction = new Transaction(
                AccountId,                          // AccountId
                TransactionType.Deposit,     // Transaction type
                amount,                      // Amount
                description,                 // Description
                this,                        // Account reference
                reference                    // Optional reference
            );

            _transactions.Add(transaction);

            // Raise domain event
            //AddDomainEvent(new AccountCreditedEvent(Id, amount, reference));

            return Result.Success();
        }


        //    destination._transactions.Add(depositTransaction);

        //    // Raise domain events for the transfer
        //    //AddDomainEvent(new MoneyTransferredEvent(this, destination, amount, reference));
        //}

        // Domain event methods
        public void AddDomainEvent(DomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        public void CloseAccount()
        {
            if (Balance.Amount != 0)
                throw new InvalidOperationException("Cannot close account with non-zero balance");

            IsActive = false;
        }

        public void UpdateBalance(Money newBalance)
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot update balance for inactive account.");

            Balance = newBalance;
        }
    }

}