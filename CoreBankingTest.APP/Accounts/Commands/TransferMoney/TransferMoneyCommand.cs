using CoreBanking.Core.Exceptions;
using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.CORE.Events;
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
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public TransferMoneyCommandHandler(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            IDomainEventDispatcher domainEventDispatcher,
            IUnitOfWork unitOfWork)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _domainEventDispatcher = domainEventDispatcher;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Find source and destination accounts
                var sourceAccount = await _accountRepository.GetByAccountNumberAsync(new AccountNumber(request.SourceAccountNumber));
                var destAccount = await _accountRepository.GetByAccountNumberAsync(new AccountNumber(request.DestinationAccountNumber));

                if (sourceAccount == null)
                    return Result.Failure("Source account not found");
                if (destAccount == null)
                    return Result.Failure("Destination account not found");

                // Execute transfer and capture the returned transaction
                var transferResult = sourceAccount.Transfer(
                    transferAmount: request.Amount,
                    destination: destAccount,
                    reference: request.Reference,
                    transferDescription: request.Description
                );

                if (!transferResult.IsSuccess)
                    return Result.Failure("Transfer Not Successful"); // Return the failure from domain

                var transaction = transferResult.Value; // Get the created transaction

                // Save changes
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // After successful transfer, publish the event
                var moneyTransferredEvent = new MoneyTransferredEvent(
                    transaction.TransactionId, // Now transaction is defined
                    sourceAccount.AccountNumber, // Use AccountNumber instead of string
                    destAccount.AccountNumber,   // Use AccountNumber instead of string
                    request.Amount,
                    request.Reference);

                await _domainEventDispatcher.DispatchAsync(moneyTransferredEvent, cancellationToken);

                return Result.Success();
            }
            catch (InvalidOperationException ex)
            {
                // Handle domain business rule violations
                return Result.Failure(ex.Message);
            }
            catch (ArgumentException ex)
            {
                // Handle argument validation errors
                return Result.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                return Result.Failure($"An unexpected error occurred: {ex.Message}");
            }
        }
    }

}
