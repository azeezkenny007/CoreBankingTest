using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.ValueObjects
{
    public record TransactionId
    {
        public Guid Value { get; init; }

        // EF Core needs a parameterless constructor
        private TransactionId() { }

        private TransactionId(Guid value)
        {
            Value = value;
        }

        public static TransactionId Create() => new(Guid.NewGuid());
        public static TransactionId Create(Guid value) => new(value);

        public override string ToString() => Value.ToString();
    }
}
