using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.ValueObjects
{
    
        public record AccountId
        {
            public Guid Value { get; init; }

            // EF Core needs a parameterless constructor
            private AccountId() { }

            private AccountId(Guid value)
            {
                Value = value;
            }

            public static AccountId Create() => new(Guid.NewGuid());
            public static AccountId Create(Guid value) => new(value);

            public override string ToString() => Value.ToString();
        }
    }

