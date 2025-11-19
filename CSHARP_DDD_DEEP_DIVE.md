# C# and DDD Deep Dive: Complete Learning Guide

## Understanding C# Features Through DDD Implementation

This guide explains every C# feature and DDD pattern used in HeavyIMS with detailed examples and explanations.

---

## Table of Contents

1. [C# Language Features](#c-language-features)
2. [Object-Oriented Principles](#object-oriented-principles)
3. [DDD Tactical Patterns](#ddd-tactical-patterns)
4. [SOLID Principles](#solid-principles)
5. [Advanced C# Concepts](#advanced-c-concepts)

---

## C# Language Features

### 1. Properties with Access Modifiers

**What**: Properties control how data is accessed and modified

```csharp
public class WorkOrder
{
    // ❌ BAD: Public setter allows anyone to change ID
    public Guid Id { get; set; }

    // ✅ GOOD: Private setter prevents external modification
    public Guid Id { get; private set; }

    // ✅ BEST: Read-only after construction (init-only setter)
    public Guid Id { get; init; }

    // Public getter, private setter
    public string WorkOrderNumber { get; private set; }

    // Auto-property (compiler generates backing field)
    public DateTime CreatedAt { get; private set; }

    // Calculated property (no backing field)
    public bool IsDelayed =>
        ScheduledEndDate.HasValue &&
        Status != WorkOrderStatus.Completed &&
        DateTime.UtcNow > ScheduledEndDate.Value;
}
```

**Why This Matters**:
- **Encapsulation**: Hide internal state, expose only necessary interface
- **Immutability**: `private set` prevents accidental changes
- **Domain Integrity**: Forces use of domain methods

**Real-World Example**:
```csharp
// ❌ Without encapsulation
var workOrder = new WorkOrder();
workOrder.Status = WorkOrderStatus.Completed; // No validation!

// ✅ With encapsulation
var workOrder = WorkOrder.Create(...);
workOrder.UpdateStatus(WorkOrderStatus.Completed); // Validated!
```

### 2. Constructors: Private vs Public

**What**: Constructors control how objects are created

```csharp
public class Inventory
{
    // Private constructor - can't be called from outside
    // EF Core uses reflection to create instances
    private Inventory()
    {
        Transactions = new List<InventoryTransaction>();
    }

    // Static factory method - only way to create valid Inventory
    public static Inventory Create(
        Guid partId,
        string warehouse,
        string binLocation,
        int minimumStockLevel,
        int maximumStockLevel)
    {
        // Validation before object creation
        if (partId == Guid.Empty)
            throw new ArgumentException("PartId is required");

        if (minimumStockLevel < 0)
            throw new ArgumentException("Minimum stock level cannot be negative");

        if (maximumStockLevel < minimumStockLevel)
            throw new ArgumentException("Maximum must be >= minimum");

        // Create object with valid state
        return new Inventory
        {
            InventoryId = Guid.NewGuid(),
            PartId = partId,
            Warehouse = warehouse,
            BinLocation = binLocation,
            QuantityOnHand = 0,
            QuantityReserved = 0,
            MinimumStockLevel = minimumStockLevel,
            MaximumStockLevel = maximumStockLevel,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }
}
```

**Why Private Constructors**:
1. **Force validation**: Can't create invalid objects
2. **Named constructors**: `Create()` is more descriptive than `new()`
3. **Business logic**: Creation is a domain operation
4. **Testability**: Easier to mock factory methods

**DDD Pattern**: This is the **Factory Method** pattern

### 3. Static Methods and Classes

**What**: Static members belong to the type, not instances

```csharp
// Static utility class (cannot be instantiated)
public static class MoneyExtensions
{
    public static string FormatAsCurrency(this decimal amount)
    {
        return $"${amount:N2}";
    }
}

// Usage
decimal price = 1250.50m;
Console.WriteLine(price.FormatAsCurrency()); // "$1,250.50"

// Static factory method (common in DDD)
public class Part
{
    public static Part Create(string partNumber, string partName, ...)
    {
        // Validation and creation logic
        return new Part { ... };
    }
}

// Static helper in domain entity
public class WorkOrder
{
    private static string GenerateWorkOrderNumber()
    {
        var year = DateTime.UtcNow.Year;
        var randomNum = new Random().Next(10000, 99999);
        return $"WO-{year}-{randomNum:D5}";
    }
}
```

**When to Use Static**:
- ✅ Extension methods
- ✅ Factory methods
- ✅ Utility functions with no state
- ❌ Business logic that needs testing (prefer instance methods)

### 4. Nullable Types

**What**: Types that can represent "no value"

```csharp
public class WorkOrder
{
    // Nullable Guid - technician might not be assigned yet
    public Guid? AssignedTechnicianId { get; private set; }

    // Nullable DateTime - work might not have started
    public DateTime? ActualStartDate { get; private set; }

    // Non-nullable - always required
    public Guid CustomerId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Checking nullable values
    public void PrintAssignment()
    {
        // Old way (C# 7 and earlier)
        if (AssignedTechnicianId.HasValue)
        {
            Console.WriteLine($"Assigned to: {AssignedTechnicianId.Value}");
        }

        // Modern way (C# 8+ with null-coalescing operator)
        var techId = AssignedTechnicianId ?? Guid.Empty;

        // Null-conditional operator
        var startDate = ActualStartDate?.ToString("yyyy-MM-dd") ?? "Not started";
    }
}
```

**C# 8+ Nullable Reference Types**:
```csharp
#nullable enable

public class Customer
{
    // Non-nullable (compiler warning if null)
    public string CompanyName { get; private set; }

    // Nullable (explicitly allows null)
    public string? Address { get; private set; }

    public Customer(string companyName)
    {
        CompanyName = companyName; // OK
        Address = null; // OK (explicitly nullable)
    }

    public void UpdateAddress(string address)
    {
        // Compiler ensures address is not null
        Address = address;
    }
}
```

### 5. Collections and Initialization

**What**: Different ways to work with collections

```csharp
public class WorkOrder
{
    // Collection property (navigation property for EF Core)
    public ICollection<WorkOrderPart> RequiredParts { get; private set; }
    public ICollection<WorkOrderNotification> Notifications { get; private set; }

    private WorkOrder()
    {
        // Initialize in constructor to prevent null reference
        RequiredParts = new List<WorkOrderPart>();
        Notifications = new List<WorkOrderNotification>();
    }

    // Why ICollection<T> instead of List<T>?
    // - Interface (more flexible, follows dependency inversion)
    // - EF Core can use its own collection implementations
    // - Easier to test (can mock interface)
}

// Modern collection initialization (C# 3.0+)
var priorities = new List<WorkOrderPriority>
{
    WorkOrderPriority.Low,
    WorkOrderPriority.Normal,
    WorkOrderPriority.High
};

// Collection expressions (C# 12)
var statuses = [ WorkOrderStatus.Pending, WorkOrderStatus.Assigned ];
```

**Read-Only Collections**:
```csharp
public class Inventory
{
    // Private backing field (mutable)
    private readonly List<InventoryTransaction> _transactions;

    // Public read-only view (immutable)
    public IReadOnlyCollection<InventoryTransaction> Transactions => _transactions.AsReadOnly();

    public Inventory()
    {
        _transactions = new List<InventoryTransaction>();
    }

    public void AddTransaction(InventoryTransaction transaction)
    {
        // Only aggregate can modify its collection
        _transactions.Add(transaction);
    }
}
```

### 6. Enums: Simple and Smart Usage

**What**: Named constants for related values

```csharp
// Basic enum
public enum WorkOrderStatus
{
    Pending,        // 0 (default)
    Assigned,       // 1
    InProgress,     // 2
    OnHold,         // 3
    Completed,      // 4
    Cancelled       // 5
}

// Enum with explicit values
public enum WorkOrderPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

// Using enums
var status = WorkOrderStatus.Pending;
var priority = WorkOrderPriority.High;

// Convert to string
string statusName = status.ToString(); // "Pending"

// Parse from string
WorkOrderStatus parsed = Enum.Parse<WorkOrderStatus>("InProgress");

// Check if valid
bool isValid = Enum.IsDefined(typeof(WorkOrderStatus), 10); // false

// Get all values
var allStatuses = Enum.GetValues<WorkOrderStatus>();
```

**Smart Enum Pattern** (alternative to basic enum):
```csharp
// Better encapsulation with behavior
public class WorkOrderStatus
{
    public string Name { get; }
    public int Value { get; }

    private WorkOrderStatus(string name, int value)
    {
        Name = name;
        Value = value;
    }

    public static WorkOrderStatus Pending = new WorkOrderStatus("Pending", 0);
    public static WorkOrderStatus Assigned = new WorkOrderStatus("Assigned", 1);
    public static WorkOrderStatus InProgress = new WorkOrderStatus("InProgress", 2);
    public static WorkOrderStatus Completed = new WorkOrderStatus("Completed", 4);

    // Business logic attached to status
    public bool CanTransitionTo(WorkOrderStatus newStatus)
    {
        if (this == Pending && newStatus == Assigned) return true;
        if (this == Assigned && newStatus == InProgress) return true;
        if (this == InProgress && newStatus == Completed) return true;
        return false;
    }
}
```

### 7. Switch Expressions (C# 8+)

**What**: Modern pattern matching syntax

```csharp
// Old switch statement
public int GetMaxConcurrentJobs(TechnicianSkillLevel skillLevel)
{
    switch (skillLevel)
    {
        case TechnicianSkillLevel.Junior:
            return 2;
        case TechnicianSkillLevel.Intermediate:
            return 3;
        case TechnicianSkillLevel.Senior:
            return 4;
        case TechnicianSkillLevel.Expert:
            return 5;
        default:
            return 2;
    }
}

// Modern switch expression (C# 8+)
public int GetMaxConcurrentJobs(TechnicianSkillLevel skillLevel) =>
    skillLevel switch
    {
        TechnicianSkillLevel.Junior => 2,
        TechnicianSkillLevel.Intermediate => 3,
        TechnicianSkillLevel.Senior => 4,
        TechnicianSkillLevel.Expert => 5,
        _ => 2 // Default case
    };

// Pattern matching with conditions
public string GetStatusDescription(WorkOrder wo) =>
    wo.Status switch
    {
        WorkOrderStatus.Pending => "Waiting for assignment",
        WorkOrderStatus.Assigned when wo.ScheduledStartDate.HasValue =>
            $"Scheduled for {wo.ScheduledStartDate.Value:d}",
        WorkOrderStatus.InProgress when wo.IsDelayed =>
            "⚠️ DELAYED - In Progress",
        WorkOrderStatus.InProgress => "Currently being worked on",
        WorkOrderStatus.Completed => "✅ Completed",
        _ => "Unknown status"
    };
```

### 8. LINQ (Language Integrated Query)

**What**: Query syntax for collections

```csharp
public class Technician
{
    public ICollection<WorkOrder> AssignedWorkOrders { get; private set; }

    /// <summary>
    /// LINQ Example: Count active jobs
    /// </summary>
    public int GetActiveJobCount()
    {
        return AssignedWorkOrders
            .Where(wo => wo.Status != WorkOrderStatus.Completed
                      && wo.Status != WorkOrderStatus.Cancelled)
            .Count();
    }

    /// <summary>
    /// LINQ Example: Get delayed work orders
    /// </summary>
    public List<WorkOrder> GetDelayedWorkOrders()
    {
        return AssignedWorkOrders
            .Where(wo => wo.IsDelayed)
            .OrderBy(wo => wo.ScheduledEndDate)
            .ToList();
    }

    /// <summary>
    /// LINQ Example: Calculate total estimated hours
    /// </summary>
    public decimal GetTotalEstimatedHours()
    {
        return AssignedWorkOrders
            .Where(wo => wo.Status == WorkOrderStatus.InProgress)
            .Sum(wo => wo.EstimatedLaborHours);
    }

    /// <summary>
    /// LINQ Example: Check if any high-priority jobs
    /// </summary>
    public bool HasHighPriorityJobs()
    {
        return AssignedWorkOrders
            .Any(wo => wo.Priority == WorkOrderPriority.Critical
                    && wo.Status != WorkOrderStatus.Completed);
    }

    /// <summary>
    /// LINQ Example: Group by status
    /// </summary>
    public Dictionary<WorkOrderStatus, int> GetWorkOrdersByStatus()
    {
        return AssignedWorkOrders
            .GroupBy(wo => wo.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
```

**LINQ Query vs Method Syntax**:
```csharp
// Query syntax (SQL-like)
var delayed = from wo in AssignedWorkOrders
              where wo.IsDelayed
              orderby wo.ScheduledEndDate
              select wo;

// Method syntax (functional)
var delayed = AssignedWorkOrders
    .Where(wo => wo.IsDelayed)
    .OrderBy(wo => wo.ScheduledEndDate);

// Both produce same result - choose based on readability
```

### 9. Async/Await

**What**: Asynchronous programming for I/O operations

```csharp
// Synchronous (blocks thread) - DON'T DO THIS
public WorkOrderDto CreateWorkOrder(CreateWorkOrderRequest request)
{
    var customer = _unitOfWork.Customers.GetById(request.CustomerId);
    var workOrder = WorkOrder.Create(...);
    _unitOfWork.SaveChanges(); // Blocks until DB write completes
    return MapToDto(workOrder);
}

// Asynchronous (non-blocking) - CORRECT WAY
public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderRequest request)
{
    // await returns control to caller while waiting for DB
    var customer = await _unitOfWork.Customers.GetByIdAsync(request.CustomerId);

    var workOrder = WorkOrder.Create(...);

    // Thread not blocked - can handle other requests
    await _unitOfWork.SaveChangesAsync();

    return MapToDto(workOrder);
}

// Calling async method
var dto = await _workOrderService.CreateWorkOrderAsync(request);
```

**Rules for Async/Await**:
1. ✅ Use `async Task<T>` for methods returning value
2. ✅ Use `async Task` for void methods
3. ✅ Add "Async" suffix to method name
4. ✅ Await all async calls
5. ❌ Don't use `async void` (except event handlers)
6. ❌ Don't block on async code (no `.Result` or `.Wait()`)

**Real-World Example**:
```csharp
// Bad: Blocking async code (deadlock risk)
var customer = _customerRepository.GetByIdAsync(id).Result; // ❌

// Good: Properly awaiting
var customer = await _customerRepository.GetByIdAsync(id); // ✅

// Bad: Not awaiting (fire and forget - error silently lost)
_emailService.SendNotificationAsync(email); // ❌

// Good: Awaiting to handle errors
await _emailService.SendNotificationAsync(email); // ✅
```

### 10. Lambda Expressions

**What**: Anonymous functions (inline methods)

```csharp
// Lambda syntax
(parameters) => expression

// Examples:

// No parameters
() => Console.WriteLine("Hello")

// One parameter (parentheses optional)
x => x * 2
(x) => x * 2  // Same thing

// Multiple parameters
(x, y) => x + y

// Statement body (multiple lines)
(x, y) => {
    var sum = x + y;
    Console.WriteLine($"Sum: {sum}");
    return sum;
}

// Real-world usage in LINQ
var activeJobs = AssignedWorkOrders
    .Where(wo => wo.Status == WorkOrderStatus.InProgress)  // Lambda
    .Select(wo => new { wo.WorkOrderNumber, wo.Description })  // Lambda
    .ToList();

// Equivalent to:
var activeJobs = AssignedWorkOrders
    .Where(WorkOrderIsInProgress)  // Method reference
    .Select(MapToAnonymous)
    .ToList();

bool WorkOrderIsInProgress(WorkOrder wo)
{
    return wo.Status == WorkOrderStatus.InProgress;
}
```

---

## DDD Tactical Patterns

### 1. Aggregate Root Pattern

**What**: Entity that controls access to a cluster of related objects

**Problem**: Without aggregates, any code can modify any entity, leading to:
- Inconsistent state
- No transaction boundaries
- No invariant enforcement

**Solution**: Aggregate Root controls all access to child entities

```csharp
// ❌ BAD: Direct access to child entities
public class BadWorkOrder
{
    public List<WorkOrderPart> RequiredParts { get; set; }
}

// Anyone can do this:
workOrder.RequiredParts.Add(new WorkOrderPart { ... }); // No validation!

// ✅ GOOD: Aggregate Root pattern
public class WorkOrder  // Aggregate Root
{
    // Private backing field
    private readonly List<WorkOrderPart> _requiredParts;

    // Read-only public view
    public IReadOnlyCollection<WorkOrderPart> RequiredParts => _requiredParts.AsReadOnly();

    private WorkOrder()
    {
        _requiredParts = new List<WorkOrderPart>();
    }

    /// <summary>
    /// ONLY way to add parts - enforces business rules
    /// </summary>
    public void AddRequiredPart(Guid partId, int quantity, bool isAvailable)
    {
        // BUSINESS RULE: Validate quantity
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive");

        // BUSINESS RULE: Can't add parts to completed work order
        if (Status == WorkOrderStatus.Completed)
            throw new InvalidOperationException("Cannot modify completed work order");

        // BUSINESS RULE: Check for duplicates
        var existing = _requiredParts.FirstOrDefault(p => p.PartId == partId);
        if (existing != null)
            throw new InvalidOperationException($"Part {partId} already added");

        // Create child entity through aggregate
        var workOrderPart = WorkOrderPart.Create(Id, partId, quantity, isAvailable);
        _requiredParts.Add(workOrderPart);

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Remove part - also through aggregate root
    /// </summary>
    public void RemoveRequiredPart(Guid partId)
    {
        var part = _requiredParts.FirstOrDefault(p => p.PartId == partId);
        if (part == null)
            throw new ArgumentException($"Part {partId} not found");

        _requiredParts.Remove(part);
        UpdatedAt = DateTime.UtcNow;
    }
}

// Usage
var workOrder = WorkOrder.Create(...);
workOrder.AddRequiredPart(partId, 2, true);  // Validated!
```

**Rules for Aggregates**:
1. ✅ External code references aggregate root by ID
2. ✅ Child entities accessed only through root
3. ✅ Transaction boundary = aggregate boundary
4. ✅ Invariants enforced by aggregate root
5. ❌ Never expose child entities for modification

### 2. Entity vs Value Object

**Entity**: Has identity, mutable, tracked over time

```csharp
// ENTITY: WorkOrder
// Identity: Id (Guid)
// Mutable: Status can change, parts can be added
// Tracked: We care about this specific work order over time
public class WorkOrder
{
    public Guid Id { get; private set; }  // Identity
    public WorkOrderStatus Status { get; private set; }  // Mutable state

    public void UpdateStatus(WorkOrderStatus newStatus)
    {
        // State changes tracked over time
        Status = newStatus;
    }
}

// Two work orders with same data are DIFFERENT
var wo1 = WorkOrder.Create(...);
var wo2 = WorkOrder.Create(...);
Console.WriteLine(wo1 == wo2);  // False - different identity
```

**Value Object**: No identity, immutable, compared by value

```csharp
// VALUE OBJECT: Money (not in current system, but should be)
public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative");

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required");

        Amount = amount;
        Currency = currency;
    }

    // Operators for value objects
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot add different currencies");

        return new Money(a.Amount + b.Amount, a.Currency);
    }

    // Value objects are immutable - return new instance
    public Money MultiplyBy(decimal multiplier)
    {
        return new Money(Amount * multiplier, Currency);
    }
}

// Usage
var price = new Money(100, "USD");
var tax = new Money(10, "USD");
var total = price + tax;  // New Money object

// Two Money objects with same amount/currency are EQUAL
var m1 = new Money(100, "USD");
var m2 = new Money(100, "USD");
Console.WriteLine(m1 == m2);  // True - same value

// VALUE OBJECT: Address
public record Address(
    string Street,
    string City,
    string State,
    string PostalCode)
{
    public string FullAddress => $"{Street}, {City}, {State} {PostalCode}";
}

// Usage in entity
public class Customer
{
    public Address ShippingAddress { get; private set; }
    public Address BillingAddress { get; private set; }

    public void UpdateShippingAddress(Address newAddress)
    {
        // Value object - replace entire object, don't modify
        ShippingAddress = newAddress;
    }
}
```

**When to Use Each**:
| Use Entity When | Use Value Object When |
|-----------------|----------------------|
| Identity matters | Value matters |
| Tracked over time | Replaceable |
| Mutable state | Immutable |
| Reference equality | Value equality |
| Example: WorkOrder, Customer | Example: Money, Address, DateRange |

### 3. Repository Pattern

**What**: Abstraction over data access

**Problem**: Without repository:
- Domain logic mixed with SQL/EF queries
- Hard to test (can't mock database)
- Violates Single Responsibility Principle

```csharp
// ❌ BAD: Domain service with direct DB access
public class BadWorkOrderService
{
    private readonly DbContext _db;

    public async Task<WorkOrder> GetWorkOrderAsync(Guid id)
    {
        // SQL/EF logic mixed with business logic
        return await _db.WorkOrders
            .Include(wo => wo.Customer)
            .Include(wo => wo.RequiredParts)
            .FirstOrDefaultAsync(wo => wo.Id == id);
    }
}
```

**Solution**: Repository interface + implementation

```csharp
// Interface in Domain layer (no dependencies)
public interface IWorkOrderRepository : IRepository<WorkOrder>
{
    Task<WorkOrder> GetByIdAsync(Guid id);
    Task<WorkOrder> GetByWorkOrderNumberAsync(string workOrderNumber);
    Task<List<WorkOrder>> GetByCustomerAsync(Guid customerId);
    Task<List<WorkOrder>> GetByTechnicianAsync(Guid technicianId);
    Task<List<WorkOrder>> GetDelayedAsync();
    Task AddAsync(WorkOrder workOrder);
}

// Implementation in Infrastructure layer
public class WorkOrderRepository : Repository<WorkOrder>, IWorkOrderRepository
{
    public WorkOrderRepository(HeavyIMSDbContext context) : base(context)
    {
    }

    public async Task<WorkOrder> GetByIdAsync(Guid id)
    {
        return await _context.WorkOrders
            .Include(wo => wo.Customer)
            .Include(wo => wo.RequiredParts)
            .Include(wo => wo.Notifications)
            .FirstOrDefaultAsync(wo => wo.Id == id);
    }

    public async Task<List<WorkOrder>> GetDelayedAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.WorkOrders
            .Where(wo => wo.ScheduledEndDate.HasValue
                      && wo.ScheduledEndDate.Value < now
                      && wo.Status != WorkOrderStatus.Completed
                      && wo.Status != WorkOrderStatus.Cancelled)
            .OrderBy(wo => wo.ScheduledEndDate)
            .ToList();
    }

    public async Task AddAsync(WorkOrder workOrder)
    {
        await _context.WorkOrders.AddAsync(workOrder);
    }
}

// Usage in Application Service
public class WorkOrderService
{
    private readonly IWorkOrderRepository _workOrderRepo;

    public WorkOrderService(IWorkOrderRepository workOrderRepo)
    {
        _workOrderRepo = workOrderRepo;
    }

    public async Task<WorkOrderDto> GetWorkOrderAsync(Guid id)
    {
        // Clean - no EF/SQL knowledge needed
        var workOrder = await _workOrderRepo.GetByIdAsync(id);

        if (workOrder == null)
            throw new NotFoundException($"Work order {id} not found");

        return MapToDto(workOrder);
    }
}
```

**Benefits**:
- ✅ Domain layer has no EF/SQL dependencies
- ✅ Easy to test (mock IRepository)
- ✅ Can swap data source (SQL → NoSQL)
- ✅ Centralized query logic

### 4. Unit of Work Pattern

**What**: Manages transactions across multiple repositories

```csharp
// Interface
public interface IUnitOfWork : IDisposable
{
    // All repositories
    IWorkOrderRepository WorkOrders { get; }
    ICustomerRepository Customers { get; }
    ITechnicianRepository Technicians { get; }
    IPartRepository Parts { get; }
    IInventoryRepository Inventory { get; }

    // Single save point
    Task<int> SaveChangesAsync();
}

// Implementation
public class UnitOfWork : IUnitOfWork
{
    private readonly HeavyIMSDbContext _context;

    public IWorkOrderRepository WorkOrders { get; }
    public ICustomerRepository Customers { get; }
    // ... other repositories

    public UnitOfWork(HeavyIMSDbContext context)
    {
        _context = context;

        // All repositories share same context (same transaction)
        WorkOrders = new WorkOrderRepository(context);
        Customers = new CustomerRepository(context);
        Technicians = new TechnicianRepository(context);
        Parts = new PartRepository(context);
        Inventory = new InventoryRepository(context);
    }

    public async Task<int> SaveChangesAsync()
    {
        // Single transaction for all changes
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

// Usage - Multiple aggregates in one transaction
public async Task ReservePartsForWorkOrderAsync(Guid workOrderId, List<PartRequest> parts)
{
    // Get work order
    var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(workOrderId);

    foreach (var partReq in parts)
    {
        // Get inventory
        var inventory = await _unitOfWork.Inventory.GetByPartAsync(partReq.PartId);

        // Domain operations
        inventory.ReserveParts(partReq.Quantity, workOrderId, "system");
        workOrder.AddRequiredPart(partReq.PartId, partReq.Quantity, true);
    }

    // Single transaction commits all changes
    await _unitOfWork.SaveChangesAsync();
    // If any operation fails, ALL changes are rolled back
}
```

---

## SOLID Principles in Action

### S - Single Responsibility Principle

**What**: Each class should have one reason to change

```csharp
// ❌ BAD: WorkOrder knows about notifications
public class BadWorkOrder
{
    public void CompleteWorkOrder()
    {
        Status = WorkOrderStatus.Completed;

        // Mixing concerns - WorkOrder shouldn't know about emails
        var emailService = new EmailService();
        emailService.SendEmail(Customer.Email, "Work order completed");
    }
}

// ✅ GOOD: Separation of concerns
public class WorkOrder
{
    // Only domain logic
    public void Complete()
    {
        Status = WorkOrderStatus.Completed;
        ActualEndDate = DateTime.UtcNow;

        // Raise domain event instead
        Events.Add(new WorkOrderCompletedEvent(Id, CustomerId));
    }
}

// Application Service handles coordination
public class WorkOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;

    public async Task CompleteWorkOrderAsync(Guid id)
    {
        var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(id);

        // Domain operation
        workOrder.Complete();

        await _unitOfWork.SaveChangesAsync();

        // Infrastructure operation (separate concern)
        await _notifications.SendCompletionNotificationAsync(workOrder);
    }
}
```

### D - Dependency Inversion Principle

**What**: Depend on abstractions, not concretions

```csharp
// ❌ BAD: Depends on concrete implementation
public class BadWorkOrderService
{
    private readonly WorkOrderRepository _repo;  // Concrete class

    public BadWorkOrderService()
    {
        _repo = new WorkOrderRepository(new HeavyIMSDbContext());  // Hard-coded
    }
}

// ✅ GOOD: Depends on interface
public class WorkOrderService
{
    private readonly IWorkOrderRepository _repo;  // Interface

    public WorkOrderService(IWorkOrderRepository repo)  // Injected
    {
        _repo = repo;
    }
}

// ASP.NET Core DI Container wires it up
services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
services.AddScoped<IWorkOrderService, WorkOrderService>();
```

---

## Key Takeaways

### C# Features Essential for DDD

1. **Properties with private setters** - Encapsulation
2. **Private constructors + factory methods** - Controlled creation
3. **ICollection interfaces** - Flexibility
4. **Enums** - Type-safe constants
5. **LINQ** - Query business logic
6. **Async/Await** - Scalable I/O
7. **Pattern matching** - Readable business rules

### DDD Patterns

1. **Aggregate Root** - Transaction boundary
2. **Entity** - Identity + mutable state
3. **Value Object** - Immutable value
4. **Repository** - Data access abstraction
5. **Unit of Work** - Transaction management
6. **Factory Method** - Controlled creation
7. **Domain Events** - Cross-aggregate communication

### Best Practices

- ✅ Keep domain pure (no EF/HTTP dependencies)
- ✅ Use interfaces for repositories
- ✅ Validate in domain layer
- ✅ Coordinate in application layer
- ✅ Make illegal states unrepresentable
- ✅ Favor immutability (Value Objects)
- ✅ Explicit is better than implicit
