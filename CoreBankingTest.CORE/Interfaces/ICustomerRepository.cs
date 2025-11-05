using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(CustomerId customerId);
        Task<IEnumerable<Customer>> GetAllAsync();

        Task AddAsync (Customer customer);
        Task UpdateAsync(Customer customer);
        Task<bool> ExistsAsync (CustomerId customerId);
        Task<bool> EmailExistsAsync(string email);
    }
}
