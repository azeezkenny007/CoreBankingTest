using AutoMapper;
using CoreBankingTest.APP.Accounts.Commands.CreateAccount;
using CoreBankingTest.APP.Accounts.Queries.GetAccountDetails;
using CoreBankingTest.APP.Accounts.Queries.GetAccountSummary;
using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Mappings
{

    public class AccountProfile : Profile
    {
        public AccountProfile()
        {
            // Domain Entity to DTO mappings
            CreateMap<Account, AccountDetailsDto>()
                .ForMember(dest => dest.AccountNumber, opt => opt.MapFrom(src => src.AccountNumber.Value))
                .ForMember(dest => dest.AccountType, opt => opt.MapFrom(src => src.AccountType.ToString()))
                .ForMember(dest => dest.Balance, opt => opt.MapFrom(src => src.Balance.Amount))
                .ForMember(dest => dest.CustomerName,
                    opt => opt.MapFrom(src => $"{src.Customer.FirstName} {src.Customer.LastName}"));

          
            CreateMap<CreateAccountCommand, Account>()
                .ConstructUsing(src => Account.Create(
                    src.CustomerId,
                    AccountNumber.Create("TEMPORARY"), // Will be replaced in handler
                    Enum.Parse<AccountType>(src.AccountType),
                    new Money(src.InitialDeposit, src.Currency)
                ))
                .ForAllMembers(opt => opt.Ignore()); // Ignore all direct mappings since we use constructor

            // Transaction mappings
            CreateMap<Transaction, TransactionDto>()
                .ForMember(dest => dest.TransactionId, opt => opt.MapFrom(src => src.TransactionId.Value.ToString()))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount.Amount))
                .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Amount.Currency));

            CreateMap<Account, AccountSummaryDto>()
                  .ForMember(dest => dest.DisplayName,
          opt => opt.MapFrom((src, dest) =>
              src.AccountType == AccountType.Savings
                  ? $"{src.AccountNumber.Value} - Savings"
                  : $"{src.AccountNumber.Value} - Current"));
        }
    }
}
