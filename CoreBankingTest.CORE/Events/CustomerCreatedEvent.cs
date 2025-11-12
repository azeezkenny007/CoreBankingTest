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
    public class CustomerCreatedEvent : IDomainEvent, INotification
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
        public CustomerId CustomerId { get; }
        public string FirstName { get; }
        public string LastName { get; }
        public string Email { get; }
        public string PhoneNumber { get; }
        public int CreditScore { get; }

        public string EventType => throw new NotImplementedException();

        public CustomerCreatedEvent(CustomerId customerId, string firstName, string lastName, string email, string phoneNumber, int creditScore)
        {
            CustomerId = customerId;
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            PhoneNumber = phoneNumber;
            CreditScore = creditScore;
        }
    }
}
