using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Entities
{
    public class Transaction : ISoftDelete
    {
        public TransactionId TransactionId { get; private set; }
        public AccountId AccountId { get; private set; }
        public Account Account { get; private set; }
        public TransactionType Type { get; private set; }
        public Money Amount { get; private set; }
        public string Description { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Reference { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public string? DeletedBy { get; private set; }


        private Transaction() { } // for materializing EF Core

        public Transaction(AccountId accountId, TransactionType type, Money amount, string description, Account account, string reference ="" )
        {
            TransactionId = TransactionId.Create();
            AccountId = accountId;
            Account = account;
            Type = type;
            Amount = amount;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Timestamp = DateTime.UtcNow;
            Reference = string.IsNullOrEmpty(reference) ? GenerateReference() : Reference;
        }

        private string GenerateReference()
        {
            return $"{Timestamp:yyyyMMddHHmmss}-{TransactionId.ToString().Substring(0, 8)}";
        }
    }
}
