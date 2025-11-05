using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Accounts.Commands.TransferMoney
{
    public class TransferMoneyCommandValidator : AbstractValidator<TransferMoneyCommand>
    {
        public TransferMoneyCommandValidator()
        {
            RuleFor(x => x.SourceAccountNumber.Value)
                .NotEmpty().WithMessage("Source account number is required")
                .Length(10).WithMessage("Source account number must be 10 digits")
                .Matches(@"^\d+$").WithMessage("Source account number must contain only digits");

            RuleFor(x => x.DestinationAccountNumber.Value)
                .NotEmpty().WithMessage("Destination account number is required")
                .Length(10).WithMessage("Destination account number must be 10 digits")
                .Matches(@"^\d+$").WithMessage("Destination account number must contain only digits")
                .NotEqual(cmd => cmd.SourceAccountNumber).WithMessage("Cannot transfer to the same account");

            RuleFor(x => x.Amount.Amount)
                .GreaterThan(0).WithMessage("Transfer amount must be greater than 0")
                .LessThanOrEqualTo(500000).WithMessage("Single transfer cannot exceed ₦500,000");

            RuleFor(x => x.Amount.Currency)
                .NotEmpty().WithMessage("Currency is required")
                .Length(3).WithMessage("Currency must be 3 characters");

            RuleFor(x => x.Reference)
                .MaximumLength(50).WithMessage("Reference cannot exceed 50 characters");

            RuleFor(x => x.Description)
                .MaximumLength(200).WithMessage("Description cannot exceed 200 characters");
        }
    }
}
