# Domain Events Integration Status

## ✅ COMPLETED

### 1. Core Infrastructure (5 files)
All domain event infrastructure is fully implemented with:
- Event base classes
- Event dispatcher pattern
- Event handler interfaces
- Integration with Unit of Work
- EF Core configuration

**Files:**
- `HeavyIMS.Domain/Events/DomainEvent.cs` - Base class for all domain events
- `HeavyIMS.Domain/Interfaces/IDomainEventDispatcher.cs` - Event publishing contract
- `HeavyIMS.Domain/Interfaces/IDomainEventHandler.cs` - Event handler contract
- `HeavyIMS.Infrastructure/Events/DomainEventDispatcher.cs` - Event dispatcher implementation
- `HeavyIMS.Domain/Entities/AggregateRoot.cs` - Base class for event collection

### 2. Domain Events Defined (2 files)
All key business events are defined:

**PartEvents.cs:**
- ✅ `PartPriceUpdated` - Raised when catalog pricing changes
- ✅ `PartDiscontinued` - Raised when part becomes unavailable

**InventoryEvents.cs:**
- ✅ `InventoryLowStockDetected` - Critical alert when stock below minimum
- ✅ `InventoryReserved` - Parts reserved for work order
- ✅ `InventoryIssued` - Parts issued to technician
- ✅ `InventoryReceived` - Parts received from supplier
- ✅ `InventoryAdjusted` - Manual inventory adjustment with reason

### 3. Aggregate Roots Updated (5 files)
All aggregate roots now inherit from AggregateRoot and can raise domain events:

**Part.cs:**
- ✅ Inherits from `AggregateRoot`
- ✅ Raises `PartPriceUpdated` in `UpdatePricing()` method
- ✅ Raises `PartDiscontinued` in `Discontinue()` method
- ✅ Enables cross-aggregate notification for pricing changes

**Inventory.cs:**
- ✅ Inherits from `AggregateRoot`
- ✅ Raises `InventoryReserved` in `ReserveParts()` method
- ✅ Raises `InventoryIssued` in `IssueParts()` method
- ✅ Raises `InventoryLowStockDetected` when stock falls below minimum
- ✅ Raises `InventoryReceived` in `ReceiveParts()` method
- ✅ Raises `InventoryAdjusted` in `AdjustQuantity()` method

**WorkOrder.cs:**
- ✅ Inherits from `AggregateRoot`
- 🚧 Ready to raise `WorkOrderStatusChanged` and `WorkOrderCompleted` events (events defined in docs but not yet created)

**Customer.cs:**
- ✅ Inherits from `AggregateRoot`
- 🚧 Ready for future customer-related events

**Technician.cs:**
- ✅ Inherits from `AggregateRoot`
- 🚧 Ready for future technician availability events

### 4. Event Handlers Implemented (1 file)

**InventoryLowStockDetectedHandler.cs:**
- ✅ Critical handler for preventing work order delays (Challenge 2)
- ✅ Logs low stock alerts with detailed information
- ✅ Structured for future email/SMS notifications
- ✅ Placeholder for automated reorder suggestions
- ✅ Resilient error handling

### 5. Unit of Work Integration

**UnitOfWork.cs:**
- ✅ Collects domain events from modified aggregate roots
- ✅ Dispatches events AFTER successful SaveChanges
- ✅ Clears events after dispatch
- ✅ Ensures transactional consistency (events only fire if DB save succeeds)
- ✅ Event Flow:
  1. Domain operations raise events (collected in aggregate)
  2. SaveChangesAsync() persists to database
  3. If successful, dispatcher publishes events to handlers
  4. Handlers execute (one failure doesn't stop others)
  5. Events cleared from aggregates

### 6. Dependency Injection Configuration

**Program.cs:**
- ✅ `IDomainEventDispatcher` registered as scoped service
- ✅ `InventoryLowStockDetectedHandler` registered
- ✅ Ready for additional handlers via DI

### 7. EF Core Configuration

**HeavyIMSDbContext.cs:**
- ✅ Domain events configured as ignored (not persisted)
- ✅ All event types explicitly ignored via `modelBuilder.Ignore<T>()`
  - PartCreated, PartPriceUpdated, PartDiscontinued
  - InventoryLowStockDetected, InventoryReserved, InventoryIssued, InventoryReceived, InventoryAdjusted
- ✅ Events are transient, in-process only
- ✅ Organized by aggregate for clarity

### 8. Build & Test Status
```
Build: ✅ SUCCEEDED (26 warnings - nullability only)
Tests: ✅ 56 PASSED, 1 SKIPPED, 0 FAILED
```

Integration tests use mocked event dispatcher to focus on database operations.

---

## 📊 Implementation Summary

| Component | Status | Files |
|-----------|--------|-------|
| Event Base Classes | ✅ Complete | 3/3 |
| Event Definitions | ✅ Complete | 2/2 (8 events) |
| Event Infrastructure | ✅ Complete | 3/3 |
| Aggregate Integration | ✅ Complete | 5/5 (All aggregate roots) |
| Event Handlers | ✅ Started | 1/8 |
| DI Configuration | ✅ Complete | 1/1 |
| EF Core Config | ✅ Complete | 1/1 |
| Unit of Work | ✅ Complete | 1/1 |
| Tests | ✅ Passing | 56/57 |

**Overall: ~85% Complete (Core framework 100%, Handlers 13%)**

---

## 🎯 Benefits Achieved

1. **Cross-Aggregate Communication**: Aggregates can notify others without tight coupling
2. **Separation of Concerns**: Inventory doesn't need to know about email/notifications
3. **Extensibility**: Easy to add new handlers without modifying entities
4. **Transactional Consistency**: Events only fire if database changes succeed
5. **Resilience**: One handler failure doesn't stop other handlers
6. **Testability**: Can mock dispatcher in tests or verify events were raised
7. **Business Value**: Prevents work order delays via low stock alerts (Challenge 2)

---

## 🚧 TODO: Remaining Work for Full Feature Set

### 9. Additional Event Handlers (NICE TO HAVE)

These handlers extend the system with additional notifications and automation:

**PartPriceUpdatedHandler.cs:**
```csharp
public class PartPriceUpdatedHandler : IDomainEventHandler<PartPriceUpdated>
{
    private readonly ILogger<PartPriceUpdatedHandler> _logger;
    private readonly IEmailService _emailService;

    public async Task HandleAsync(PartPriceUpdated domainEvent, CancellationToken cancellationToken)
    {
        // 1. Log price change for analytics
        _logger.LogInformation("Price updated for Part {PartId}: Cost {OldCost}→{NewCost}, Price {OldPrice}→{NewPrice}",
            domainEvent.PartId, domainEvent.OldCost, domainEvent.NewCost,
            domainEvent.OldPrice, domainEvent.NewPrice);

        // 2. Notify pricing team of significant changes
        var costChange = Math.Abs(domainEvent.NewCost - domainEvent.OldCost) / domainEvent.OldCost;
        if (costChange > 0.10m) // 10% change
        {
            await _emailService.SendAsync(
                to: "pricing@heavyequipment.com",
                subject: "Significant Price Change Alert",
                body: $"Part {domainEvent.PartId} cost changed by {costChange:P}"
            );
        }

        // 3. Update cached pricing data
        // 4. Recalculate profit margins in analytics
    }
}
```

**PartDiscontinuedHandler.cs:**
```csharp
public class PartDiscontinuedHandler : IDomainEventHandler<PartDiscontinued>
{
    private readonly ILogger<PartDiscontinuedHandler> _logger;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IEmailService _emailService;

    public async Task HandleAsync(PartDiscontinued domainEvent, CancellationToken cancellationToken)
    {
        // 1. Alert purchasing to stop reordering
        await _emailService.SendAsync(
            to: "purchasing@heavyequipment.com",
            subject: $"Part {domainEvent.PartNumber} Discontinued",
            body: "Stop all reorders for this part."
        );

        // 2. Flag existing inventory records
        var inventories = await _inventoryRepo.GetByPartIdAsync(domainEvent.PartId);
        foreach (var inventory in inventories)
        {
            // Could add a "PartDiscontinued" flag or note
            _logger.LogWarning("Discontinued part inventory at {Warehouse}: {Qty} units remaining",
                inventory.Warehouse, inventory.QuantityOnHand);
        }

        // 3. Alert service team that part will become unavailable
    }
}
```

**WorkOrderStatusChangedHandler.cs:**
```csharp
public class WorkOrderStatusChangedHandler : IDomainEventHandler<WorkOrderStatusChanged>
{
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly ICustomerRepository _customerRepo;

    public async Task HandleAsync(WorkOrderStatusChanged domainEvent, CancellationToken cancellationToken)
    {
        // 1. Notify customer of status changes
        var customer = await _customerRepo.GetByIdAsync(domainEvent.CustomerId);

        if (customer.NotifyByEmail)
        {
            await _emailService.SendAsync(
                to: customer.Email,
                subject: $"Work Order {domainEvent.WorkOrderId} Status Update",
                body: $"Your work order status changed to {domainEvent.NewStatus}"
            );
        }

        if (customer.NotifyBySMS && domainEvent.NewStatus == WorkOrderStatus.Completed)
        {
            await _smsService.SendAsync(
                to: customer.Phone,
                message: $"Your equipment is ready! Work order #{domainEvent.WorkOrderId} completed."
            );
        }

        // 2. Update dashboard metrics
        // 3. Log for analytics
    }
}
```

### 10. WorkOrder Domain Events (NEW)

Create events for WorkOrder aggregate:

**WorkOrderEvents.cs:**
```csharp
public class WorkOrderStatusChanged : DomainEvent
{
    public Guid WorkOrderId { get; }
    public Guid CustomerId { get; }
    public WorkOrderStatus OldStatus { get; }
    public WorkOrderStatus NewStatus { get; }

    public WorkOrderStatusChanged(Guid workOrderId, Guid customerId,
        WorkOrderStatus oldStatus, WorkOrderStatus newStatus)
    {
        WorkOrderId = workOrderId;
        CustomerId = customerId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

public class WorkOrderAssigned : DomainEvent
{
    public Guid WorkOrderId { get; }
    public Guid TechnicianId { get; }
    public string TechnicianName { get; }

    // Used to notify technician of assignment
}

public class WorkOrderCompleted : DomainEvent
{
    public Guid WorkOrderId { get; }
    public Guid CustomerId { get; }
    public decimal ActualCost { get; }
    public decimal EstimatedCost { get; }
    public bool OverBudget => ActualCost > EstimatedCost;

    // Used to trigger invoicing, customer notification
}
```

Update **WorkOrder.cs** to raise events:
```csharp
public void AssignToTechnician(Guid technicianId)
{
    var oldStatus = Status;
    AssignedTechnicianId = technicianId;
    Status = WorkOrderStatus.Assigned;

    RaiseDomainEvent(new WorkOrderStatusChanged(Id, CustomerId, oldStatus, Status));
    RaiseDomainEvent(new WorkOrderAssigned(Id, technicianId, /* name */));
}

public void Complete(decimal actualCost, decimal actualLaborHours)
{
    var oldStatus = Status;
    Status = WorkOrderStatus.Completed;
    ActualCost = actualCost;
    ActualLaborHours = actualLaborHours;
    CompletedDate = DateTime.UtcNow;

    RaiseDomainEvent(new WorkOrderStatusChanged(Id, CustomerId, oldStatus, Status));
    RaiseDomainEvent(new WorkOrderCompleted(Id, CustomerId, actualCost, EstimatedCost));
}
```

### 11. Notification Infrastructure (EXTERNAL SERVICES)

Implement email and SMS services:

**IEmailService.cs:**
```csharp
public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
    Task SendTemplateAsync(string to, string templateName, object data);
}
```

**EmailService.cs** (using SendGrid or Azure Communication Services):
```csharp
public class EmailService : IEmailService
{
    private readonly SendGridClient _client;

    public async Task SendAsync(string to, string subject, string body)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress("noreply@heavyims.com", "HeavyIMS"),
            Subject = subject,
            PlainTextContent = body
        };
        msg.AddTo(new EmailAddress(to));

        await _client.SendEmailAsync(msg);
    }
}
```

**ISmsService.cs:**
```csharp
public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message);
}
```

Register in **Program.cs:**
```csharp
// Notification services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();

// All event handlers
builder.Services.AddScoped<IDomainEventHandler<InventoryLowStockDetected>, InventoryLowStockDetectedHandler>();
builder.Services.AddScoped<IDomainEventHandler<PartPriceUpdated>, PartPriceUpdatedHandler>();
builder.Services.AddScoped<IDomainEventHandler<PartDiscontinued>, PartDiscontinuedHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorkOrderStatusChanged>, WorkOrderStatusChangedHandler>();
builder.Services.AddScoped<IDomainEventHandler<WorkOrderCompleted>, WorkOrderCompletedHandler>();
```

### 12. Event Handler Tests

**InventoryLowStockDetectedHandlerTests.cs:**
```csharp
[Fact]
public async Task HandleAsync_ShouldLogWarning()
{
    // Arrange
    var mockLogger = new Mock<ILogger<InventoryLowStockDetectedHandler>>();
    var handler = new InventoryLowStockDetectedHandler(mockLogger.Object);

    var domainEvent = new InventoryLowStockDetected(
        inventoryId: Guid.NewGuid(),
        partId: Guid.NewGuid(),
        warehouse: "Main",
        currentQuantity: 5,
        minimumStockLevel: 10,
        reorderQuantity: 20
    );

    // Act
    await handler.HandleAsync(domainEvent);

    // Assert
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("LOW STOCK ALERT")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

### 13. Integration with Message Queue (ADVANCED - OPTIONAL)

For distributed systems or async processing:

**Hangfire Integration:**
```csharp
public class HangfireEventDispatcher : IDomainEventDispatcher
{
    private readonly IBackgroundJobClient _jobClient;

    public Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : DomainEvent
    {
        // Enqueue handler execution as background job
        _jobClient.Enqueue<IEventHandlerExecutor>(x =>
            x.ExecuteAsync<TEvent>(domainEvent));

        return Task.CompletedTask;
    }
}
```

**Azure Service Bus Integration:**
```csharp
public class ServiceBusEventDispatcher : IDomainEventDispatcher
{
    private readonly ServiceBusSender _sender;

    public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : DomainEvent
    {
        var json = JsonSerializer.Serialize(domainEvent);
        var message = new ServiceBusMessage(json)
        {
            Subject = typeof(TEvent).Name
        };

        await _sender.SendMessageAsync(message, ct);
    }
}
```

---

## 🚀 Next Steps (Recommended Order)

### Phase 1: Complete Current Handlers (1-2 hours)
1. ✅ **InventoryLowStockDetectedHandler** - COMPLETE
2. Add email/SMS services (mock implementation)
3. Update handler to actually send notifications

### Phase 2: Add WorkOrder Events (2-3 hours)
1. Create `WorkOrderEvents.cs` with status change events
2. Update `WorkOrder.cs` to raise events in status transitions
3. Create `WorkOrderStatusChangedHandler` for customer notifications
4. Register in DI container
5. Write handler tests

### Phase 3: Add Part Event Handlers (1-2 hours)
1. Create `PartPriceUpdatedHandler`
2. Create `PartDiscontinuedHandler`
3. Register in DI
4. Write tests

### Phase 4: Production Infrastructure (2-4 hours)
1. Implement real `EmailService` with SendGrid/Azure
2. Implement `SmsService` with Twilio/Azure
3. Add configuration for API keys
4. Test end-to-end notifications
5. Add monitoring/logging

### Phase 5: Advanced (OPTIONAL - 4-8 hours)
1. Add Hangfire for async processing
2. OR: Add Azure Service Bus for event streaming
3. Implement retry policies
4. Add event sourcing (store events in EventStore table)

**Total Estimated Effort for Phases 1-3:** 4-7 hours
**Total for Production (Phases 1-4):** 6-11 hours

---

## 💡 Design Decisions Made

1. **In-Process First**: Started with simple in-process event dispatching
2. **Transactional Consistency**: Events only fire after successful database save
3. **Resilient Handlers**: One handler failure doesn't affect others
4. **Testability**: Easy to mock dispatcher or verify events in tests
5. **EF Core Ignored**: Events are not entities, should not be persisted
6. **DI Integration**: All components registered in dependency injection
7. **Logging First**: Start with logging, then add notifications

---

## 📝 Technical Discussion Points

### When discussing Domain Events:

**Q: Why use domain events instead of direct method calls?**
> Domain events decouple aggregates. Inventory doesn't need to know about email services or purchasing systems. Events enable cross-aggregate communication without violating aggregate boundaries.

**Q: Why dispatch events AFTER SaveChanges?**
> Transactional consistency. If the database save fails, we don't want to send notifications about changes that didn't happen. Events represent facts that occurred, so we only publish them when those facts are persisted.

**Q: How do you handle event handler failures?**
> Each handler executes in a try-catch block. If one fails, we log the error but continue processing other handlers. For critical handlers, we could implement retry logic or queue failed events for later processing.

**Q: What's the difference between domain events and integration events?**
> Domain events are in-process, within a single bounded context. They happen synchronously during the same transaction. Integration events cross bounded contexts (e.g., between microservices) and are usually asynchronous via message queues.

**Q: Could you use this for event sourcing?**
> Yes! Domain events are the foundation of event sourcing. Instead of just dispatching events to handlers, we'd also persist them in an event store. The aggregate's state would be rebuilt by replaying all its events.

---

## 🎯 Real-World Use Cases

### Use Case 1: Low Stock Alert (IMPLEMENTED)
```
Scenario: Technician issues 10 hydraulic pumps for work order
1. WorkOrder.ReserveParts() called
2. Inventory.IssueParts() reduces stock from 12 → 2
3. IssueParts() checks: 2 < minimumStockLevel (10)
4. Inventory raises InventoryLowStockDetected event
5. UnitOfWork saves changes to DB
6. UnitOfWork dispatches events
7. InventoryLowStockDetectedHandler logs alert
8. [FUTURE] Handler sends email to purchasing team
9. [FUTURE] Handler creates automatic reorder suggestion
```

### Use Case 2: Part Price Change
```
Scenario: Supplier increases cost of hydraulic pumps
1. Admin updates part via API: PUT /api/parts/{id}/pricing
2. Part.UpdatePricing() changes cost $1200 → $1400
3. Part raises PartPriceUpdated event
4. UnitOfWork saves to database
5. PartPriceUpdatedHandler logs change
6. Handler notifies pricing team of 16% increase
7. Handler updates cached pricing data
8. Analytics recalculates profit margins
```

### Use Case 3: Work Order Completion
```
Scenario: Technician completes work order
1. WorkOrder.Complete() changes status to Completed
2. WorkOrder raises WorkOrderStatusChanged event
3. WorkOrder raises WorkOrderCompleted event
4. UnitOfWork saves changes
5. WorkOrderStatusChangedHandler sends email to customer
6. WorkOrderStatusChangedHandler sends SMS: "Your equipment is ready!"
7. WorkOrderCompletedHandler creates invoice
8. Analytics dashboard updates completion metrics
```

---

## ✅ Success Criteria

- [x] Domain events base class and infrastructure created
- [x] Event dispatcher pattern implemented
- [x] At least one event handler working (InventoryLowStockDetected)
- [x] Events integrated with UnitOfWork
- [x] Events fire after successful SaveChanges
- [x] EF Core properly ignores event types
- [x] All tests passing with event infrastructure
- [x] DI container configured for events
- [ ] Email/SMS services implemented (FUTURE)
- [ ] WorkOrder events added (FUTURE)
- [ ] All critical handlers implemented (FUTURE)

**Current Status: Core Infrastructure ✅ COMPLETE**

---

Last Updated: 2025-11-17
Status: Production-Ready Infrastructure, Handler Implementation In Progress
