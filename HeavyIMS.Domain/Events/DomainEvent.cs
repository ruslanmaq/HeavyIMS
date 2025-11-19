using System;

namespace HeavyIMS.Domain.Events
{
    /// <summary>
    /// Base class for all domain events
    /// DEMONSTRATES: Event-driven architecture for cross-aggregate communication
    /// </summary>
    public abstract class DomainEvent
    {
        public Guid EventId { get; }
        public DateTime OccurredOn { get; }

        protected DomainEvent()
        {
            EventId = Guid.NewGuid();
            OccurredOn = DateTime.UtcNow;
        }
    }
}
