using HeavyIMS.Domain.Events;
using System.Threading;
using System.Threading.Tasks;

namespace HeavyIMS.Domain.Interfaces
{
    /// <summary>
    /// Domain Event Handler Interface
    /// RESPONSIBILITY: Handle domain events when published
    ///
    /// DDD PATTERN: Event-Driven Architecture
    /// - Implement this interface to react to domain events
    /// - Handlers execute business logic in response to state changes
    /// - Multiple handlers can subscribe to the same event
    ///
    /// USAGE EXAMPLE:
    /// <code>
    /// public class InventoryLowStockDetectedHandler : IDomainEventHandler&lt;InventoryLowStockDetected&gt;
    /// {
    ///     public async Task HandleAsync(InventoryLowStockDetected event, CancellationToken cancellationToken)
    ///     {
    ///         // Send notification to purchasing
    ///         // Create reorder suggestion
    ///         // Log analytics event
    ///     }
    /// }
    /// </code>
    ///
    /// BEST PRACTICES:
    /// - Keep handlers focused (Single Responsibility)
    /// - Handlers should be idempotent (safe to run multiple times)
    /// - Avoid long-running operations (use background jobs if needed)
    /// - Don't modify aggregates that raised the event (causes circular dependencies)
    ///
    /// DEMONSTRATES:
    /// - Command Query Responsibility Segregation (CQRS)
    /// - Observer pattern
    /// - Open/Closed Principle (add handlers without modifying aggregates)
    /// </summary>
    /// <typeparam name="TEvent">Type of domain event this handler processes</typeparam>
    public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
    {
        /// <summary>
        /// Handle the domain event
        /// </summary>
        /// <param name="domainEvent">The event that occurred</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
    }
}
