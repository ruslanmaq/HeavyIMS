using HeavyIMS.Domain.Events;
using HeavyIMS.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeavyIMS.Infrastructure.Events
{
    /// <summary>
    /// Domain Event Dispatcher Implementation
    /// RESPONSIBILITY: Dispatch domain events to all registered handlers
    ///
    /// DEMONSTRATES:
    /// - Mediator pattern
    /// - Service Locator pattern (via IServiceProvider)
    /// - Error handling and logging
    /// - Async/await pattern
    ///
    /// HOW IT WORKS:
    /// 1. Receives domain event
    /// 2. Resolves all handlers for that event type from DI container
    /// 3. Executes each handler in sequence
    /// 4. Logs execution and errors
    /// 5. Continues even if individual handlers fail (resilience)
    ///
    /// USAGE:
    /// Called by Unit of Work after SaveChanges succeeds:
    /// <code>
    /// await _dispatcher.DispatchAsync(aggregateRoot.DomainEvents);
    /// aggregateRoot.ClearDomainEvents();
    /// </code>
    /// </summary>
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DomainEventDispatcher> _logger;

        public DomainEventDispatcher(
            IServiceProvider serviceProvider,
            ILogger<DomainEventDispatcher> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Dispatch a single domain event to all registered handlers
        /// </summary>
        public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
            where TEvent : DomainEvent
        {
            if (domainEvent == null)
                throw new ArgumentNullException(nameof(domainEvent));

            _logger.LogInformation(
                "Dispatching domain event: {EventType} (ID: {EventId}, Occurred: {OccurredOn})",
                domainEvent.GetType().Name,
                domainEvent.EventId,
                domainEvent.OccurredOn);

            // Resolve all handlers for this event type from DI container
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);

            if (!handlers.Any())
            {
                _logger.LogWarning(
                    "No handlers registered for domain event: {EventType}",
                    domainEvent.GetType().Name);
                return;
            }

            // Execute each handler
            var handlerList = handlers.ToList();
            _logger.LogInformation(
                "Found {HandlerCount} handler(s) for {EventType}",
                handlerList.Count,
                domainEvent.GetType().Name);

            foreach (var handler in handlerList)
            {
                try
                {
                    // Invoke HandleAsync method via reflection
                    // (Could use dynamic for better performance but this is clearer)
                    var handleMethod = handlerType.GetMethod("HandleAsync");
                    if (handleMethod != null)
                    {
                        var task = (Task)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken });
                        await task;

                        _logger.LogInformation(
                            "Successfully executed handler {HandlerType} for event {EventType}",
                            handler.GetType().Name,
                            domainEvent.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other handlers
                    // RESILIENCE: One handler failure shouldn't stop others
                    _logger.LogError(ex,
                        "Error executing handler {HandlerType} for event {EventType}. Event ID: {EventId}",
                        handler.GetType().Name,
                        domainEvent.GetType().Name,
                        domainEvent.EventId);

                    // In production, you might want to:
                    // - Publish to dead letter queue
                    // - Store failed event for retry
                    // - Alert operations team
                }
            }
        }

        /// <summary>
        /// Dispatch multiple domain events in order
        /// Used by Unit of Work to publish all events from modified aggregates
        /// </summary>
        public async Task DispatchAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            if (domainEvents == null)
                throw new ArgumentNullException(nameof(domainEvents));

            var eventList = domainEvents.ToList();

            if (!eventList.Any())
            {
                _logger.LogDebug("No domain events to dispatch");
                return;
            }

            _logger.LogInformation(
                "Dispatching {EventCount} domain event(s)",
                eventList.Count);

            // Dispatch events in order (FIFO)
            // Events from the same aggregate should execute in the order they were raised
            foreach (var domainEvent in eventList)
            {
                // Use reflection to call generic DispatchAsync<TEvent> method
                var dispatchMethod = GetType()
                    .GetMethod(nameof(DispatchAsync), new[] { domainEvent.GetType(), typeof(CancellationToken) })
                    ?? GetType()
                        .GetMethods()
                        .First(m => m.Name == nameof(DispatchAsync) && m.IsGenericMethod)
                        .MakeGenericMethod(domainEvent.GetType());

                var task = (Task)dispatchMethod.Invoke(this, new object[] { domainEvent, cancellationToken });
                await task;
            }

            _logger.LogInformation(
                "Successfully dispatched {EventCount} domain event(s)",
                eventList.Count);
        }
    }
}
