using CoreBankingTest.CORE.Enums;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Accounts.Commands.CreateAccount
{

    public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
    {
        public CreateAccountCommandValidator()
        {
            RuleFor(x => x.CustomerId)
                .NotNull().WithMessage("Customer ID is required")
                .Must(customerId => customerId != null && customerId.Value != Guid.Empty)
                .WithMessage("Customer ID cannot be empty");

            RuleFor(x => x.AccountType)
                .NotEmpty().WithMessage("Account type is required")
                .Must(BeValidAccountType).WithMessage("Invalid account type. Must be Savings or Current");

            RuleFor(x => x.InitialDeposit)
                .GreaterThanOrEqualTo(0).WithMessage("Initial deposit cannot be negative")
                .LessThan(1000000).WithMessage("Initial deposit cannot exceed ₦1,000,000");

            RuleFor(x => x.Currency)
                .NotEmpty().WithMessage("Currency is required")
                .Length(3).WithMessage("Currency must be 3 characters")
                .Must(BeSupportedCurrency).WithMessage("Unsupported currency. Supported: NGN, USD, GBP");
        }

        private bool BeValidAccountType(string accountType)
                => Enum.TryParse<AccountType>(accountType, out _);

        private bool BeSupportedCurrency(string currency)
            => new[] { "NGN", "USD", "GBP" }.Contains(currency);
    }
}