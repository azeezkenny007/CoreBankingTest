using AutoMapper;
using CoreBankingTest.CORE.Entities; // Ensure this contains the Customer class
using CoreBankingTest.CORE.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Mappings.Resolvers
{
    public class FullNameResolver : IValueResolver<CoreBankingTest.CORE.Entities.Customer, object, string>
    {
        public string Resolve(CoreBankingTest.CORE.Entities.Customer source, object destination, string destMember, ResolutionContext context)
            => $"{source.FirstName} {source.LastName}";
    }
}
