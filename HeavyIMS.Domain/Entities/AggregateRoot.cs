using HeavyIMS.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavyIMS.Domain.Entities
{
    /// <summary>
    /// Aggregate Root Base Class
    /// RESPONSIBILITY: Provide domain event collection for aggregate roots
    ///
    /// DDD PATTERN: Aggregate Root
    /// - Entry point for all operations on the aggregate
    /// - Maintains consistency boundary
    /// - Collects domain events raised during operations
    /// - Events are dispatched by Unit of Work after successful persistence
    ///
    /// USAGE:
    /// Inherit from this class for all aggregate roots:
    /// - WorkOrder
    /// - Part
    /// - Inventory
    /// - Customer
    /// - Technician
    ///
    /// DEMONSTRATES:
    /// - Template Method pattern (RaiseDomainEvent)
    /// - Encapsulation (private event collection)
    /// - Domain Event pattern
    ///
    /// EXAMPLE:
    /// <code>
    /// public class Part : AggregateRoot
    /// {
    ///     public void UpdatePricing(decimal unitCost, decimal unitPrice)
    ///     {
    ///         var oldCost = UnitCost.Amount;
    ///         var oldPrice = UnitPrice.Amount;
    ///
    ///         UnitCost = Money.Create(unitCost);
    ///         UnitPrice = Money.Create(unitPrice);
    ///
    ///         // Raise domain event for cross-aggregate communication
    ///         RaiseDomainEvent(new PartPriceUpdated(
    ///             PartId, oldCost, unitCost, oldPrice, unitPrice
    ///         ));
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class AggregateRoot
    {
        /// <summary>
        /// Private collection of domain events raised by this aggregate
        /// Events are cleared after being dispatched by Unit of Work
        /// </summary>
        private readonly List<DomainEvent> _domainEvents = new();

        /// <summary>
        /// Read-only view of domain events raised by this aggregate
        /// Used by Unit of Work to collect and dispatch events after SaveChanges
        /// </summary>
        public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        /// <summary>
        /// Raise a domain event to be published after SaveChanges succeeds
        ///
        /// IMPORTANT: Events are NOT published immediately
        /// - Events are collected in this aggregate
        /// - Unit of Work dispatches them after successful SaveChanges
        /// - This ensures transactional consistency (events only fire if persistence succeeds)
        /// </summary>
        /// <param name="domainEvent">The event to raise</param>
        protected void RaiseDomainEvent(DomainEvent domainEvent)
        {
            if (domainEvent == null)
                throw new ArgumentNullException(nameof(domainEvent));

            _domainEvents.Add(domainEvent);
        }

        /// <summary>
        /// Clear all domain events from this aggregate
        /// Called by Unit of Work after events have been successfully dispatched
        /// </summary>
        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        /// <summary>
        /// Check if this aggregate has any pending domain events
        /// </summary>
        public bool HasDomainEvents => _domainEvents.Any();
    }
}
