using CoreBankingTest.APP.Accounts.Queries.GetAccountSummary;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Customers.Queries.GetCustomers
{
    public record CustomerDetailsDto
    {
        public CustomerId CustomerId { get; init; } = CustomerId.Create();
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public DateTime DateOfBirth { get; init; }
        public DateTime DateRegistered { get; init; }
        public bool IsActive { get; init; }
        public List<AccountSummaryDto> Accounts { get; init; } = new();
    }
}
