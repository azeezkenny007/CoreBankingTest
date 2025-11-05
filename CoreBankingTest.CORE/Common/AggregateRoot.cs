using CoreBankingTest.CORE.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Common
{
    public abstract class AggregateRoot<TId> where TId : notnull
    {
        [NotMapped]
        private readonly List<IDomainEvent> _domainEvents = new();

        [NotMapped]
        public IReadOnlyCollection<IDomainEvent> DomainEvents  => _domainEvents.AsReadOnly();

        protected void AddDomainEvent(IDomainEvent domainEvent)  => _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();


    }
}
