# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

HeavyIMS is a **Heavy Industry Management System** built as a production-ready .NET backend solution demonstrating Domain-Driven Design (DDD) architecture. The system manages work orders, technicians, inventory parts, and customers for heavy equipment maintenance facilities.

**Target Framework:** .NET 9.0

## Common Commands

### Build and Run

```bash
# Restore dependencies for entire solution
dotnet restore

# Build the entire solution
dotnet build

# Build in Release configuration
dotnet build --configuration Release

# Run the API
dotnet run --project HeavyIMS.API

# Access Swagger/Scalar API documentation (when running)
# Navigate to: https://localhost:5001
```

### Database Migrations

```bash
# Add a new migration (run from solution root)
cd HeavyIMS.API
dotnet ef migrations add <MigrationName> --project ../HeavyIMS.Infrastructure

# Apply migrations to database
dotnet ef database update --project ../HeavyIMS.Infrastructure

# Remove last migration (if not applied)
dotnet ef migrations remove --project ../HeavyIMS.Infrastructure

# Generate SQL script from migrations
dotnet ef migrations script --project ../HeavyIMS.Infrastructure --output migration.sql
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test HeavyIMS.Tests/HeavyIMS.Tests.csproj

# Run only unit tests
dotnet test --filter "FullyQualifiedName~UnitTests"

# Run only integration tests
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests with detailed output
dotnet test --verbosity detailed

# Run a single test method
dotnet test --filter "FullyQualifiedName~<TestClassName>.<TestMethodName>"
```

### Package Management

```bash
# Add a NuGet package to specific project
dotnet add HeavyIMS.Infrastructure package <PackageName>

# Update all packages in solution
dotnet restore --force-evaluate

# List outdated packages
dotnet list package --outdated
```

## Architecture

### DDD Layered Architecture

The solution follows a strict **Domain-Driven Design** pattern with clear layer separation:

```
┌─────────────────────────────────────────┐
│      HeavyIMS.API (Presentation)        │  Controllers, API endpoints, DI configuration
│                                         │  Dependencies: Application, Infrastructure
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│   HeavyIMS.Application (Use Cases)      │  Services, DTOs, business workflows
│                                         │  Dependencies: Domain only
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│     HeavyIMS.Domain (Core Business)     │  Entities, interfaces, business rules
│                                         │  Dependencies: NONE (pure domain logic)
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│ HeavyIMS.Infrastructure (Data Access)   │  EF Core, repositories, DbContext
│                                         │  Dependencies: Domain
└─────────────────────────────────────────┘
```

**Key Architectural Principles:**
- **Domain layer has NO dependencies** - pure business logic
- **Application layer orchestrates** domain operations and coordinates workflows
- **Infrastructure layer implements** repository interfaces defined in Domain
- **API layer wires everything together** via dependency injection

### Design Patterns in Use

1. **Repository Pattern**: Abstracts data access behind interfaces (`IRepository<T>`, specialized repositories)
2. **Unit of Work Pattern**: Coordinates transactions across multiple repositories (`UnitOfWork.cs`)
3. **Domain Events Pattern**: Cross-aggregate communication without tight coupling (`AggregateRoot`, event handlers)
4. **Dependency Injection**: Constructor injection throughout, configured in `Program.cs`
5. **Domain-Driven Design**: Rich domain entities with business logic encapsulation
6. **Value Objects**: Immutable types for domain concepts (`Money`, `Address`, `EquipmentIdentifier`, `DateRange`)

### Key Domain Entities

**Core Aggregates:**
- **WorkOrder**: Core entity managing repair/maintenance work
- **Technician**: Represents service technicians with scheduling capabilities
- **Customer**: Customer information and work order associations

**Inventory Management (Separate Aggregates - DDD Refactoring):**
- **Part** (Catalog Aggregate): Product catalog, pricing, supplier relationships
- **Inventory** (Operational Aggregate): Stock levels per warehouse location, reservations, movements

**Note**: The system has been refactored to separate catalog data (Part) from operational inventory (Inventory) following DDD best practices. This enables multi-warehouse support and better separation of concerns.

**Legacy**: `InventoryPart` entity (deprecated) combined both catalog and inventory - kept for backward compatibility.

## Development Workflow

### Adding New Features

When adding new features to this codebase:

1. **Start in Domain layer**: Create/modify entities with business rules
2. **Add repository methods**: If new queries needed, extend repository interfaces and implementations
3. **Implement in Application layer**: Create service methods that orchestrate domain logic
4. **Expose via API**: Add controller endpoints
5. **Write tests**: Unit tests for services, integration tests for repositories

### Database Changes

When modifying entity models:

1. Update entity classes in `HeavyIMS.Domain/Entities/`
2. Update or create EF configurations in `HeavyIMS.Infrastructure/Configurations/`
3. Generate migration: `dotnet ef migrations add <DescriptiveName>`
4. Review generated migration code
5. Apply to database: `dotnet ef database update`

### Testing Strategy

- **Unit Tests** (`HeavyIMS.Tests/UnitTests/`): Mock all dependencies, test business logic in isolation
- **Integration Tests** (`HeavyIMS.Tests/IntegrationTests/`): Use real SQL Server database to test repository/EF configurations

**Test Dependencies:**
- xUnit: Test framework
- Moq: Mocking framework for unit tests
- FluentAssertions: Readable assertion syntax
- Microsoft.Extensions.Configuration.UserSecrets: Secure connection string management for integration tests
- Real SQL Server: More realistic integration testing (not in-memory)

**Test Status:** 56/57 passing (1 skipped - cache test)

**Integration Test Features:**
- Uses real SQL Server database for accurate testing
- Sequential execution via `DatabaseCollection` to prevent test interference
- Unique test prefixes for data isolation
- Proper cleanup in Dispose methods respecting foreign key constraints
- User secrets for secure connection string storage

## Configuration

### Connection Strings

Update `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;",
    "Redis": "localhost:6379"
  }
}
```

For Azure deployment, connection strings are typically stored in Azure Key Vault or App Service configuration.

### Service Lifetimes

Configured in `Program.cs`:
- **Scoped**: DbContext, Repositories, Services (one instance per HTTP request)
- **Singleton**: Configuration, Caching
- **Transient**: Lightweight, stateless services

## CI/CD Pipeline

GitHub Actions workflow: `.github/workflows/dotnet-ci-cd.yml`

**Pipeline stages:**
1. Build and Test
2. Code Quality Analysis
3. Build Docker Image (optional)
4. Deploy to Staging (on `develop` branch)
5. Deploy to Production (on `main` branch)
6. Database Migration

## Important Notes

- This is a **demonstration project** showcasing backend development skills
- The solution uses **.NET 9.0** (note: CI/CD workflow references .NET 6.0 and may need updating)
- API uses **Scalar** for API documentation instead of traditional Swagger UI
- **Redis caching** is configured but implementation may be incomplete

**Recent DDD Enhancements:**
- ✅ **Domain Events**: Core infrastructure complete (~85%), low stock handler implemented
- ✅ **Value Objects**: Domain entities updated with Money, Address, EquipmentIdentifier, DateRange
- ✅ **Aggregate Root**: Base class for event collection in Part and Inventory aggregates
- 🚧 **EF Core Configuration**: Value object owned entity mappings in progress
- 🚧 **Event Handlers**: Additional handlers for Part events and WorkOrder events (future work)

**Build Status:** ✅ Solution builds successfully (56/57 tests passing)

## DDD Aggregate Refactoring (Part vs. Inventory)

The codebase has been refactored to separate inventory management into two distinct aggregates:

### Why Separate Aggregates?

**Before:** Single `InventoryPart` entity combined catalog data with inventory tracking
- Problem: Can't support multiple warehouses elegantly
- Problem: Catalog changes (price updates) mixed with operational changes (stock movements)
- Problem: High contention when updating inventory

**After:** Separate `Part` (catalog) and `Inventory` (operational) aggregates
- ✅ Multi-warehouse support: Each Part can have multiple Inventory locations
- ✅ Independent lifecycles: Catalog team updates Parts, warehouse team updates Inventory
- ✅ Better scalability: No contention between warehouses
- ✅ Clear bounded contexts: Catalog vs. Operations

### Working with Separate Aggregates

```csharp
// Create a part in the catalog
var part = Part.Create("HYD-001", "Hydraulic Pump", "Main pump", "Hydraulics", 1200m, 1800m);
await unitOfWork.Parts.AddAsync(part);
await unitOfWork.SaveChangesAsync();

// Create inventory locations for that part (different warehouses)
var warehouse1Inventory = Inventory.Create(part.PartId, "Warehouse-Main", "A-12-3", 10, 50);
var warehouse2Inventory = Inventory.Create(part.PartId, "Warehouse-East", "B-05-1", 5, 30);
await unitOfWork.Inventory.AddAsync(warehouse1Inventory);
await unitOfWork.Inventory.AddAsync(warehouse2Inventory);
await unitOfWork.SaveChangesAsync();

// Reserve parts from specific warehouse
var inventory = await unitOfWork.Inventory.GetByPartAndWarehouseAsync(part.PartId, "Warehouse-Main");
inventory.ReserveParts(5, workOrderId, "user@example.com");
await unitOfWork.SaveChangesAsync();

// Get total quantity across all warehouses
var totalQuantity = await unitOfWork.Inventory.GetTotalQuantityOnHandAsync(part.PartId);
```

### Cross-Aggregate Queries

When you need both catalog and inventory data:

```csharp
// Application Service coordinates the two aggregates
var part = await unitOfWork.Parts.GetByIdAsync(partId);
var inventories = await unitOfWork.Inventory.GetByPartIdAsync(partId);

// Combine in DTO for API response
var dto = new PartWithInventoryDto
{
    PartInfo = MapPartToDto(part),
    TotalQuantity = inventories.Sum(i => i.QuantityOnHand),
    Locations = inventories.Select(MapInventoryToDto)
};
```

### Domain Events (Implemented)

The system implements the **Domain Events pattern** for cross-aggregate communication without tight coupling.

**Core Infrastructure:**
- `AggregateRoot` base class collects events from domain operations
- `IDomainEventDispatcher` publishes events to registered handlers
- `UnitOfWork` dispatches events AFTER successful database save (transactional consistency)
- Events are in-process, synchronous, and occur within the same transaction

**Available Events:**

**Part Events** (`PartEvents.cs`):
- `PartCreated` - New part added to catalog
- `PartPriceUpdated` - Catalog pricing changed (tracks old/new cost and price)
- `PartDiscontinued` - Part no longer available

**Inventory Events** (`InventoryEvents.cs`):
- `InventoryLowStockDetected` - Stock fell below minimum (CRITICAL for preventing work order delays)
- `InventoryReserved` - Parts reserved for work order
- `InventoryIssued` - Parts physically issued to technician
- `InventoryReceived` - Parts received from supplier
- `InventoryAdjusted` - Manual inventory correction (cycle count)

**Event Flow:**
1. Domain method executes (e.g., `Inventory.IssueParts()`)
2. Method raises event via `RaiseDomainEvent()` (collected in aggregate)
3. `UnitOfWork.SaveChangesAsync()` persists changes to database
4. If successful, dispatcher publishes events to handlers
5. Handlers execute (failures logged but don't stop other handlers)
6. Events cleared from aggregates

**Example Usage:**
```csharp
// In domain entity
public void IssueParts(int quantity, Guid workOrderId, string userName)
{
    // Business logic...
    QuantityOnHand -= quantity;

    // Raise event for cross-aggregate communication
    RaiseDomainEvent(new InventoryIssued(
        InventoryId, PartId, workOrderId, Warehouse,
        quantity, QuantityOnHand
    ));

    // Check for low stock
    if (QuantityOnHand < MinimumStockLevel)
    {
        RaiseDomainEvent(new InventoryLowStockDetected(
            InventoryId, PartId, Warehouse,
            QuantityOnHand, MinimumStockLevel, ReorderQuantity
        ));
    }
}
```

**Implemented Handlers:**
- `InventoryLowStockDetectedHandler` - Logs alerts, ready for email/SMS notifications

**Registration** (in `Program.cs`):
```csharp
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IDomainEventHandler<InventoryLowStockDetected>,
    InventoryLowStockDetectedHandler>();
```

For full details, see `DOMAIN_EVENTS_INTEGRATION_STATUS.md`.

### Value Objects (Implemented)

The system uses **Value Objects** to eliminate primitive obsession and encapsulate domain concepts.

**Implemented Value Objects:**

1. **Money** (`Money.cs`) - Monetary amounts with currency
   - Properties: `Amount` (decimal), `Currency` (string, default "USD")
   - Validates non-negative amounts
   - Supports arithmetic operations (Add, Subtract, Multiply)
   - Equality based on amount AND currency

2. **Address** (`Address.cs`) - Physical addresses
   - Properties: `Street`, `Street2`, `City`, `State`, `ZipCode`, `Country`
   - Validates required fields and formats (ZIP code, state)
   - Provides formatted full address string
   - Backwards compatible parsing from simple strings

3. **EquipmentIdentifier** (`EquipmentIdentifier.cs`) - Equipment identity
   - Properties: `VIN` (17-char validated), `Type`, `Model`
   - Enforces VIN format validation
   - Encapsulates equipment identification logic

4. **DateRange** (`DateRange.cs`) - Time periods
   - Properties: `Start`, `End`
   - Validates end >= start
   - Calculates duration in hours/days
   - Checks for overlaps with other date ranges

**Usage in Entities:**

**Before (Primitive Obsession):**
```csharp
public class WorkOrder
{
    public decimal EstimatedCost { get; set; }  // What currency?
    public string EquipmentVIN { get; set; }     // Validation?
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }  // Separate validation
}
```

**After (Value Objects):**
```csharp
public class WorkOrder : AggregateRoot
{
    public Money EstimatedCost { get; private set; }  // Currency-aware
    public EquipmentIdentifier Equipment { get; private set; }  // VIN validated
    public DateRange? ScheduledPeriod { get; private set; }  // Always valid range
}
```

**Benefits:**
- **Type Safety**: Can't assign VIN to customer name or add money to hours
- **Validation**: Format/range validation happens at value object creation
- **Immutability**: Thread-safe and predictable behavior
- **Rich Behavior**: `GetProfitMargin()`, `DurationInHours()`, `Overlaps()` live where they belong
- **Self-Documenting**: Code reads like business language

**Status:** Domain entities updated, EF Core configuration in progress. See `VALUE_OBJECTS_INTEGRATION_STATUS.md` for details.

## Advanced DDD Concepts in HeavyIMS

This project demonstrates several advanced Domain-Driven Design patterns:

### 1. Aggregate Boundaries
- **Part** and **Inventory** are separate aggregates (not a single entity)
- Each aggregate has its own lifecycle and consistency boundary
- Cross-aggregate references use IDs only (not object references)

### 2. Domain Events for Cross-Aggregate Communication
- Aggregates communicate via events, not direct method calls
- Maintains aggregate boundaries and loose coupling
- Transactional consistency (events fire AFTER successful save)

### 3. Value Objects for Domain Concepts
- Eliminates primitive obsession
- Encapsulates validation and behavior
- Immutable and equality-based comparison

### 4. Rich Domain Models
- Business logic lives in entities, not services
- Services orchestrate, entities enforce rules
- Example: `Inventory.IssueParts()` validates stock, raises events, updates state

### 5. Repository Pattern with Unit of Work
- Abstracts persistence
- Coordinates transactions across multiple aggregates
- Collects and dispatches domain events after successful commit

### 6. Ubiquitous Language
- Code uses business terms: `WorkOrder`, `Technician`, `IssueParts`, `ReserveParts`
- Not technical terms: `Record`, `Transaction`, `UpdateQuantity`

**Key Design Decisions:**
- Why separate Part and Inventory aggregates? (Multi-warehouse support, separate lifecycles, reduced contention)
- Why domain events vs direct calls? (Decoupling, extensibility, aggregate boundaries)
- Why value objects vs primitives? (Type safety, validation, rich behavior)
- How to ensure consistency? (Aggregate boundaries, Unit of Work pattern, domain events after save)

## Project Structure

```
HeavyIMS/
├── HeavyIMS.Domain/              # Pure business logic (NO dependencies)
│   ├── Entities/                 # Domain entities (WorkOrder, Technician, etc.)
│   │   └── AggregateRoot.cs      # Base class for event-capable aggregates
│   ├── Events/                   # Domain events for cross-aggregate communication
│   │   ├── DomainEvent.cs        # Base event class
│   │   ├── PartEvents.cs         # Part lifecycle events
│   │   └── InventoryEvents.cs    # Inventory operation events
│   ├── ValueObjects/             # Immutable value types
│   │   ├── Money.cs              # Monetary amounts with currency
│   │   ├── Address.cs            # Physical addresses
│   │   ├── EquipmentIdentifier.cs # Equipment VIN/Type/Model
│   │   └── DateRange.cs          # Time periods
│   └── Interfaces/               # Repository and event contracts
│       ├── IRepository.cs
│       ├── IDomainEventDispatcher.cs
│       └── IDomainEventHandler.cs
├── HeavyIMS.Application/         # Business workflows
│   ├── Services/                 # Service implementations
│   ├── DTOs/                     # Data transfer objects
│   └── Interfaces/               # Service contracts
├── HeavyIMS.Infrastructure/      # Technical implementation
│   ├── Data/                     # DbContext
│   ├── Repositories/             # Repository implementations
│   ├── Events/                   # Event infrastructure
│   │   ├── DomainEventDispatcher.cs
│   │   └── Handlers/             # Domain event handlers
│   │       └── InventoryLowStockDetectedHandler.cs
│   └── Configurations/           # EF Fluent API configurations
├── HeavyIMS.API/                 # Web API
│   ├── Controllers/              # REST endpoints
│   └── Program.cs                # Application startup & DI
└── HeavyIMS.Tests/               # Automated tests
    ├── UnitTests/
    └── IntegrationTests/
```
