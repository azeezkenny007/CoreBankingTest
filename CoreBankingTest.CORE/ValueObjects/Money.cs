using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.ValueObjects
{
    public record Money
    {
        public decimal Amount { get; private set; }
        public string Currency { get; private set; } = "NGN";

        // EF Core needs this for materialization
        private Money() { }

        public Money(decimal amount, string currency = "NGN")
        {
            if (amount < 0)
                throw new ArgumentException("Money amount cannot be negative");

            Amount = amount;
            Currency = currency;
        }

        public static Money operator +(Money a, Money b)
        {
            if (a.Currency != b.Currency)
                throw new InvalidOperationException("Cannot add different currencies");

            return new Money(a.Amount + b.Amount, a.Currency);
        }

        public static Money operator -(Money a, Money b)
        {
            if (a.Currency != b.Currency)
                throw new InvalidOperationException("Cannot subtract different currencies");

            return new Money(a.Amount - b.Amount, a.Currency);
        }
    }
}

