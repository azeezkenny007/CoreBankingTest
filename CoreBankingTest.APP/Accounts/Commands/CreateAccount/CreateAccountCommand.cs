using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Accounts.Commands.CreateAccount
{

    public record CreateAccountCommand : ICommand<Guid>
    {
        public CustomerId CustomerId { get; init; } = CustomerId.Create();
        public string AccountType { get; init; } = string.Empty;
        public decimal InitialDeposit { get; init; }
        public string Currency { get; init; } = "NGN";
    }

    public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<Guid>>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateAccountCommandHandler(
            IAccountRepository accountRepository,
            ICustomerRepository customerRepository,
            IUnitOfWork unitOfWork)
        {
            _accountRepository = accountRepository;
            _customerRepository = customerRepository;
            _unitOfWork = unitOfWork;
            
        }

        public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            // Validate customer exists
            var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
            if (customer == null)
                return Result<Guid>.Failure("Customer not found");

            // Generate unique account number
            var accountNumber = await GenerateUniqueAccountNumberAsync();

            // Create account with initial deposit
            var account = Account.Create(
                customerId: request.CustomerId,
                accountNumber: accountNumber,
                accountType: Enum.Parse<AccountType>(request.AccountType),
                initialBalance: new Money(request.InitialDeposit, request.Currency)
            );

            // Add to repository
            await _accountRepository.AddAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(account.AccountId.Value);
        }

        private async Task<AccountNumber> GenerateUniqueAccountNumberAsync()
        {
            string accountNumber;
            do
            {
                accountNumber = GenerateAccountNumber();
            } while (await _accountRepository.AccountNumberExistsAsync(new AccountNumber(accountNumber)));

            return new AccountNumber(accountNumber);
        }

        private string GenerateAccountNumber() =>
            DateTime.UtcNow.ToString("HHmmss") + Random.Shared.Next(1000, 9999).ToString();
    }
}
