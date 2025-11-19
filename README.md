# 🏗️ Heavy Industry Management System (HeavyIMS)

A complete **production-ready .NET backend solution** demonstrating Domain-Driven Design (DDD) architecture and modern backend development practices.

---

## 📋 What This Project Demonstrates

### ✅ Key Technologies & Patterns

| Requirement | Implementation | Location |
|------------|----------------|----------|
| **.NET & C#** | Full .NET 9.0 solution | All layers |
| **Entity Framework** | EF Core 9.0 with Fluent API | `Infrastructure/` |
| **LINQ to SQL** | Complex queries | `Repositories/` |
| **Azure** | SQL Server, Redis Cache, Key Vault, User Secrets | `Program.cs`, `appsettings.json` |
| **Git** | Version control ready | `.git/` |
| **DI/IoC** | Constructor injection throughout | `Program.cs` |
| **Configuration Management** | appsettings.json, User Secrets, Key Vault | `API/` |
| **Caching** | Redis distributed cache | `Services/` |
| **Domain-Driven Design** | DDD with aggregate boundaries, domain events, value objects | All layers |
| **Domain Events** | Event-driven architecture for cross-aggregate communication | `Domain/Events/`, `Infrastructure/Events/` |
| **Value Objects** | Immutable types (Money, Address, DateRange, EquipmentIdentifier) | `Domain/ValueObjects/` |
| **CI/CD** | GitHub Actions pipeline | `.github/workflows/` |
| **OOD** | SOLID principles | All code |
| **Unit Testing** | xUnit + Moq + FluentAssertions | `Tests/UnitTests/` |
| **Integration Testing** | Real SQL Server integration tests | `Tests/IntegrationTests/` |

---

## 🎯 Heavy Industry Challenges Solved

### 1. **Technician Shortage & Workload Imbalance**
**Solution:** Digital Dashboards & Drag-and-Drop Scheduling

**Implementation:**
- `Technician.cs`: Entity with capacity management
- `TechnicianRepository.cs`: Queries for available technicians
- `WorkOrderService.AssignWorkOrderAsync()`: Assignment logic
- Real-time workload calculation: `GetWorkloadPercentage()`

**API Endpoint:**
```
POST /api/workorders/{id}/assign
```

---

### 2. **Parts Delays & Inventory Chaos**
**Solution:** Real-Time Multi-Warehouse Inventory Tracking & Automated Alerts

**Implementation:**
- **Part Catalog Aggregate** (`Part.cs`): Product catalog with pricing, supplier info, categorization
- **Inventory Operational Aggregate** (`Inventory.cs`): Multi-warehouse stock management
  - Warehouse-specific stock levels with min/max thresholds per location
  - Quantity on hand, quantity reserved, quantity available
  - Full transaction history audit trail
- **Domain Events** (`InventoryEvents.cs`): Event-driven alerts and automation
  - `InventoryLowStockDetected`: Triggers when stock falls below minimum
  - `InventoryReserved`, `InventoryIssued`, `InventoryReceived`: Track inventory movements
  - `InventoryAdjusted`: Audit trail for manual corrections
- **Event Handlers** (`InventoryLowStockDetectedHandler.cs`): Automated responses
  - Logs critical low stock alerts
  - Ready for email/SMS notifications to purchasing team
  - Foundation for automated reorder suggestions
- `PartRepository.cs` & `InventoryRepository.cs`: Catalog and warehouse queries
- `PartService.cs` & `InventoryService.cs`: Business logic coordination
- `PartsController.cs` & `InventoryController.cs`: RESTful APIs

**Key Methods:**
```csharp
// Part catalog operations
var part = Part.Create(partNumber, name, description, category, unitCost, unitPrice);
part.UpdatePricing(newCost, newPrice);
part.Discontinue();

// Inventory operations (per warehouse)
var inventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", min: 10, max: 50);
inventory.ReceiveParts(20, "admin", "PO-12345");
inventory.ReserveParts(5, workOrderId, "tech@example.com");
inventory.IssueParts(5, workOrderId, "tech@example.com");
inventory.IsLowStock();  // Returns true if below minimum

// Multi-warehouse queries
var total = await inventoryRepo.GetTotalQuantityOnHandAsync(partId);  // All warehouses
var available = await inventoryRepo.GetTotalAvailableQuantityAsync(partId);  // Unreserved stock
var lowStock = await inventoryRepo.GetLowStockInventoryAsync();  // All low stock locations
```

**API Endpoints:**
```
GET    /api/parts                    - List/search parts catalog
GET    /api/parts/{id}               - Get part with all warehouse inventory
POST   /api/parts                    - Create new part
PUT    /api/parts/{id}/pricing       - Update pricing

GET    /api/inventory                - List inventory (filter by warehouse)
GET    /api/inventory/lowstock       - Low stock alerts
GET    /api/inventory/part/{partId}  - All warehouse locations for a part
POST   /api/inventory/reserve        - Reserve parts for work order
POST   /api/inventory/issue          - Issue parts to work order
POST   /api/inventory/receive        - Receive parts from supplier
```

---

### 3. **Inefficient Communication**
**Solution:** Automated Customer & Team Notifications

**Implementation:**
- `INotificationService`: Interface for notifications
- `WorkOrderNotification.cs`: Notification audit trail
- Triggered on status changes, assignments, delays
- Support for Email, SMS, Push notifications

**Trigger Points:**
```csharp
// In WorkOrderService
await _notificationService.SendWorkOrderCreatedNotificationAsync(...);
await _notificationService.SendWorkOrderAssignedNotificationAsync(...);
await _notificationService.SendWorkOrderStatusChangedNotificationAsync(...);
```

---

### 4. **Manual Processes & Paperwork**
**Solution:** Mobile-Responsive Platforms & Digital Workflows

**Implementation:**
- RESTful API for web/mobile consumption
- Digital work order creation and tracking
- Status updates via API
- Mobile-friendly JSON responses

**API Design:**
```
GET    /api/workorders          - List all work orders
POST   /api/workorders          - Create work order
GET    /api/workorders/{id}     - Get details
PUT    /api/workorders/{id}/status - Update status
```

---

### 5. **Estimating & Diagnostics**
**Solution:** Integrated Labor Guides & Diagnostics Data

**Implementation:**
- `WorkOrder.SetEstimate()`: Labor time and cost estimates
- `WorkOrder.RecordActualTime()`: Actual vs. estimated tracking
- Support for OEM data integration (API structure in place)
- VIN decoder support (ready for integration)

**Methods:**
```csharp
workOrder.SetEstimate(laborHours: 8, estimatedCost: 1200);
workOrder.RecordActualTime(actualHours: 9, actualCost: 1350);
// Track accuracy for future estimates
```

---

## 🏗️ Architecture

### DDD Layers

```
┌──────────────────────────────────────┐
│         HeavyIMS.API                 │  ← Controllers, DI/IoC, Middleware
│         (Presentation Layer)         │
└─────────────┬────────────────────────┘
              │
┌─────────────▼────────────────────────┐
│      HeavyIMS.Application            │  ← Services, DTOs, Use Cases
│      (Application Layer)             │
└─────────────┬────────────────────────┘
              │
┌─────────────▼────────────────────────┐
│       HeavyIMS.Domain                │  ← Entities, Business Rules
│       (Domain Layer)                 │
└─────────────┬────────────────────────┘
              │
┌─────────────▼────────────────────────┐
│    HeavyIMS.Infrastructure           │  ← EF Core, Repositories, Data
│    (Infrastructure Layer)            │
└──────────────────────────────────────┘
```

**Why DDD?**
- ✅ Clean separation of concerns
- ✅ Business logic isolated and testable
- ✅ Easy to maintain and extend
- ✅ Follows SOLID principles

---

## 🔑 Key Design Patterns

### 1. Repository Pattern
```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}
```

**Benefits:**
- Abstracts data access
- Easy to mock for testing
- Centralizes queries

---

### 2. Unit of Work Pattern
```csharp
using (var unitOfWork = new UnitOfWork(context))
{
    var workOrder = await unitOfWork.WorkOrders.GetByIdAsync(id);
    var technician = await unitOfWork.Technicians.GetByIdAsync(techId);

    workOrder.AssignTechnician(technician);

    await unitOfWork.SaveChangesAsync();  // Atomic transaction
}
```

**Benefits:**
- Transaction management
- All changes succeed or fail together
- Coordinates multiple repositories

---

### 3. Dependency Injection
```csharp
public class WorkOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IDistributedCache cache,
        ILogger<WorkOrderService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _cache = cache;
        _logger = logger;
    }
}
```

**Benefits:**
- Loose coupling
- Easy to test (inject mocks)
- Configuration-based swapping

---

## 🧪 Testing

### Unit Tests (Fast, Isolated)
```bash
# Run unit tests
dotnet test --filter "FullyQualifiedName~UnitTests"
```

**Example:**
```csharp
[Fact]
public async Task CreateWorkOrderAsync_WithValidData_ShouldCreateWorkOrder()
{
    // Arrange: Setup mocks
    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var service = new WorkOrderService(mockUnitOfWork.Object, ...);

    // Act: Execute
    var result = await service.CreateWorkOrderAsync(dto);

    // Assert: Verify
    result.Should().NotBeNull();
    mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
}
```

### Integration Tests (Realistic)
```bash
# Run integration tests
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

**Example:**
```csharp
[Fact]
public async Task CreateWorkOrder_ShouldPersistToDatabase()
{
    // Uses REAL SQL Server database (not in-memory)
    var workOrder = WorkOrder.Create(...);
    await _unitOfWork.WorkOrders.AddAsync(workOrder);
    await _unitOfWork.SaveChangesAsync();

    var retrieved = await _unitOfWork.WorkOrders.GetByIdAsync(workOrder.Id);
    retrieved.Should().NotBeNull();
}
```

**Test Status:** 56/57 passing (1 skipped - cache test)

**Integration Test Features:**
- Uses real SQL Server database for accurate testing
- Sequential execution to prevent test interference (`DatabaseCollection`)
- Unique test prefixes for data isolation
- Proper cleanup respecting foreign key constraints
- Connection strings stored in User Secrets for security

---

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- SQL Server (LocalDB, Express, or Azure SQL)
- Redis (optional, for caching)
- Visual Studio 2022 or VS Code

### Quick Start

1. **Clone and restore:**
```bash
git clone <repository>
cd HeavyIMS
dotnet restore
```

2. **Update connection string in `appsettings.json`:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;"
  }
}
```

3. **Apply database migrations:**
```bash
cd HeavyIMS.API
dotnet ef database update --project ../HeavyIMS.Infrastructure
```

4. **Run the API:**
```bash
dotnet run --project HeavyIMS.API
```

5. **Open Swagger UI:**
```
https://localhost:5001
```

---

## 📚 Project Structure

```
HeavyIMS/
├── HeavyIMS.Domain/                    # Business entities & rules (NO dependencies)
│   ├── Entities/
│   │   ├── Technician.cs              # Scheduling entity
│   │   ├── WorkOrder.cs               # Core work entity
│   │   ├── Part.cs                    # Part catalog aggregate (NEW)
│   │   ├── Inventory.cs               # Warehouse inventory aggregate (NEW)
│   │   └── Customer.cs                # Customer management
│   ├── Events/                         # Domain events (NEW)
│   │   ├── PartEvents.cs              # Catalog domain events
│   │   └── InventoryEvents.cs         # Inventory domain events
│   └── Interfaces/
│       ├── IRepository.cs             # Generic repository
│       ├── IPartRepository.cs         # Part catalog queries (NEW)
│       └── IInventoryRepository.cs    # Inventory queries (NEW)
│
├── HeavyIMS.Application/              # Business logic layer
│   ├── Services/
│   │   ├── WorkOrderService.cs        # Work order operations
│   │   ├── PartService.cs             # Part catalog operations (NEW)
│   │   └── InventoryService.cs        # Inventory operations (NEW)
│   ├── DTOs/
│   │   ├── WorkOrderDtos.cs           # Work order contracts
│   │   ├── PartDtos.cs                # Part contracts (NEW)
│   │   └── InventoryDtos.cs           # Inventory contracts (NEW)
│   └── Interfaces/
│       ├── IWorkOrderService.cs       # Service contracts
│       ├── IPartService.cs            # Part service contract (NEW)
│       └── IInventoryService.cs       # Inventory service contract (NEW)
│
├── HeavyIMS.Infrastructure/           # Data access layer
│   ├── Data/
│   │   └── HeavyIMSDbContext.cs       # EF Core context
│   ├── Repositories/
│   │   ├── Repository.cs              # Generic repository
│   │   ├── TechnicianRepository.cs    # Specialized queries
│   │   ├── PartRepository.cs          # Part catalog repository (NEW)
│   │   ├── InventoryRepository.cs     # Inventory repository (NEW)
│   │   └── UnitOfWork.cs              # Transaction coordinator
│   ├── Configurations/
│   │   ├── TechnicianConfiguration.cs # EF Fluent API
│   │   ├── PartConfiguration.cs       # Part EF config (NEW)
│   │   └── InventoryConfiguration.cs  # Inventory EF config (NEW)
│   └── Migrations/                     # EF Core migrations
│       ├── 20251116022329_SeparatePartAndInventoryAggregates.cs (NEW)
│       └── 20251116025251_RemoveLegacyInventoryPartTables.cs (NEW)
│
├── HeavyIMS.API/                      # Web API
│   ├── Controllers/
│   │   ├── WorkOrdersController.cs    # Work order endpoints
│   │   ├── PartsController.cs         # Part catalog endpoints (NEW)
│   │   └── InventoryController.cs     # Inventory endpoints (NEW)
│   ├── Program.cs                     # DI/IoC configuration
│   └── appsettings.json               # Configuration
│
└── HeavyIMS.Tests/                    # Automated tests
    ├── UnitTests/
    │   ├── WorkOrderServiceTests.cs   # Work order unit tests
    │   ├── PartServiceTests.cs        # Part service tests (NEW)
    │   └── InventoryServiceTests.cs   # Inventory service tests (NEW)
    └── IntegrationTests/
        ├── WorkOrderIntegrationTests.cs # Work order integration tests
        ├── PartRepositoryTests.cs     # Part repository tests (NEW)
        ├── InventoryRepositoryTests.cs # Inventory tests (NEW)
        └── DatabaseCollection.cs      # Test collection config (NEW)
```

---

## 📖 Additional Documentation

- [Implementation Guide](IMPLEMENTATION_GUIDE.md) - Detailed build instructions
- [Refactoring Summary](REFACTORING_SUMMARY.md) - DDD aggregate refactoring details
- [Refactoring Complete](REFACTORING_COMPLETE.md) - Completion summary
- [Claude Code Guide](CLAUDE.md) - Development guidance for AI assistants
- [API Documentation](https://localhost:5001/swagger) - Interactive API docs (Scalar)

---

## 🛠️ Built With

- **.NET 9.0** - Framework
- **C# 13** - Language
- **Entity Framework Core 9.0** - ORM
- **SQL Server** - Database
- **Redis** - Caching (distributed cache)
- **xUnit** - Testing framework
- **Moq** - Mocking library
- **FluentAssertions** - Assertion library
- **Scalar/OpenAPI** - API documentation
- **GitHub Actions** - CI/CD
- **User Secrets** - Secure configuration management

---

## 📝 License

This is a demonstration project showcasing modern .NET backend architecture and Domain-Driven Design patterns.
