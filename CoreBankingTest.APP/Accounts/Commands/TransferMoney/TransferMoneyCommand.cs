using CoreBanking.Core.Exceptions;
using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CoreBankingTest.APP.Accounts.Commands.TransferMoney
{
    public record TransferMoneyCommand : ICommand
    {
        public AccountNumber SourceAccountNumber { get; init; } = AccountNumber.Create(string.Empty);
        public AccountNumber DestinationAccountNumber { get; init; } = AccountNumber.Create(string.Empty);
        public Money Amount { get; init; } = new Money(0);

        public string Reference { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

    }


    public class TransferMoneyCommandHandler : IRequestHandler<TransferMoneyCommand, Result>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public TransferMoneyCommandHandler(IAccountRepository accountRepository, ITransactionRepository transactionRepository, IUnitOfWork unitOfWork)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var sourceAccount = await _accountRepository.GetByAccountNumberAsync(new AccountNumber(request.SourceAccountNumber));
                var destAccount = await _accountRepository.GetByAccountNumberAsync(new AccountNumber(request.DestinationAccountNumber));

                if (sourceAccount == null) return Result.Failure("Source account not found");
                if (destAccount == null) return Result.Failure("Destination account not found");


                sourceAccount.Transfer(
                       amount: request.Amount,
                       destination: destAccount,
                       reference: request.Reference,
                       description: request.Description
                   );

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (InsufficientFundsException ex)
            {
                return Result.Failure($"Insufficient funds. Required: {ex.RequiredAmount:C}, Available: {ex.AvailableBalance:C}");
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Result.Failure($"{ex.Message}");
            }
            catch (Exception ex)
            {
                return Result.Failure($"An unexpected error occurred : {ex.Message}");
            }



        }


    }
}
