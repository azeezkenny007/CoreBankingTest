using AutoMapper;
using CoreBankingTest.API.Models.Requests;
using CoreBankingTest.APP.Accounts.Commands.CreateAccount;
using CoreBankingTest.APP.Accounts.Commands.TransferMoney;
using CoreBankingTest.APP.Customers.Commands.CreateCustomer;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.API.Mappings
{
    public class RequestToCommandProfile : Profile
    {
        public RequestToCommandProfile()
        {
            // Map API Request DTOs to Application Commands

            // CreateAccountRequest -> CreateAccountCommand
            CreateMap<CreateAccountRequest, CreateAccountCommand>()
                .ForMember(dest => dest.CustomerId,
                    opt => opt.MapFrom(src => CustomerId.Create(src.CustomerId)))
                .ForMember(dest => dest.AccountType,
                    opt => opt.MapFrom(src => src.AccountType ?? string.Empty));

            // CreateCustomerRequest -> CreateCustomerCommand
            CreateMap<CreateCustomerRequest, CreateCustomerCommand>()
                .ForMember(dest => dest.PhoneNumber,
                    opt => opt.MapFrom(src => src.Phone));

            // TransferMoneyRequest -> TransferMoneyCommand
            // Note: SourceAccountNumber must be set separately in the controller from route parameter
            CreateMap<TransferMoneyRequest, TransferMoneyCommand>()
                .ForMember(dest => dest.DestinationAccountNumber,
                    opt => opt.MapFrom(src => AccountNumber.Create(src.DestinationAccountNumber)))
                .ForMember(dest => dest.Amount,
                    opt => opt.MapFrom(src => new Money(src.Amount, src.Currency)))
                .ForMember(dest => dest.Reference,
                    opt => opt.MapFrom(src => src.Reference))
                .ForMember(dest => dest.Description,
                    opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.SourceAccountNumber,
                    opt => opt.Ignore()); // Must be set from route parameter in controller
        }
    }
}
