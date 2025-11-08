using AutoMapper;
using CoreBankingTest.API.Models.Requests;
using CoreBankingTest.APP.Accounts.Commands.CreateAccount;
using CoreBankingTest.APP.Accounts.Commands.TransferMoney;
using CoreBankingTest.APP.Accounts.Queries.GetAccountDetails;
using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using CoreBankingTest.CORE.ValueObjects;
using CoreBankingTest.API.gRPC;
using Grpc.Core;
using MediatR;

namespace CoreBankingTest.API.gRPC.Services
{
    public class AccountGrpcService : AccountService.AccountServiceBase
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;
        private readonly ILogger<AccountGrpcService> _logger;

        public AccountGrpcService(IMediator mediator, IMapper mapper, ILogger<AccountGrpcService> logger)
        {
            _mediator = mediator;
            _mapper = mapper;
            _logger = logger;
        }

        public override async Task<AccountResponse> GetAccount(GetAccountRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation("gRPC GetAccount called for {AccountNumber}", request.AccountNumber);

            // Validate input
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Account number is required"));

            try
            {
                var accountNumber = AccountNumber.Create(request.AccountNumber);
                var query = new GetAccountDetailsQuery { AccountNumber = accountNumber };
                var result = await _mediator.Send(query);

                if (result.IsSuccess)
                {
                    return _mapper.Map<AccountResponse>(result.Data);
                }
                else
                {
                    throw new RpcException(new Status(StatusCode.NotFound, string.Join("; ", result.Errors)));
                }
            }
            catch (Exception ex) when (ex is not RpcException)
            {
                _logger.LogError(ex, "Error retrieving account {AccountNumber}", request.AccountNumber);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public override async Task<CreateAccountResponse> CreateAccount(CreateAccountRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation("gRPC CreateAccount called for customer {CustomerId}", request.CustomerId);

            if (!Guid.TryParse(request.CustomerId, out var customerGuid))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid customer ID format"));

            try
            {
                var command = new CreateAccountCommand
                {
                    CustomerId = CustomerId.Create(customerGuid),
                    AccountType = request.AccountType,
                    InitialDeposit = (decimal)request.InitialDeposit,
                    Currency = request.Currency
                };

                var result = await _mediator.Send(command);

                if (result.IsSuccess)
                {
                    return new CreateAccountResponse
                    {
                        AccountId = result.Data.ToString(),
                        AccountNumber = "", // TODO: Return actual account number from command result
                        Message = "Account created successfully"
                    };
                }
                else
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors)));
                }
            }
            catch (Exception ex) when (ex is not RpcException)
            {
                _logger.LogError(ex, "Error creating account for customer {CustomerId}", request.CustomerId);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }
    }
}
