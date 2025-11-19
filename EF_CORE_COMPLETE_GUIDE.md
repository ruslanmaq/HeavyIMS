# Entity Framework Core Complete Guide

## Understanding EF Core Through HeavyIMS

This guide explains how Entity Framework Core works, from basic concepts to advanced patterns used in production applications.

---

## Table of Contents

1. [What is EF Core?](#what-is-ef-core)
2. [DbContext Deep Dive](#dbcontext-deep-dive)
3. [Entity Configuration](#entity-configuration)
4. [Relationships and Navigation Properties](#relationships-and-navigation-properties)
5. [Migrations](#migrations)
6. [Querying with LINQ](#querying-with-linq)
7. [Change Tracking](#change-tracking)
8. [Performance Optimization](#performance-optimization)

---

## What is EF Core?

**Entity Framework Core** is an **Object-Relational Mapper (ORM)** that:
- Maps C# classes to database tables
- Translates LINQ queries to SQL
- Tracks changes to objects
- Generates and applies database schemas

```
C# DOMAIN MODELS          EF CORE              SQL DATABASE
┌──────────────┐                              ┌──────────────┐
│  WorkOrder   │                              │  WorkOrders  │
│  - Id        │───────mapping──────────────►│  - Id        │
│  - Number    │                              │  - Number    │
│  - Status    │◄───────queries──────────────│  - Status    │
└──────────────┘                              └──────────────┘
       │                                             │
       │ LINQ:                                       │ SQL:
       │ .Where(w => w.Status == "Pending")          │ SELECT * FROM WorkOrders
       │                                             │ WHERE Status = 'Pending'
       └─────────────────────────────────────────────┘
```

### Why Use EF Core?

**Without EF Core** (Raw ADO.NET):
```csharp
// ❌ Manual SQL - Error-prone, not type-safe
public WorkOrder GetById(Guid id)
{
    using (var conn = new SqlConnection(connectionString))
    {
        conn.Open();
        var cmd = new SqlCommand(
            "SELECT Id, WorkOrderNumber, Status FROM WorkOrders WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);

        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                return new WorkOrder
                {
                    Id = reader.GetGuid(0),
                    WorkOrderNumber = reader.GetString(1),
                    Status = Enum.Parse<WorkOrderStatus>(reader.GetString(2))
                };
            }
        }
    }
    return null;
}
```

**With EF Core**:
```csharp
// ✅ Type-safe, clean, maintainable
public async Task<WorkOrder> GetByIdAsync(Guid id)
{
    return await _context.WorkOrders
        .FirstOrDefaultAsync(wo => wo.Id == id);
}
```

---

## DbContext Deep Dive

### What is DbContext?

`DbContext` is the **session** between your application and the database. It:
1. Represents a connection to the database
2. Tracks changes to entities
3. Provides querying capabilities via DbSet<T>
4. Coordinates database operations

```csharp
// File: HeavyIMS.Infrastructure/Data/HeavyIMSDbContext.cs
public class HeavyIMSDbContext : DbContext
{
    // DbSet<T> represents a table
    public DbSet<WorkOrder> WorkOrders { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Technician> Technicians { get; set; }
    public DbSet<Part> Parts { get; set; }
    public DbSet<Inventory> Inventory { get; set; }

    // Constructor injection for configuration
    public HeavyIMSDbContext(DbContextOptions<HeavyIMSDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Model configuration - Fluent API
    /// Called once when first DbContext is created
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeavyIMSDbContext).Assembly);

        // Or apply individual configurations
        modelBuilder.ApplyConfiguration(new WorkOrderConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
    }
}
```

### DbContext Lifecycle

```csharp
// 1. CREATE - DbContext instance created (typically per HTTP request)
using (var context = new HeavyIMSDbContext(options))
{
    // 2. QUERY - Load entities from database
    var workOrder = await context.WorkOrders.FindAsync(id);

    // 3. MODIFY - Change entity state (tracked by context)
    workOrder.UpdateStatus(WorkOrderStatus.InProgress);

    // 4. SAVE - Persist changes to database
    await context.SaveChangesAsync();

}  // 5. DISPOSE - Context and connection disposed

// In ASP.NET Core with DI, this is automatic:
// - Context created at start of request
// - Disposed at end of request
```

### Connection String Configuration

```csharp
// File: Program.cs or Startup.cs
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<HeavyIMSDbContext>(options =>
{
    // SQL Server provider
    options.UseSqlServer(connectionString);

    // Development: Detailed errors
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }

    // Query tracking behavior
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
});

// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;"
  }
}
```

---

## Entity Configuration

### Three Ways to Configure Entities

1. **Conventions** (implicit)
2. **Data Annotations** (attributes on entity)
3. **Fluent API** (code in configuration class) ✅ **RECOMMENDED for DDD**

### Why Fluent API for DDD?

**Problem with Data Annotations**:
```csharp
// ❌ Domain entity polluted with infrastructure concerns
[Table("WorkOrders")]
public class WorkOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; private set; }

    [Required]
    [MaxLength(50)]
    [Column("WO_Number")]
    public string WorkOrderNumber { get; private set; }
}
```

**Solution with Fluent API**:
```csharp
// ✅ Clean domain entity (no EF dependencies)
public class WorkOrder
{
    public Guid Id { get; private set; }
    public string WorkOrderNumber { get; private set; }
}

// ✅ Infrastructure configuration in separate file
public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");
        builder.HasKey(wo => wo.Id);
        builder.Property(wo => wo.WorkOrderNumber).IsRequired().HasMaxLength(50);
    }
}
```

### Complete Entity Configuration Example

```csharp
// File: HeavyIMS.Infrastructure/Configurations/WorkOrderConfiguration.cs
public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        // ═══════════════════════════════════════════════════════════
        // TABLE MAPPING
        // ═══════════════════════════════════════════════════════════
        builder.ToTable("WorkOrders");

        // ═══════════════════════════════════════════════════════════
        // PRIMARY KEY
        // ═══════════════════════════════════════════════════════════
        builder.HasKey(wo => wo.Id);

        // Alternative: Composite key
        // builder.HasKey(wo => new { wo.Id, wo.CompanyId });

        // ═══════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════

        // Required string with max length
        builder.Property(wo => wo.WorkOrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        // VIN with specific length
        builder.Property(wo => wo.EquipmentVIN)
            .IsRequired()
            .HasMaxLength(17)  // Standard VIN length
            .IsUnicode(false); // ASCII only (no Unicode)

        // Optional string (nullable)
        builder.Property(wo => wo.DiagnosticNotes)
            .HasMaxLength(2000);

        // Decimal with precision
        builder.Property(wo => wo.EstimatedCost)
            .HasPrecision(18, 2);  // 18 total digits, 2 after decimal

        // Enum stored as string
        builder.Property(wo => wo.Status)
            .HasConversion<string>()  // "Pending", "Completed", etc.
            .HasMaxLength(20);

        // Alternative: Enum as int
        // builder.Property(wo => wo.Priority)
        //     .HasConversion<int>();

        // DateTime with specific SQL type
        builder.Property(wo => wo.CreatedAt)
            .HasColumnType("datetime2")  // SQL Server datetime2
            .HasDefaultValueSql("GETUTCDATE()");  // Default in DB

        // ═══════════════════════════════════════════════════════════
        // INDEXES (for query performance)
        // ═══════════════════════════════════════════════════════════

        // Unique index
        builder.HasIndex(wo => wo.WorkOrderNumber)
            .IsUnique()
            .HasDatabaseName("IX_WorkOrders_Number");

        // Regular index
        builder.HasIndex(wo => wo.CustomerId)
            .HasDatabaseName("IX_WorkOrders_CustomerId");

        // Composite index
        builder.HasIndex(wo => new { wo.Status, wo.Priority })
            .HasDatabaseName("IX_WorkOrders_Status_Priority");

        // ═══════════════════════════════════════════════════════════
        // RELATIONSHIPS
        // ═══════════════════════════════════════════════════════════

        // Many-to-One: WorkOrder → Customer
        builder.HasOne(wo => wo.Customer)              // WorkOrder has one Customer
            .WithMany(c => c.WorkOrders)               // Customer has many WorkOrders
            .HasForeignKey(wo => wo.CustomerId)        // FK column
            .OnDelete(DeleteBehavior.Restrict);        // Prevent cascade delete

        // Many-to-One: WorkOrder → Technician (optional)
        builder.HasOne(wo => wo.AssignedTechnician)
            .WithMany(t => t.AssignedWorkOrders)
            .HasForeignKey(wo => wo.AssignedTechnicianId)
            .IsRequired(false)                         // Nullable FK
            .OnDelete(DeleteBehavior.SetNull);         // Set FK to NULL on delete

        // ═══════════════════════════════════════════════════════════
        // OWNED ENTITIES (Aggregate pattern)
        // ═══════════════════════════════════════════════════════════

        // One-to-Many within aggregate: WorkOrder → WorkOrderParts
        builder.OwnsMany(wo => wo.RequiredParts, parts =>
        {
            parts.ToTable("WorkOrderParts");
            parts.HasKey(p => p.Id);

            parts.Property(p => p.QuantityRequired).IsRequired();
            parts.Property(p => p.IsAvailable).IsRequired();

            // FK back to WorkOrder (implicit)
            parts.WithOwner().HasForeignKey("WorkOrderId");
        });

        // One-to-Many within aggregate: WorkOrder → WorkOrderNotifications
        builder.OwnsMany(wo => wo.Notifications, notifications =>
        {
            notifications.ToTable("WorkOrderNotifications");
            notifications.HasKey(n => n.Id);

            notifications.Property(n => n.NotificationType)
                .HasConversion<string>();

            notifications.Property(n => n.RecipientEmail)
                .HasMaxLength(255);
        });

        // ═══════════════════════════════════════════════════════════
        // QUERY FILTERS (Global filters applied to all queries)
        // ═══════════════════════════════════════════════════════════

        // Example: Soft delete filter (optional)
        // builder.HasQueryFilter(wo => !wo.IsDeleted);

        // Example: Multi-tenant filter
        // builder.HasQueryFilter(wo => wo.TenantId == _currentTenant.Id);
    }
}
```

### Inventory Configuration (Separate Aggregate)

```csharp
// File: HeavyIMS.Infrastructure/Configurations/InventoryConfiguration.cs
public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventory");
        builder.HasKey(i => i.InventoryId);

        // Reference to Part aggregate (by ID only, not navigation property)
        builder.Property(i => i.PartId).IsRequired();

        // No direct navigation property to Part - separate aggregate!
        // Cross-aggregate references are by ID only

        // Warehouse and location
        builder.Property(i => i.Warehouse).IsRequired().HasMaxLength(100);
        builder.Property(i => i.BinLocation).HasMaxLength(50);

        // Stock quantities
        builder.Property(i => i.QuantityOnHand).IsRequired();
        builder.Property(i => i.QuantityReserved).IsRequired();

        // Indexes for common queries
        builder.HasIndex(i => i.PartId);
        builder.HasIndex(i => new { i.PartId, i.Warehouse }).IsUnique();

        // Owned entity: InventoryTransactions (audit trail)
        builder.OwnsMany(i => i.Transactions, transactions =>
        {
            transactions.ToTable("InventoryTransactions");
            transactions.HasKey(t => t.TransactionId);

            transactions.Property(t => t.TransactionType)
                .HasConversion<string>();

            transactions.Property(t => t.Quantity).IsRequired();
            transactions.Property(t => t.TransactionDate).IsRequired();

            // Index for querying transaction history
            transactions.HasIndex(t => t.TransactionDate);
        });
    }
}
```

---

## Relationships and Navigation Properties

### One-to-Many Relationship

```
Customer (1) ──< (Many) WorkOrder
```

```csharp
// Principal entity (one side)
public class Customer
{
    public Guid Id { get; private set; }

    // Navigation property (collection)
    public ICollection<WorkOrder> WorkOrders { get; private set; }
}

// Dependent entity (many side)
public class WorkOrder
{
    public Guid Id { get; private set; }

    // Foreign key
    public Guid CustomerId { get; private set; }

    // Navigation property (reference)
    public Customer Customer { get; private set; }
}

// Configuration
builder.HasOne(wo => wo.Customer)
    .WithMany(c => c.WorkOrders)
    .HasForeignKey(wo => wo.CustomerId);
```

**EF Core generates SQL**:
```sql
CREATE TABLE Customers (
    Id uniqueidentifier PRIMARY KEY,
    CompanyName nvarchar(200) NOT NULL
);

CREATE TABLE WorkOrders (
    Id uniqueidentifier PRIMARY KEY,
    CustomerId uniqueidentifier NOT NULL,
    WorkOrderNumber nvarchar(50) NOT NULL,
    CONSTRAINT FK_WorkOrders_Customers FOREIGN KEY (CustomerId)
        REFERENCES Customers(Id)
);
```

### Optional Relationship (Nullable FK)

```csharp
public class WorkOrder
{
    // Nullable FK - technician might not be assigned yet
    public Guid? AssignedTechnicianId { get; private set; }
    public Technician AssignedTechnician { get; private set; }
}

// Configuration
builder.HasOne(wo => wo.AssignedTechnician)
    .WithMany(t => t.AssignedWorkOrders)
    .HasForeignKey(wo => wo.AssignedTechnicianId)
    .IsRequired(false)  // Nullable
    .OnDelete(DeleteBehavior.SetNull);  // If technician deleted, set FK to NULL
```

### Delete Behaviors

```csharp
// Cascade: Delete work orders when customer deleted
.OnDelete(DeleteBehavior.Cascade)

// Restrict: Prevent deleting customer if work orders exist
.OnDelete(DeleteBehavior.Restrict)

// SetNull: Set FK to NULL when parent deleted
.OnDelete(DeleteBehavior.SetNull)

// NoAction: Don't touch FK (application handles it)
.OnDelete(DeleteBehavior.NoAction)
```

### Owned Entities (Aggregate Pattern)

**Owned entities** are part of the aggregate, not separate tables:

```csharp
// Parent aggregate
public class Inventory
{
    public Guid InventoryId { get; private set; }

    // Owned collection - part of aggregate
    public ICollection<InventoryTransaction> Transactions { get; private set; }
}

// Owned entity (no independent existence)
public class InventoryTransaction
{
    public Guid TransactionId { get; private set; }
    // No InventoryId property needed - EF manages it
}

// Configuration
builder.OwnsMany(i => i.Transactions, transactions =>
{
    transactions.ToTable("InventoryTransactions");  // Separate table
    transactions.WithOwner().HasForeignKey("InventoryId");  // Shadow FK
});
```

**Result**:
- `InventoryTransaction` always loaded with `Inventory`
- Can't query `InventoryTransaction` independently
- Enforces aggregate boundary

---

## Migrations

### What Are Migrations?

**Migrations** are version control for your database schema.

```
CODE CHANGES              MIGRATION            DATABASE SCHEMA
┌──────────────┐                              ┌──────────────┐
│ Add property │                              │              │
│ Status       │───► dotnet ef migrations ───►│ ALTER TABLE  │
│              │     add AddStatus            │ ADD Status   │
└──────────────┘                              └──────────────┘
```

### Creating Migrations

```bash
# Navigate to project with DbContext
cd HeavyIMS.API

# Create migration
dotnet ef migrations add InitialCreate --project ../HeavyIMS.Infrastructure

# Migration files created:
# - 20250117_InitialCreate.cs (Up/Down methods)
# - 20250117_InitialCreate.Designer.cs (metadata)
# - HeavyIMSDbContextModelSnapshot.cs (current model state)
```

### Migration File Structure

```csharp
// File: Migrations/20250117123456_InitialCreate.cs
public partial class InitialCreate : Migration
{
    /// <summary>
    /// Forward migration - apply changes
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create WorkOrders table
        migrationBuilder.CreateTable(
            name: "WorkOrders",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WorkOrderNumber = table.Column<string>(maxLength: 50, nullable: false),
                EquipmentVIN = table.Column<string>(maxLength: 17, nullable: false),
                Status = table.Column<string>(maxLength: 20, nullable: false),
                CustomerId = table.Column<Guid>(nullable: false),
                EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false,
                    defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkOrders", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkOrders_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_WorkOrders_Number",
            table: "WorkOrders",
            column: "WorkOrderNumber",
            unique: true);
    }

    /// <summary>
    /// Reverse migration - undo changes
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WorkOrders");
    }
}
```

### Applying Migrations

```bash
# Apply migrations to database
dotnet ef database update --project ../HeavyIMS.Infrastructure

# Apply specific migration
dotnet ef database update AddInventoryTable --project ../HeavyIMS.Infrastructure

# Rollback to previous migration
dotnet ef database update PreviousMigration --project ../HeavyIMS.Infrastructure

# Rollback all migrations
dotnet ef database update 0 --project ../HeavyIMS.Infrastructure
```

### Migration Commands Cheat Sheet

```bash
# List migrations
dotnet ef migrations list

# Remove last migration (if not applied)
dotnet ef migrations remove

# Generate SQL script from migrations
dotnet ef migrations script --output migration.sql

# Generate script for specific range
dotnet ef migrations script FromMigration ToMigration
```

### Real-World Migration Example

**Scenario**: Add `Priority` field to existing `WorkOrders` table

```bash
# 1. Add property to entity
# WorkOrder.cs:
# public WorkOrderPriority Priority { get; private set; }

# 2. Update configuration
# WorkOrderConfiguration.cs:
# builder.Property(wo => wo.Priority).HasConversion<int>();

# 3. Create migration
dotnet ef migrations add AddWorkOrderPriority --project ../HeavyIMS.Infrastructure
```

**Generated migration**:
```csharp
public partial class AddWorkOrderPriority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "WorkOrders",
            nullable: false,
            defaultValue: 2);  // Normal priority as default
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Priority",
            table: "WorkOrders");
    }
}
```

---

## Querying with LINQ

### Basic Queries

```csharp
// Get all work orders
var allWorkOrders = await _context.WorkOrders.ToListAsync();

// Filter with Where
var pendingOrders = await _context.WorkOrders
    .Where(wo => wo.Status == WorkOrderStatus.Pending)
    .ToListAsync();

// Get single entity (throws if not found)
var workOrder = await _context.WorkOrders
    .SingleAsync(wo => wo.Id == id);

// Get single or default (null if not found)
var workOrder = await _context.WorkOrders
    .FirstOrDefaultAsync(wo => wo.Id == id);

// Find by primary key (uses cache)
var workOrder = await _context.WorkOrders.FindAsync(id);

// Count
var count = await _context.WorkOrders
    .CountAsync(wo => wo.Status == WorkOrderStatus.Pending);

// Any
bool hasDelayed = await _context.WorkOrders
    .AnyAsync(wo => wo.IsDelayed);
```

**SQL Generated**:
```sql
-- Where + ToListAsync
SELECT * FROM WorkOrders WHERE Status = 'Pending';

-- CountAsync
SELECT COUNT(*) FROM WorkOrders WHERE Status = 'Pending';

-- AnyAsync
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM WorkOrders WHERE IsDelayed = 1
) THEN 1 ELSE 0 END;
```

### Eager Loading (Include)

**Problem**: N+1 query problem
```csharp
// ❌ BAD: Causes N+1 queries
var workOrders = await _context.WorkOrders.ToListAsync();  // 1 query

foreach (var wo in workOrders)
{
    Console.WriteLine(wo.Customer.CompanyName);  // N queries!
}
```

**Solution**: Eager loading with Include
```csharp
// ✅ GOOD: Single query with JOIN
var workOrders = await _context.WorkOrders
    .Include(wo => wo.Customer)              // Load related Customer
    .Include(wo => wo.AssignedTechnician)    // Load related Technician
    .Include(wo => wo.RequiredParts)         // Load collection
    .ToListAsync();

// Nested Include
var workOrders = await _context.WorkOrders
    .Include(wo => wo.RequiredParts)
        .ThenInclude(part => part.Part)  // Not available - separate aggregate!
    .ToListAsync();
```

**SQL Generated**:
```sql
SELECT
    wo.Id, wo.WorkOrderNumber, wo.Status,
    c.Id, c.CompanyName,
    t.Id, t.FirstName, t.LastName,
    wop.Id, wop.PartId, wop.QuantityRequired
FROM WorkOrders wo
LEFT JOIN Customers c ON wo.CustomerId = c.Id
LEFT JOIN Technicians t ON wo.AssignedTechnicianId = t.Id
LEFT JOIN WorkOrderParts wop ON wo.Id = wop.WorkOrderId;
```

### Projection (Select)

**Select specific columns** (more efficient than loading entire entity):

```csharp
// DTO projection
var workOrderSummaries = await _context.WorkOrders
    .Select(wo => new WorkOrderSummaryDto
    {
        Id = wo.Id,
        WorkOrderNumber = wo.WorkOrderNumber,
        CustomerName = wo.Customer.CompanyName,
        TechnicianName = wo.AssignedTechnician.FullName,
        Status = wo.Status.ToString()
    })
    .ToListAsync();
```

**SQL Generated** (only selected columns):
```sql
SELECT
    wo.Id,
    wo.WorkOrderNumber,
    c.CompanyName AS CustomerName,
    CONCAT(t.FirstName, ' ', t.LastName) AS TechnicianName,
    wo.Status
FROM WorkOrders wo
LEFT JOIN Customers c ON wo.CustomerId = c.Id
LEFT JOIN Technicians t ON wo.AssignedTechnicianId = t.Id;
```

### Complex Queries

```csharp
// Multiple conditions
var results = await _context.WorkOrders
    .Where(wo => wo.Status == WorkOrderStatus.InProgress
              && wo.Priority == WorkOrderPriority.High
              && wo.EstimatedCost > 1000)
    .OrderByDescending(wo => wo.Priority)
    .ThenBy(wo => wo.ScheduledStartDate)
    .Take(10)
    .ToListAsync();

// SQL:
// SELECT TOP 10 *
// FROM WorkOrders
// WHERE Status = 'InProgress'
//   AND Priority = 3
//   AND EstimatedCost > 1000
// ORDER BY Priority DESC, ScheduledStartDate ASC;

// Group by
var statusCounts = await _context.WorkOrders
    .GroupBy(wo => wo.Status)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToListAsync();

// SQL:
// SELECT Status, COUNT(*) AS Count
// FROM WorkOrders
// GROUP BY Status;

// Join (cross-aggregate)
var partsNeeded = await _context.Set<WorkOrderPart>()
    .Join(_context.Parts,
        wop => wop.PartId,
        p => p.PartId,
        (wop, p) => new
        {
            WorkOrderId = wop.WorkOrderId,
            PartNumber = p.PartNumber,
            PartName = p.PartName,
            Quantity = wop.QuantityRequired
        })
    .Where(x => x.WorkOrderId == workOrderId)
    .ToListAsync();
```

---

## Change Tracking

### How Change Tracking Works

```csharp
// 1. Load entity (tracked)
var workOrder = await _context.WorkOrders.FindAsync(id);
// EntityState: Unchanged

// 2. Modify entity
workOrder.UpdateStatus(WorkOrderStatus.InProgress);
// EntityState: Modified (detected by Change Tracker)

// 3. SaveChanges generates UPDATE
await _context.SaveChangesAsync();
// SQL: UPDATE WorkOrders SET Status='InProgress', UpdatedAt=... WHERE Id=...
// EntityState: Unchanged
```

### Entity States

```csharp
EntityState.Detached   // Not tracked by context
EntityState.Unchanged  // Tracked, not modified
EntityState.Added      // New entity, will be INSERTed
EntityState.Modified   // Changed entity, will be UPDATEd
EntityState.Deleted    // Marked for deletion, will be DELETEd

// Check state
var state = _context.Entry(workOrder).State;

// Manually change state
_context.Entry(workOrder).State = EntityState.Modified;
```

### No-Tracking Queries

**For read-only scenarios** (better performance):

```csharp
// Tracked query (default)
var workOrders = await _context.WorkOrders.ToListAsync();
// Change Tracker monitors all entities

// No-tracking query (faster)
var workOrders = await _context.WorkOrders
    .AsNoTracking()
    .ToListAsync();
// Entities not tracked, can't call SaveChanges()
```

**When to use AsNoTracking()**:
- ✅ Read-only queries (DTOs, reports)
- ✅ Large datasets
- ❌ If you need to update entities

---

## Performance Optimization

### 1. Use Projections (Select)

```csharp
// ❌ BAD: Load entire entities
var orders = await _context.WorkOrders
    .Include(wo => wo.Customer)
    .ToListAsync();
// Loads all columns from WorkOrders and Customers

// ✅ GOOD: Project to DTO
var orders = await _context.WorkOrders
    .Select(wo => new WorkOrderListDto
    {
        Id = wo.Id,
        Number = wo.WorkOrderNumber,
        CustomerName = wo.Customer.CompanyName
    })
    .ToListAsync();
// Loads only needed columns
```

### 2. Use AsNoTracking for Read-Only Queries

```csharp
// ✅ Faster for reports
var report = await _context.WorkOrders
    .AsNoTracking()
    .Select(wo => new { ... })
    .ToListAsync();
```

### 3. Batch Updates (EF Core 7+)

```csharp
// ❌ BAD: Load all, modify, save
var pending = await _context.WorkOrders
    .Where(wo => wo.Status == WorkOrderStatus.Pending)
    .ToListAsync();

foreach (var wo in pending)
{
    wo.UpdateStatus(WorkOrderStatus.Cancelled);
}
await _context.SaveChangesAsync();

// ✅ GOOD: Batch update (single SQL statement)
await _context.WorkOrders
    .Where(wo => wo.Status == WorkOrderStatus.Pending)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(wo => wo.Status, WorkOrderStatus.Cancelled)
        .SetProperty(wo => wo.UpdatedAt, DateTime.UtcNow));

// SQL: UPDATE WorkOrders
//      SET Status='Cancelled', UpdatedAt=GETUTCDATE()
//      WHERE Status='Pending';
```

### 4. Indexes

```csharp
// Add indexes for frequently queried columns
builder.HasIndex(wo => wo.Status);
builder.HasIndex(wo => wo.CustomerId);
builder.HasIndex(wo => new { wo.Status, wo.Priority });
```

### 5. Pagination

```csharp
// ✅ Use Skip/Take for paging
var page = 1;
var pageSize = 20;

var workOrders = await _context.WorkOrders
    .OrderBy(wo => wo.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// SQL: SELECT * FROM WorkOrders
//      ORDER BY CreatedAt
//      OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
```

---

## Key Takeaways

### EF Core Concepts
1. **DbContext** - Database session and unit of work
2. **DbSet<T>** - Collection of entities (table)
3. **Fluent API** - Configure entities without polluting domain
4. **Migrations** - Version control for database schema
5. **Change Tracker** - Monitors entity modifications
6. **LINQ** - Type-safe queries translated to SQL

### Best Practices
- ✅ Use Fluent API for configuration (keep domain clean)
- ✅ Use async methods (scalability)
- ✅ Use AsNoTracking() for read-only queries
- ✅ Use projections (Select) when you don't need full entity
- ✅ Use Include() for related data (avoid N+1 queries)
- ✅ Use indexes for frequently queried columns
- ✅ Use migrations for schema changes (never manual SQL)

### Common Pitfalls
- ❌ N+1 query problem (forgetting Include)
- ❌ Loading too much data (not using Select)
- ❌ Tracking entities unnecessarily
- ❌ Not using async methods
- ❌ Cartesian explosion with multiple Includes
- ❌ Not disposing DbContext
