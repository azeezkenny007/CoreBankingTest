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

namespace CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory
{
    public record GetTransactionHistoryQuery : IQuery<TransactionHistoryDto>
    {
        public AccountNumber AccountNumber { get; init; } = AccountNumber.Create(string.Empty);
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 50;
    }

    public record TransactionHistoryDto
    {
        public AccountNumber AccountNumber { get; init; } = AccountNumber.Create(string.Empty);
        public List<TransactionDto> Transactions { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int TotalPages { get; init; }
    }

    public record TransactionDto
    {
        public string TransactionId { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Reference { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public decimal RunningBalance { get; init; }
    }

    public class GetTransactionHistoryQueryHandler : IRequestHandler<GetTransactionHistoryQuery, Result<TransactionHistoryDto>>
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountRepository _accountRepository;

        public GetTransactionHistoryQueryHandler(
            ITransactionRepository transactionRepository,
            IAccountRepository accountRepository)
        {
            _transactionRepository = transactionRepository;
            _accountRepository = accountRepository;
        }

        public async Task<Result<TransactionHistoryDto>> Handle(GetTransactionHistoryQuery request, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByAccountNumberAsync(new AccountNumber(request.AccountNumber));
            if (account == null)
                return Result<TransactionHistoryDto>.Failure("Account not found");

            // Start with IQueryable or IEnumerable from repository
            var transactionsQuery = (await _transactionRepository.GetByAccountIdAsync(account.AccountId, cancellationToken))
                .AsQueryable(); // Or keep as IEnumerable

            // Apply date filtering
            if (request.StartDate.HasValue)
                transactionsQuery = transactionsQuery.Where(t => t.Timestamp >= request.StartDate.Value);
            if (request.EndDate.HasValue)
                transactionsQuery = transactionsQuery.Where(t => t.Timestamp <= request.EndDate.Value);

            // Get total count before pagination
            var totalCount = transactionsQuery.Count();

            // Apply pagination and execute query
            var pagedTransactions = transactionsQuery
                .OrderByDescending(t => t.Timestamp)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var transactionDtos = pagedTransactions.Select(t => new TransactionDto
            {
                TransactionId = t.TransactionId.Value.ToString(),
                Type = t.Type.ToString(),
                Amount = t.Amount.Amount,
                Currency = t.Amount.Currency,
                Description = t.Description,
                Reference = t.Reference,
                Timestamp = t.Timestamp
            }).ToList();

            var dto = new TransactionHistoryDto
            {
                AccountNumber = request.AccountNumber,
                Transactions = transactionDtos,
                TotalCount = totalCount,
                Page = request.Page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            };

            return Result<TransactionHistoryDto>.Success(dto);
        }
    }
}
