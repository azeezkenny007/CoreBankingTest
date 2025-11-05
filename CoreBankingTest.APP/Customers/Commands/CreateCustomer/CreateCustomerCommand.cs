using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;



namespace CoreBankingTest.APP.Customers.Commands.CreateCustomer
{



    public record CreateCustomerCommand : ICommand<Guid>
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public DateTime DateOfBirth { get; init; }
    }





    public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<Guid>>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateCustomerCommandHandler(
            ICustomerRepository customerRepository,
            IUnitOfWork unitOfWork)
        {
            _customerRepository = customerRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            // Check if customer with email already exists
            if (await _customerRepository.EmailExistsAsync(request.Email))
                return Result<Guid>.Failure("Customer with this email already exists");

            var customer = Customer.Create(
                firstName: request.FirstName,
                lastName: request.LastName,
                email: request.Email,
                phoneNumber: request.PhoneNumber,
                address: request.Address,
                dateOfBirth: request.DateOfBirth
            );

            //      var customer = new Customer(
            //    firstName: request.FirstName,
            //    lastName: request.LastName,
            //    email: request.Email,
            //    phoneNumber: request.PhoneNumber,
            //    address: request.Address,
            //    dateOfBirth: request.DateOfBirth

            //);


            await _customerRepository.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(customer.CustomerId.Value);
        }
    }



}
