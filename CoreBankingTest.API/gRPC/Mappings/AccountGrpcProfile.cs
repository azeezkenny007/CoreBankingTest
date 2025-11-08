using AutoMapper;
using CoreBankingTest.API.gRPC; // gRPC-generated types
using CoreBankingTest.APP.Accounts.Queries.GetAccountDetails;
using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using Google.Protobuf.WellKnownTypes;

namespace CoreBanking.API.gRPC.Mappings
{
    public class AccountGrpcProfile : Profile
    {
        public AccountGrpcProfile()
        {
            CreateMap<AccountDetailsDto, AccountResponse>()
                .ForMember(dest => dest.AccountNumber, opt => opt.MapFrom(src => src.AccountNumber != null ? src.AccountNumber.Value : string.Empty))
                .ForMember(dest => dest.AccountType, opt => opt.MapFrom(src => src.AccountType))
                .ForMember(dest => dest.Balance, opt => opt.MapFrom(src => src.Balance.Amount))
                .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Balance.Currency))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.CustomerName))
                .ForMember(dest => dest.DateOpened, opt => opt.MapFrom(src =>
                    Timestamp.FromDateTime(DateTime.SpecifyKind(src.DateOpened, DateTimeKind.Utc))))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

            CreateMap<TransactionDto, TransactionResponse>()
                .ForMember(dest => dest.TransactionId, opt => opt.MapFrom(src => src.TransactionId))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Currency))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Reference, opt => opt.MapFrom(src => src.Reference))
                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src =>
                    Timestamp.FromDateTime(DateTime.SpecifyKind(src.Timestamp, DateTimeKind.Utc))))
                .ForMember(dest => dest.RunningBalance, opt => opt.MapFrom(src => src.RunningBalance));
        }
    }
}
