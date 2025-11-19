# System Architecture Visual Diagrams

## Complete Visual Guide to HeavyIMS Architecture

This document provides visual representations of the system architecture, data flows, and design patterns.

---

## 1. Overall System Architecture (Layered DDD)

```
┌─────────────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                                  │
│  React/Angular Frontend, Mobile App, External APIs                  │
│  - HTTP Requests (JSON)                                             │
│  - Authentication (JWT tokens)                                       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ HTTPS
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                    PRESENTATION LAYER                                │
│                    HeavyIMS.API (.NET 9)                             │
│                                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │  WorkOrders  │  │  Inventory   │  │    Parts     │             │
│  │ Controller   │  │ Controller   │  │ Controller   │             │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘             │
│         │                  │                  │                      │
│         │ [DTOs]          │ [DTOs]          │ [DTOs]               │
│         │                  │                  │                      │
└─────────┼──────────────────┼──────────────────┼──────────────────────┘
          │                  │                  │
          │    Dependency Injection (Scoped)   │
          │                  │                  │
┌─────────▼──────────────────▼──────────────────▼──────────────────────┐
│                    APPLICATION LAYER                                  │
│                 HeavyIMS.Application                                  │
│                                                                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ WorkOrder    │  │  Inventory   │  │    Part      │              │
│  │  Service     │  │  Service     │  │  Service     │              │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘              │
│         │                  │                  │                       │
│         │   Uses IUnitOfWork (Repository Pattern)                    │
│         │                  │                  │                       │
│         └──────────────────┼──────────────────┘                       │
│                            │                                          │
└────────────────────────────┼──────────────────────────────────────────┘
                             │
┌────────────────────────────▼──────────────────────────────────────────┐
│                       DOMAIN LAYER                                     │
│                    HeavyIMS.Domain                                     │
│                   (No Dependencies!)                                   │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────┐          │
│  │              AGGREGATE ROOTS                             │          │
│  │                                                          │          │
│  │  ┌───────────┐  ┌───────────┐  ┌────────────┐         │          │
│  │  │ WorkOrder │  │ Customer  │  │ Technician │         │          │
│  │  │           │  │           │  │            │         │          │
│  │  │ - Status  │  │ - Email   │  │ - Capacity │         │          │
│  │  │ - Parts   │  │ - Active  │  │ - Status   │         │          │
│  │  └───────────┘  └───────────┘  └────────────┘         │          │
│  │                                                          │          │
│  │  ┌───────────┐  ┌────────────┐                         │          │
│  │  │   Part    │  │ Inventory  │                         │          │
│  │  │ (Catalog) │  │(Operations)│                         │          │
│  │  │           │  │            │                         │          │
│  │  │ - Price   │  │ - OnHand   │                         │          │
│  │  │ - Supplier│  │ - Reserved │                         │          │
│  │  └───────────┘  └────────────┘                         │          │
│  └─────────────────────────────────────────────────────────┘          │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────┐          │
│  │              ENTITIES (within aggregates)                │          │
│  │                                                          │          │
│  │  WorkOrderPart, WorkOrderNotification,                   │          │
│  │  InventoryTransaction                                    │          │
│  └─────────────────────────────────────────────────────────┘          │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────┐          │
│  │              DOMAIN EVENTS                               │          │
│  │                                                          │          │
│  │  PartPriceUpdated, InventoryLowStock,                   │          │
│  │  WorkOrderStatusChanged                                  │          │
│  └─────────────────────────────────────────────────────────┘          │
└───────────────────────────────┬────────────────────────────────────────┘
                                │
                                │ Implements Interfaces
                                │
┌───────────────────────────────▼────────────────────────────────────────┐
│                    INFRASTRUCTURE LAYER                                 │
│                  HeavyIMS.Infrastructure                                │
│                                                                         │
│  ┌──────────────────────────────────────────────────────┐              │
│  │              EF CORE DbContext                        │              │
│  │          HeavyIMSDbContext                            │              │
│  │                                                       │              │
│  │  - DbSet<WorkOrder>  - DbSet<Customer>               │              │
│  │  - DbSet<Part>       - DbSet<Inventory>              │              │
│  │  - SaveChangesAsync()                                 │              │
│  └──────────────────────────────────────────────────────┘              │
│                                                                         │
│  ┌──────────────────────────────────────────────────────┐              │
│  │           REPOSITORIES (Data Access)                  │              │
│  │                                                       │              │
│  │  WorkOrderRepository   InventoryRepository           │              │
│  │  CustomerRepository    PartRepository                │              │
│  │  TechnicianRepository                                │              │
│  └──────────────────────────────────────────────────────┘              │
│                                                                         │
│  ┌──────────────────────────────────────────────────────┐              │
│  │         UNIT OF WORK (Transaction Manager)            │              │
│  │                                                       │              │
│  │  Coordinates multiple repositories                    │              │
│  │  Single transaction for all changes                   │              │
│  └──────────────────────────────────────────────────────┘              │
└───────────────────────────────┬────────────────────────────────────────┘
                                │
                                │ SQL Commands
                                │
┌───────────────────────────────▼────────────────────────────────────────┐
│                        DATABASE LAYER                                   │
│                   SQL Server / Azure SQL                                │
│                                                                         │
│  Tables: WorkOrders, Customers, Technicians, Parts,                    │
│          Inventory, InventoryTransactions, WorkOrderParts               │
│                                                                         │
│  Indexes: Status, CustomerID, PartNumber, etc.                         │
│  Foreign Keys: Referential integrity                                    │
│  Stored Procedures: Complex queries (optional)                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. DDD Aggregate Boundaries

```
┌─────────────────────────────────────────────────────────────────────┐
│                        AGGREGATE ROOTS                               │
│              (Transaction & Consistency Boundaries)                  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────┐         ┌───────────────────────────┐
│    WorkOrder Aggregate      │         │   Customer Aggregate      │
│         (Root)              │         │      (Root)               │
├─────────────────────────────┤         ├───────────────────────────┤
│ + Id: Guid                  │◄────────┤ + Id: Guid                │
│ + WorkOrderNumber: string   │  ref    │ + CompanyName: string     │
│ + Status: enum              │         │ + Email: string           │
│ + CustomerId: Guid          │         │ + IsActive: bool          │
│ + AssignedTechnicianId: Guid│         │                           │
│ + EstimatedCost: decimal    │         │ + UpdateContactInfo()     │
│                             │         │ + Deactivate()            │
│ + AssignTechnician()        │         └───────────────────────────┘
│ + UpdateStatus()            │
│ + AddRequiredPart()         │
│                             │         ┌───────────────────────────┐
│ owns ▼                      │         │  Technician Aggregate     │
│ ┌─────────────────────────┐ │         │      (Root)               │
│ │ WorkOrderPart (Entity)  │ │         ├───────────────────────────┤
│ │ - PartId: Guid          │ │◄────────┤ + Id: Guid                │
│ │ - QuantityRequired: int │ │  ref    │ + FullName: string        │
│ │ - IsAvailable: bool     │ │         │ + SkillLevel: enum        │
│ │ - ReservedAt: DateTime? │ │         │ + MaxConcurrentJobs: int  │
│ └─────────────────────────┘ │         │ + IsActive: bool          │
│                             │         │                           │
│ owns ▼                      │         │ + CanAcceptNewJob(): bool │
│ ┌─────────────────────────┐ │         │ + GetWorkloadPct(): dec   │
│ │WorkOrderNotification(E) │ │         └───────────────────────────┘
│ │ - NotificationType: enum│ │
│ │ - RecipientEmail: string│ │
│ │ - SentAt: DateTime      │ │
│ │ - WasSuccessful: bool   │ │
│ └─────────────────────────┘ │
└─────────────────────────────┘

┌─────────────────────────────┐         ┌───────────────────────────┐
│    Part Aggregate           │         │  Inventory Aggregate      │
│  (Catalog Context)          │         │ (Operations Context)      │
├─────────────────────────────┤         ├───────────────────────────┤
│ + PartId: Guid              │◄────────┤ + InventoryId: Guid       │
│ + PartNumber: string        │  ref    │ + PartId: Guid            │
│ + PartName: string          │         │ + Warehouse: string       │
│ + UnitCost: decimal         │         │ + BinLocation: string     │
│ + UnitPrice: decimal        │         │ + QuantityOnHand: int     │
│ + SupplierId: Guid?         │         │ + QuantityReserved: int   │
│ + IsDiscontinued: bool      │         │                           │
│                             │         │ + GetAvailableQty(): int  │
│ + UpdatePricing()           │         │ + ReserveParts()          │
│ + Discontinue()             │         │ + IssueParts()            │
│ + SetDefaultStockLevels()   │         │ + ReceiveParts()          │
│                             │         │                           │
│ NO owned entities           │         │ owns ▼                    │
│ (catalog data only)         │         │ ┌───────────────────────┐ │
└─────────────────────────────┘         │ │ InventoryTransaction  │ │
                                        │ │       (Entity)        │ │
    Why Separate?                       │ │ - TransactionType     │ │
    ✓ Different teams                   │ │ - Quantity            │ │
    ✓ Different change freq             │ │ - WorkOrderId         │ │
    ✓ Multi-warehouse support           │ │ - TransactionDate     │ │
    ✓ Independent scaling               │ │ - TransactionBy       │ │
                                        │ └───────────────────────┘ │
                                        └───────────────────────────┘

Legend:
  ◄──── ref    = Cross-aggregate reference (by ID only)
  owns ▼       = Owned entity (part of aggregate boundary)
  (Root)       = Aggregate Root
  (Entity)     = Entity within aggregate
```

---

## 3. Complete Request Flow: Creating a Work Order

```
┌─────────┐
│  USER   │
└────┬────┘
     │ 1. HTTP POST /api/workorders
     │    Content-Type: application/json
     │    Authorization: Bearer <token>
     │    Body: {
     │      "customerId": "abc-123",
     │      "equipmentVIN": "CAT987654321",
     │      "description": "Hydraulic pump leaking",
     │      "priority": "High"
     │    }
     ▼
┌─────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                          │
│  WorkOrdersController.cs                                     │
│                                                              │
│  [HttpPost]                                                  │
│  public async Task<ActionResult<WorkOrderDto>>               │
│      CreateWorkOrder([FromBody] CreateWorkOrderRequest req)  │
│  {                                                           │
│      // 2. Model validation (ASP.NET)                        │
│      if (!ModelState.IsValid)                                │
│          return BadRequest();                                │
│                                                              │
│      // 3. Call application service                          │
│      var dto = await _workOrderService                       │
│          .CreateWorkOrderAsync(req);                         │
│                                                              │
│      // 13. Return HTTP 201 Created                          │
│      return CreatedAtAction(nameof(Get), new { id }, dto);   │
│  }                                                           │
└──────────────────────┬───────────────────────────────────────┘
                       │
                       │ 4. Service method call
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  APPLICATION LAYER                                           │
│  WorkOrderService.cs                                         │
│                                                              │
│  public async Task<WorkOrderDto> CreateWorkOrderAsync(...)   │
│  {                                                           │
│      // 5. Validate customer exists (cross-aggregate)        │
│      var customer = await _unitOfWork.Customers              │
│          .GetByIdAsync(customerId);                          │
│                                                              │
│      if (customer == null || !customer.IsActive)             │
│          throw new InvalidOperationException(...);           │
│                                                              │
│      // 6. Use domain factory method                         │
│      var workOrder = WorkOrder.Create(                       │
│          equipmentVIN,                                       │
│          equipmentType,                                      │
│          customerId,                                         │
│          description,                                        │
│          priority,                                           │
│          createdBy                                           │
│      );                                                      │
│                                                              │
│      // 11. Add to repository                                │
│      await _unitOfWork.WorkOrders.AddAsync(workOrder);       │
│                                                              │
│      // 12. Persist (transaction)                            │
│      await _unitOfWork.SaveChangesAsync();                   │
│                                                              │
│      return MapToDto(workOrder);                             │
│  }                                                           │
└──────────────────────┬──────────┬───────────────────────────┘
                       │          │
        ┌──────────────┘          └──────────────┐
        │ 5. GetByIdAsync()                       │ 11. AddAsync()
        ▼                                         ▼
┌─────────────────────────┐              ┌──────────────────────┐
│ INFRASTRUCTURE          │              │  DOMAIN LAYER        │
│ CustomerRepository      │              │  WorkOrder.cs        │
│                         │              │                      │
│ public async Task<      │              │  public static       │
│   Customer>             │              │  WorkOrder Create()  │
│   GetByIdAsync(id)      │              │  {                   │
│ {                       │              │    // 7. Validate    │
│   return await          │              │    if (VIN == null)  │
│     _context.Customers  │              │      throw ...       │
│     .FindAsync(id);     │              │                      │
│ }                       │              │    // 8. Generate WO#│
│                         │              │    var number =      │
└────────┬────────────────┘              │      GenerateNumber()│
         │                                │                      │
         │ EF Core LINQ                   │    // 9. Set initial│
         ▼                                │    // state          │
┌──────────────────────┐                  │    return new        │
│ EF Core DbContext    │                  │      WorkOrder {     │
│                      │                  │        Id = Guid..., │
│ _context.Customers   │                  │        Status =      │
│   .FindAsync(id)     │                  │          Pending,    │
│                      │                  │        ...           │
│ SQL Query:           │                  │      };              │
│ SELECT * FROM        │                  │  }                   │
│   Customers          │                  │  // 10. Returns new  │
│ WHERE Id = @id       │                  │  //     aggregate    │
└──────────────────────┘                  └──────────────────────┘

         │                                         │
         │ 11. AddAsync(workOrder)                 │
         ▼                                         │
┌──────────────────────────────────────────────────┴──────────┐
│  INFRASTRUCTURE LAYER                                        │
│  UnitOfWork.cs                                               │
│                                                              │
│  public async Task<int> SaveChangesAsync()                   │
│  {                                                           │
│      // 12a. EF Core Change Tracker detects new entity       │
│      //      (EntityState.Added)                             │
│                                                              │
│      // 12b. Generate SQL INSERT                             │
│      //      BEGIN TRANSACTION;                              │
│      //      INSERT INTO WorkOrders (Id, Number, ...)        │
│      //      VALUES ('...', 'WO-2025-12345', ...);           │
│      //      COMMIT;                                         │
│                                                              │
│      return await _context.SaveChangesAsync();               │
│  }                                                           │
└──────────────────────┬───────────────────────────────────────┘
                       │
                       │ SQL INSERT
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  DATABASE                                                    │
│  SQL Server                                                  │
│                                                              │
│  Table: WorkOrders                                           │
│  ┌──────────┬─────────────────┬──────────┬────────────────┐ │
│  │ Id       │ WorkOrderNumber │ Status   │ CustomerId     │ │
│  ├──────────┼─────────────────┼──────────┼────────────────┤ │
│  │ guid-789 │ WO-2025-12345   │ Pending  │ abc-123        │ │
│  └──────────┴─────────────────┴──────────┴────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. Part Reservation Flow (Multiple Aggregates)

```
┌──────────────────────────────────────────────────────────────┐
│  SCENARIO: Reserve hydraulic pump for work order             │
│  Demonstrates coordination of 3 aggregates                    │
└──────────────────────────────────────────────────────────────┘

APPLICATION SERVICE COORDINATES:

┌────────────────────────────────────────────────────────────┐
│  AddPartsToWorkOrderAsync(workOrderId, parts)               │
│                                                             │
│  Step 1: Get WorkOrder Aggregate                            │
│  ┌──────────────────────────────────┐                       │
│  │  var workOrder = await           │                       │
│  │    _unitOfWork.WorkOrders        │                       │
│  │    .GetByIdAsync(workOrderId);   │                       │
│  └──────────────────────────────────┘                       │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────┐                                     │
│  │  WorkOrder         │                                     │
│  │  Status: Assigned  │                                     │
│  └────────────────────┘                                     │
│                                                             │
│  Step 2: Get Part (Catalog Aggregate)                       │
│  ┌──────────────────────────────────┐                       │
│  │  var part = await                │                       │
│  │    _unitOfWork.Parts             │                       │
│  │    .GetByPartNumberAsync("HYD-1")│                       │
│  └──────────────────────────────────┘                       │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────┐                                     │
│  │  Part              │                                     │
│  │  PartNumber: HYD-1 │                                     │
│  │  UnitPrice: $1200  │                                     │
│  │  Discontinued: No  │                                     │
│  └────────────────────┘                                     │
│           │                                                  │
│           │ Validate not discontinued                       │
│           ▼                                                  │
│  Step 3: Get Inventory (Operations Aggregate)               │
│  ┌──────────────────────────────────┐                       │
│  │  var inventory = await           │                       │
│  │    _unitOfWork.Inventory         │                       │
│  │    .GetByPartAndWarehouseAsync(  │                       │
│  │      part.PartId,                │                       │
│  │      "Main-Warehouse");          │                       │
│  └──────────────────────────────────┘                       │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────┐                                     │
│  │  Inventory         │                                     │
│  │  PartId: HYD-1     │                                     │
│  │  Warehouse: Main   │                                     │
│  │  OnHand: 10        │                                     │
│  │  Reserved: 3       │                                     │
│  │  Available: 7      │ ← Calculated (OnHand - Reserved)   │
│  └────────────────────┘                                     │
│           │                                                  │
│           │ Check available >= requested                    │
│           ▼                                                  │
│  Step 4: DOMAIN METHOD - Reserve Parts                      │
│  ┌──────────────────────────────────┐                       │
│  │  inventory.ReserveParts(         │                       │
│  │    quantity: 1,                  │                       │
│  │    workOrderId,                  │                       │
│  │    "system");                    │                       │
│  └──────────────────────────────────┘                       │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────────────────────────────┐             │
│  │ DOMAIN LOGIC in Inventory.ReserveParts(): │             │
│  │                                            │             │
│  │ // Business rule validation                │             │
│  │ if (quantity > GetAvailableQuantity())     │             │
│  │   throw new InvalidOperationException(...);│             │
│  │                                            │             │
│  │ // Update aggregate state                  │             │
│  │ QuantityReserved += quantity;              │             │
│  │                                            │             │
│  │ // Create audit record (owned entity)      │             │
│  │ var transaction =                          │             │
│  │   InventoryTransaction.CreateReservation(  │             │
│  │     InventoryId, quantity, workOrderId);   │             │
│  │ Transactions.Add(transaction);             │             │
│  └────────────────────────────────────────────┘             │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────┐                                     │
│  │  Inventory         │                                     │
│  │  OnHand: 10        │ ← No change                         │
│  │  Reserved: 4       │ ← Incremented (+1)                  │
│  │  Available: 6      │ ← Now 6                             │
│  │                    │                                     │
│  │  Transactions:     │                                     │
│  │    [new] Type: Reservation                              │
│  │          Quantity: 1                                    │
│  │          WorkOrderId: ...                                │
│  └────────────────────┘                                     │
│                                                             │
│  Step 5: DOMAIN METHOD - Add Part to WorkOrder              │
│  ┌──────────────────────────────────┐                       │
│  │  workOrder.AddRequiredPart(      │                       │
│  │    part.PartId,                  │                       │
│  │    quantity: 1,                  │                       │
│  │    isAvailable: true);           │                       │
│  └──────────────────────────────────┘                       │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────────────────────────────┐             │
│  │ DOMAIN LOGIC in WorkOrder.AddRequiredPart: │             │
│  │                                            │             │
│  │ // Validate quantity                       │             │
│  │ if (quantity <= 0) throw ...               │             │
│  │                                            │             │
│  │ // Create child entity                     │             │
│  │ var woPart = WorkOrderPart.Create(         │             │
│  │   Id, partId, quantity, isAvailable);      │             │
│  │ RequiredParts.Add(woPart);                 │             │
│  └────────────────────────────────────────────┘             │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────┐                                     │
│  │  WorkOrder         │                                     │
│  │  RequiredParts:    │                                     │
│  │    [new] PartId: HYD-1                                  │
│  │          Quantity: 1                                    │
│  │          IsAvailable: true                               │
│  └────────────────────┘                                     │
│                                                             │
│  Step 6: SAVE (Transaction for all 3 aggregates)            │
│  ┌──────────────────────────────────┐                       │
│  │  await _unitOfWork               │                       │
│  │    .SaveChangesAsync();          │                       │
│  └──────────────────────────────────┘                       │
└─────────────────┬──────────────────────────────────────────┘
                  │
                  ▼
┌──────────────────────────────────────────────────────────────┐
│  EF CORE CHANGE TRACKER                                       │
│                                                              │
│  Detected Changes:                                           │
│  ✓ Inventory: Modified (Reserved increased)                  │
│  ✓ InventoryTransaction: Added                               │
│  ✓ WorkOrderPart: Added                                      │
│  ✓ WorkOrder: Modified (UpdatedAt changed)                   │
└─────────────────┬────────────────────────────────────────────┘
                  │
                  ▼
┌──────────────────────────────────────────────────────────────┐
│  SQL TRANSACTION                                             │
│                                                              │
│  BEGIN TRANSACTION;                                          │
│                                                              │
│  -- Update Inventory aggregate                               │
│  UPDATE Inventory                                            │
│  SET QuantityReserved = 4,                                   │
│      UpdatedAt = '2025-01-17 11:00:00'                       │
│  WHERE InventoryId = 'inv-123';                              │
│                                                              │
│  -- Insert InventoryTransaction (child entity)               │
│  INSERT INTO InventoryTransactions                           │
│    (TransactionId, InventoryId, TransactionType,             │
│     Quantity, WorkOrderId, TransactionDate)                  │
│  VALUES                                                      │
│    ('trans-456', 'inv-123', 'Reservation',                   │
│     1, 'wo-789', '2025-01-17 11:00:00');                     │
│                                                              │
│  -- Insert WorkOrderPart (child entity)                      │
│  INSERT INTO WorkOrderParts                                  │
│    (Id, WorkOrderId, PartId, QuantityRequired, IsAvailable)  │
│  VALUES                                                      │
│    ('wop-321', 'wo-789', 'part-111', 1, 1);                  │
│                                                              │
│  -- Update WorkOrder aggregate                               │
│  UPDATE WorkOrders                                           │
│  SET UpdatedAt = '2025-01-17 11:00:00'                       │
│  WHERE Id = 'wo-789';                                        │
│                                                              │
│  COMMIT;  -- All or nothing!                                │
└──────────────────────────────────────────────────────────────┘
```

**What This Demonstrates**:
- ✅ Application Service coordinates 3 aggregates
- ✅ Business rules enforced in domain layer
- ✅ Each aggregate maintains its own consistency
- ✅ Single transaction ensures atomicity
- ✅ Audit trail created automatically
- ✅ Cross-aggregate references by ID only

---

## 5. Technology Stack

```
┌─────────────────────────────────────────────────────────────┐
│                    HEAVYIMS STACK                            │
└─────────────────────────────────────────────────────────────┘

RUNTIME
├─ .NET 9.0
│  ├─ C# 12 (latest language features)
│  ├─ ASP.NET Core (web framework)
│  └─ Dependency Injection (built-in IoC)

API LAYER
├─ ASP.NET Core Web API
│  ├─ Controllers (HTTP routing)
│  ├─ Model Binding (JSON ↔ DTOs)
│  ├─ Authentication/Authorization (JWT)
│  └─ Scalar (API documentation)

APPLICATION SERVICES
├─ .NET Class Library
│  ├─ Use case orchestration
│  ├─ DTOs (Data Transfer Objects)
│  └─ Interfaces for dependency inversion

DOMAIN LAYER
├─ .NET Class Library (pure C#)
│  ├─ Entities & Aggregates
│  ├─ Domain Events
│  ├─ Business Rules
│  └─ NO external dependencies

ORM & DATA ACCESS
├─ Entity Framework Core 9.0
│  ├─ DbContext (database session)
│  ├─ LINQ Provider (query translation)
│  ├─ Change Tracker (automatic UPDATE/INSERT/DELETE)
│  ├─ Migrations (schema versioning)
│  └─ SQL Server Provider

DATABASE
├─ Microsoft SQL Server / Azure SQL
│  ├─ Tables (WorkOrders, Customers, Parts, Inventory, etc.)
│  ├─ Foreign Keys (referential integrity)
│  ├─ Indexes (query performance)
│  └─ Transactions (ACID guarantees)

TESTING
├─ xUnit (test framework)
├─ Moq (mocking library)
├─ FluentAssertions (readable assertions)
└─ Integration Tests (real SQL Server)

CACHING (configured but optional)
└─ Redis
   ├─ Distributed cache
   └─ Session storage

CI/CD
└─ GitHub Actions
   ├─ Build & Test
   ├─ Docker images
   └─ Azure deployment
```

---

## Key Architectural Decisions

### 1. Why Separate Part and Inventory?

```
BEFORE (Monolithic)             AFTER (Separate Aggregates)
┌─────────────────┐             ┌─────────┐    ┌──────────┐
│ InventoryPart   │             │  Part   │    │Inventory │
├─────────────────┤             ├─────────┤    ├──────────┤
│ PartNumber      │             │ Number  │    │ PartId   │
│ PartName        │             │ Name    │    │ OnHand   │
│ UnitPrice       │             │ Price   │    │ Reserved │
│ OnHand          │             │ Supplier│    │ Location │
│ Reserved        │             └─────────┘    └──────────┘
│ MinStock        │                  ▲              ▲
└─────────────────┘                  │              │
                                 Catalog Team   Warehouse Team
Problem:
✗ Can't support multiple warehouses            ✓ Each warehouse independent
✗ Price change locks inventory                 ✓ No contention
✗ Mixed responsibilities                       ✓ Clear separation
```

### 2. Why Repository Pattern?

```
WITHOUT REPOSITORY              WITH REPOSITORY
┌─────────────────┐             ┌─────────────────┐
│ WorkOrderService│             │ WorkOrderService│
├─────────────────┤             ├─────────────────┤
│ // SQL in       │             │ // Clean logic  │
│ // service!     │             │                 │
│ var sql = "..." │             │ var wo = await  │
│ var cmd = new   │             │   _repo         │
│   SqlCommand... │             │   .GetByIdAsync │
│                 │             │   (id);         │
└─────────────────┘             └─────────────────┘
                                         ▲
                                         │
                                ┌────────┴─────────┐
                                │   IRepository    │ Interface
                                │   (Domain)       │
                                └──────────────────┘
                                         ▲
                                         │ Implements
                                ┌────────┴─────────┐
                                │ EF Repository    │ Concrete
                                │ (Infrastructure) │
                                └──────────────────┘

Benefits:
✓ Domain layer has no EF dependencies
✓ Easy to test (mock interface)
✓ Can swap implementations
✓ Centralized query logic
```

---

## Complete File Structure

```
HeavyIMS/
│
├── HeavyIMS.Domain/                    # Pure business logic
│   ├── Entities/
│   │   ├── WorkOrder.cs               # Aggregate Root
│   │   ├── Customer.cs                # Aggregate Root
│   │   ├── Technician.cs              # Aggregate Root
│   │   ├── Part.cs                    # Aggregate Root (Catalog)
│   │   └── Inventory.cs               # Aggregate Root (Operations)
│   ├── Events/
│   │   ├── DomainEvent.cs
│   │   ├── PartEvents.cs
│   │   └── InventoryEvents.cs
│   └── Interfaces/
│       └── IRepository.cs             # Repository contract
│
├── HeavyIMS.Application/               # Use cases
│   ├── Services/
│   │   ├── WorkOrderService.cs        # Orchestrates domain
│   │   ├── InventoryService.cs
│   │   └── PartService.cs
│   ├── Interfaces/
│   │   ├── IWorkOrderService.cs
│   │   ├── IInventoryService.cs
│   │   └── IPartService.cs
│   └── DTOs/
│       ├── WorkOrderDtos.cs           # Data transfer objects
│       ├── InventoryDtos.cs
│       └── PartDtos.cs
│
├── HeavyIMS.Infrastructure/            # Technical implementation
│   ├── Data/
│   │   └── HeavyIMSDbContext.cs       # EF Core context
│   ├── Configurations/
│   │   ├── WorkOrderConfiguration.cs  # Fluent API
│   │   ├── PartConfiguration.cs
│   │   └── InventoryConfiguration.cs
│   ├── Repositories/
│   │   ├── Repository.cs              # Generic base
│   │   ├── WorkOrderRepository.cs     # Specific queries
│   │   ├── PartRepository.cs
│   │   ├── InventoryRepository.cs
│   │   └── UnitOfWork.cs              # Transaction coordinator
│   └── Migrations/
│       └── 20250117_*.cs              # Database migrations
│
├── HeavyIMS.API/                       # HTTP interface
│   ├── Controllers/
│   │   ├── WorkOrdersController.cs    # REST endpoints
│   │   ├── InventoryController.cs
│   │   └── PartsController.cs
│   ├── Program.cs                     # App startup & DI
│   └── appsettings.json               # Configuration
│
└── HeavyIMS.Tests/                     # Automated tests
    ├── UnitTests/
    │   ├── WorkOrderServiceTests.cs
    │   ├── InventoryServiceTests.cs
    │   └── PartServiceTests.cs
    └── IntegrationTests/
        ├── WorkOrderIntegrationTests.cs
        └── InventoryRepositoryTests.cs
```

---

This visual guide demonstrates the complete architecture, request flows, and design decisions that make HeavyIMS a production-ready DDD application!
