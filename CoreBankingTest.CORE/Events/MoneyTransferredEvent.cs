using CoreBankingTest.CORE.Common;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Events
{
    public record MoneyTransferredEvent : DomainEvent, INotification
    {
        public TransactionId TransactionId { get; }
        public AccountNumber SourceAccountNumber { get; }
        public AccountNumber DestinationAccountNumber { get; }
        public Money Amount { get; }
        public string Reference { get; }
        public DateTime TransferDate { get; }
        public Guid EventId { get; } = Guid.NewGuid();
        public string EventType { get; } = nameof(MoneyTransferredEvent);
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
        public string TransactionType { get; } = "Transfer";


        public MoneyTransferredEvent(TransactionId transactionId, AccountNumber sourceAccountNumber,
            AccountNumber destinationAccountNumber, Money amount, string reference)
        {
            TransactionId = transactionId;
            SourceAccountNumber = sourceAccountNumber;
            DestinationAccountNumber = destinationAccountNumber;
            Amount = amount;
            Reference = reference;
            TransferDate = DateTime.UtcNow;
        }
    }



}
