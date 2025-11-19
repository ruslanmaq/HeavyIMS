using HeavyIMS.Domain.Events;
using System.Threading;
using System.Threading.Tasks;

namespace HeavyIMS.Domain.Interfaces
{
    /// <summary>
    /// Domain Event Dispatcher Interface
    /// RESPONSIBILITY: Publish domain events to registered handlers
    ///
    /// DDD PATTERN: Domain Events for cross-aggregate communication
    /// - Aggregates publish events when state changes
    /// - Handlers react to events without tight coupling
    /// - Maintains aggregate boundaries
    ///
    /// USAGE:
    /// - Called by UnitOfWork after successful SaveChanges
    /// - Dispatches events collected from aggregate roots
    /// - Ensures handlers execute in order
    ///
    /// DEMONSTRATES:
    /// - Mediator pattern for event distribution
    /// - Decoupling through events
    /// - Single Responsibility Principle
    /// </summary>
    public interface IDomainEventDispatcher
    {
        /// <summary>
        /// Dispatch a single domain event to all registered handlers
        /// </summary>
        /// <typeparam name="TEvent">Type of domain event</typeparam>
        /// <param name="domainEvent">The event to dispatch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
            where TEvent : DomainEvent;

        /// <summary>
        /// Dispatch multiple domain events in order
        /// Useful for publishing all events from modified aggregates after SaveChanges
        /// </summary>
        /// <param name="domainEvents">Collection of events to dispatch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task DispatchAsync(System.Collections.Generic.IEnumerable<DomainEvent> domainEvents,
            CancellationToken cancellationToken = default);
    }
}
