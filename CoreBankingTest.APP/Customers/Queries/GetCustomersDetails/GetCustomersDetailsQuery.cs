using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;

using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Customers.Queries.GetCustomers
{
    public record GetCustomerDetailsQuery : IQuery<CustomerDetailsDto>
    {
        public Guid CustomerId { get; init; }
    }

    public class GetCustomersDetailsQueryHandler : IRequestHandler<GetCustomerDetailsQuery, Result<CustomerDetailsDto>>
    {
        private readonly ICustomerRepository _customerRepository;

        public GetCustomersDetailsQueryHandler(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task<Result<CustomerDetailsDto>> Handle(GetCustomerDetailsQuery request, CancellationToken cancellationToken)
        {

            var customer = await _customerRepository.GetByIdAsync(CustomerId.Create(request.CustomerId));
            if (customer == null) throw new ArgumentException("Customer doesn't exist");

            var customerDtos = new CustomerDetailsDto
            {
                CustomerId = customer.CustomerId,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Phone = customer.PhoneNumber,
                DateRegistered = customer.DateCreated,
                IsActive = customer.IsActive
            };

            return Result<CustomerDetailsDto>.Success(customerDtos);
        }

    }

}
