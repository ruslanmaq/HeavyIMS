# Domain-Driven Design Architecture Documentation

## HeavyIMS Domain Model Analysis

This document explains the DDD patterns used in HeavyIMS and why each class was designed as an Aggregate Root, Entity, or Value Object.

---

## Aggregates & Aggregate Roots

### 1. WorkOrder (Aggregate Root)
**File**: `HeavyIMS.Domain/Entities/WorkOrder.cs`

**Type**: Aggregate Root

**Why it's an Aggregate Root**:
- **Transactional Boundary**: WorkOrder and its child entities (WorkOrderPart, WorkOrderNotification) must be consistent as a unit
- **Lifecycle Owner**: Controls the lifecycle of parts assignments and notifications
- **Business Invariants**: Enforces rules like "cannot start work without all parts" and valid status transitions
- **External Reference Point**: Other aggregates reference WorkOrder by ID only, not directly

**What it manages**:
- Work order lifecycle (Pending → Assigned → InProgress → Completed)
- Part requirements (`WorkOrderPart` entities)
- Customer notifications (`WorkOrderNotification` entities)
- Technician assignments
- Scheduling and estimating data

**Key Business Rules**:
- Status transitions follow state machine pattern
- Cannot assign to technician at full capacity
- Cannot record actual time unless completed
- Automatically updates status when assigned

**Relationships**:
- References `Customer` by ID (cross-aggregate reference)
- References `Technician` by ID (cross-aggregate reference)
- Contains `WorkOrderPart` entities (within aggregate)
- Contains `WorkOrderNotification` entities (within aggregate)

---

### 2. Customer (Aggregate Root)
**File**: `HeavyIMS.Domain/Entities/Customer.cs`

**Type**: Aggregate Root

**Why it's an Aggregate Root**:
- **Independent Lifecycle**: Customers exist independently of work orders
- **Consistency Boundary**: Customer data and notification preferences must be consistent
- **Business Entity**: Primary business concept that owns relationships with work orders
- **External Reference Point**: WorkOrders reference Customer by ID

**What it manages**:
- Customer master data (company name, contact info)
- Communication preferences (email/SMS notifications)
- Relationship to work orders (navigation property)

**Key Business Rules**:
- Company name is required
- Active/inactive status controls business operations
- Notification preferences affect communication workflows

**Relationships**:
- Has many `WorkOrder` entities (via navigation property)
- NOT the owner of WorkOrder lifecycle (WorkOrder is separate aggregate)

**Design Note**:
- The navigation property `ICollection<WorkOrder>` is for querying convenience
- WorkOrders are modified through their own aggregate root, not through Customer

---

### 3. Technician (Aggregate Root)
**File**: `HeavyIMS.Domain/Entities/Technician.cs`

**Type**: Aggregate Root

**Why it's an Aggregate Root**:
- **Independent Lifecycle**: Technicians are managed separately from work assignments
- **Workload Business Logic**: Contains rules about capacity and availability
- **Consistency Boundary**: Skill level, capacity, and status must be consistent
- **Resource Management**: Acts as a resource in scheduling system

**What it manages**:
- Technician personal data and contact info
- Skill level and maximum concurrent job capacity
- Availability status (Available, Busy, OnLeave, etc.)
- Relationship to assigned work orders

**Key Business Rules**:
- Max concurrent jobs based on skill level (Junior: 2, Expert: 5)
- Cannot accept new jobs when at capacity or on leave
- Workload percentage calculation for dashboard
- Active/inactive status controls assignment

**Relationships**:
- Has many `WorkOrder` entities (via navigation property)
- WorkOrders reference Technician for assignment tracking

---

### 4. Part (Catalog Aggregate Root)
**File**: `HeavyIMS.Domain/Entities/Part.cs`

**Type**: Aggregate Root (Catalog Bounded Context)

**Why it's an Aggregate Root**:
- **Separate Concern**: Catalog data (what exists) vs operational data (where it is)
- **Independent Lifecycle**: Parts are created/maintained by catalog team
- **No Operational Dependencies**: Part data doesn't change based on inventory movements
- **Consistency Boundary**: Pricing, supplier info, and categorization are atomic

**What it manages**:
- Product catalog information (part number, name, description)
- Pricing data (cost and selling price)
- Supplier relationships and lead times
- Default stock level templates
- Discontinuation status

**Key Business Rules**:
- Part number is unique business identifier
- Pricing cannot be negative
- Price updates trigger domain events for downstream systems
- Discontinued parts cannot be added to new work orders
- Profit margin calculations for analysis

**Why Separate from Inventory**:
- **Multi-warehouse Support**: One Part can have multiple Inventory locations
- **Different Teams**: Catalog team manages Parts, warehouse team manages Inventory
- **Different Change Frequencies**: Prices change rarely; inventory moves constantly
- **Scalability**: No contention between warehouses

**Relationships**:
- Referenced by `Inventory` aggregate by PartId (cross-aggregate reference)
- Referenced by `WorkOrderPart` by PartId

---

### 5. Inventory (Operational Aggregate Root)
**File**: `HeavyIMS.Domain/Entities/Inventory.cs`

**Type**: Aggregate Root (Operations Bounded Context)

**Why it's an Aggregate Root**:
- **Transactional Consistency**: Stock movements and reservations must be atomic
- **Warehouse-Specific**: Each warehouse location is independent
- **Complex Business Logic**: Reservations, issues, receipts require transactional control
- **Audit Requirements**: All movements tracked via InventoryTransaction entities

**What it manages**:
- Stock quantities at specific warehouse/bin location
- Reserved vs available calculations
- Stock level thresholds (min/max, reorder points)
- Inventory transactions audit trail (`InventoryTransaction` entities)

**Key Business Rules**:
- Available = OnHand - Reserved
- Cannot reserve more than available
- Cannot issue more than reserved
- Cannot adjust below reserved quantity
- Low stock triggers domain events for reordering
- Deactivation only when empty

**Why Separate from Part**:
- **Operational Independence**: Each warehouse operates independently
- **Eventual Consistency**: Price updates from Part aggregate propagate via events
- **Transaction Isolation**: Inventory movements don't lock catalog data
- **Performance**: High-frequency operations (reservations) don't contend with catalog

**Relationships**:
- References `Part` by PartId (NOT a navigation property - separate aggregate)
- Contains `InventoryTransaction` entities (within aggregate)
- Referenced by work order logic for availability checks

---

## Entities (Non-Root)

### 6. WorkOrderPart (Entity within WorkOrder Aggregate)
**File**: `HeavyIMS.Domain/Entities/Customer.cs` ⚠️ **WRONG FILE - Should be in WorkOrder.cs**

**Type**: Entity (not an aggregate root)

**Why it's an Entity**:
- **Has Identity**: Each part requirement has unique ID
- **Part of Aggregate**: Cannot exist without parent WorkOrder
- **No Independent Lifecycle**: Created/deleted with WorkOrder
- **Business Logic**: Tracks reservation and issuance status per part

**What it represents**:
- Link between WorkOrder and Part (many-to-many with business logic)
- Quantity required for specific work order
- Availability and reservation status
- Timestamps for reservation and issuance

**Why Not an Aggregate Root**:
- Always accessed through WorkOrder
- Consistency enforced by WorkOrder aggregate
- No external systems reference WorkOrderPart directly

---

### 7. WorkOrderNotification (Entity within WorkOrder Aggregate)
**File**: `HeavyIMS.Domain/Entities/Customer.cs` ⚠️ **WRONG FILE - Should be in WorkOrder.cs**

**Type**: Entity (not an aggregate root)

**Why it's an Entity**:
- **Has Identity**: Each notification has unique ID and timestamp
- **Part of Aggregate**: Belongs to specific WorkOrder
- **Audit Trail**: Tracks notification history per work order
- **No Independent Lifecycle**: Exists only in context of WorkOrder

**What it represents**:
- Communication sent for work order status changes
- Delivery status (success/failure)
- Recipient information and message content
- Timestamp and error tracking

**Why Not an Aggregate Root**:
- Accessed through WorkOrder
- Consistency managed by WorkOrder aggregate
- Created as side effect of work order status changes

---

### 8. InventoryTransaction (Entity within Inventory Aggregate)
**File**: `HeavyIMS.Domain/Entities/Inventory.cs`

**Type**: Entity (not an aggregate root)

**Why it's an Entity**:
- **Has Identity**: Each transaction uniquely identified
- **Immutable Audit Record**: Once created, never modified
- **Part of Aggregate**: Exists within Inventory aggregate boundary
- **Lifecycle Controlled**: Created by Inventory domain methods

**What it represents**:
- Audit trail of all inventory movements
- Transaction types: Receipt, Issue, Reservation, Release, Adjustment
- Reference to work orders when applicable
- Who performed the transaction and when

**Why Not an Aggregate Root**:
- Always accessed through parent Inventory
- Enforces audit trail consistency
- No independent business operations

---

## Value Objects (Currently Missing ⚠️)

**Recommended Value Objects to Add**:

### Money (for pricing)
```csharp
public record Money(decimal Amount, string Currency)
{
    public static Money Zero => new(0, "USD");

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add different currencies");
        return new Money(Amount + other.Amount, Currency);
    }
}
```

**Why**: Ensures currency is always paired with amount, prevents mixing currencies

### Address (for Customer)
```csharp
public record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country)
{
    public string FullAddress => $"{Street}, {City}, {State} {PostalCode}";
}
```

**Why**: Encapsulates address validation and formatting logic

### EquipmentIdentifier (for WorkOrder)
```csharp
public record EquipmentIdentifier(string VIN, string Type, string Model)
{
    public bool IsValid => VIN.Length == 17; // VIN validation
}
```

**Why**: Groups related equipment data and enforces VIN format

### DateRange (for scheduling)
```csharp
public record DateRange(DateTime Start, DateTime End)
{
    public TimeSpan Duration => End - Start;
    public bool Contains(DateTime date) => date >= Start && date <= End;
}
```

**Why**: Encapsulates scheduling logic and validation

---

## Design Issues Found

### 1. ⚠️ File Organization Problem
**Issue**: `Customer.cs` contains `WorkOrderPart`, `WorkOrderNotification`, and `NotificationType`

**Problem**:
- `WorkOrderPart` and `WorkOrderNotification` are entities within the WorkOrder aggregate
- They should be in `WorkOrder.cs` with their aggregate root
- Current organization violates DDD principle: entities should be in same file as their aggregate root

**Impact**:
- Confusing to navigate
- Harder to understand aggregate boundaries
- Violates cohesion principle

**Recommendation**: Move `WorkOrderPart`, `WorkOrderNotification`, and `NotificationType` to `WorkOrder.cs`

### 2. ⚠️ Missing Value Objects
**Issue**: Primitive types used where Value Objects would provide better domain modeling

**Examples**:
- Decimal for money (should be `Money` value object with currency)
- String for addresses (should be `Address` value object)
- Multiple equipment fields (should be `EquipmentIdentifier` value object)

**Benefits of Value Objects**:
- Type safety (can't mix currencies)
- Encapsulated validation
- Immutability
- Business logic in one place

### 3. ⚠️ Navigation Properties vs References
**Issue**: Some aggregates use navigation properties to other aggregates

**Example**:
```csharp
public class WorkOrder
{
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } // Navigation property
}
```

**DDD Guideline**: Aggregates should reference each other by ID only, not navigation properties

**Why**: Prevents accidentally modifying other aggregates, enforces transaction boundaries

**Current State**: Navigation properties exist for EF Core convenience, but application layer should coordinate cross-aggregate operations

---

## Aggregate Relationship Summary

```
┌─────────────────┐         ┌──────────────────┐
│    Customer     │◄────────│    WorkOrder     │
│ (Aggregate Root)│         │ (Aggregate Root) │
└─────────────────┘         └────────┬─────────┘
                                     │ owns
                            ┌────────▼─────────────────┐
                            │  WorkOrderPart (Entity)  │
                            │WorkOrderNotification (E) │
                            └──────────────────────────┘

┌─────────────────┐         ┌──────────────────┐
│   Technician    │◄────────│    WorkOrder     │
│ (Aggregate Root)│         │ (Aggregate Root) │
└─────────────────┘         └──────────────────┘

┌─────────────────┐         ┌──────────────────┐
│      Part       │◄───ref──│    Inventory     │
│ (Catalog Root)  │         │(Operational Root)│
└─────────────────┘         └────────┬─────────┘
                                     │ owns
                            ┌────────▼──────────────────┐
                            │ InventoryTransaction (E)  │
                            └───────────────────────────┘

┌─────────────────┐         ┌──────────────────┐
│      Part       │◄───ref──│  WorkOrderPart   │
│ (Catalog Root)  │         │    (Entity)      │
└─────────────────┘         └──────────────────┘
```

**Legend**:
- Solid lines = ownership (parent aggregate owns child entity)
- `ref` = reference by ID only (cross-aggregate reference)
- `◄` = points to referenced aggregate

---

## Summary of Patterns Used

| Class | Pattern | Reason | Manages |
|-------|---------|--------|---------|
| WorkOrder | Aggregate Root | Controls work lifecycle, parts, notifications | WorkOrderPart, WorkOrderNotification entities |
| Customer | Aggregate Root | Independent customer lifecycle | Customer master data |
| Technician | Aggregate Root | Resource management, capacity rules | Technician availability |
| Part | Aggregate Root | Catalog management, pricing | Part master data |
| Inventory | Aggregate Root | Stock movements, reservations | InventoryTransaction entities |
| WorkOrderPart | Entity | Part of WorkOrder aggregate | Part-to-work-order link |
| WorkOrderNotification | Entity | Part of WorkOrder aggregate | Notification history |
| InventoryTransaction | Entity | Part of Inventory aggregate | Audit trail |
| Enums | Value Type | Immutable, no identity | Status values |

---

## Key DDD Principles Applied

1. **Aggregate Roots are Transaction Boundaries**
   - Each aggregate root enforces consistency within its boundary
   - Cross-aggregate operations coordinated by Application Services

2. **Entities Within Aggregates**
   - WorkOrderPart, WorkOrderNotification, InventoryTransaction
   - Always accessed through their aggregate root

3. **Separate Aggregates for Separate Concerns**
   - Part (catalog) vs Inventory (operations)
   - Different teams, different change frequencies, independent scaling

4. **Reference by ID Across Aggregates**
   - WorkOrder references Customer/Technician by ID
   - Inventory references Part by ID
   - Prevents accidental cross-aggregate modifications

5. **Rich Domain Models**
   - Business logic in entities (not anemic models)
   - Factory methods for creation
   - Business rule validation
   - Encapsulated state changes

---

## Recommendations for Improvement

1. **Move entities to correct files**:
   - Move `WorkOrderPart` and `WorkOrderNotification` from `Customer.cs` to `WorkOrder.cs`

2. **Introduce Value Objects**:
   - `Money` for pricing
   - `Address` for customer addresses
   - `EquipmentIdentifier` for VIN/Type/Model
   - `DateRange` for scheduling periods

3. **Remove Navigation Properties** (optional, for purist DDD):
   - Replace with ID-only references
   - Use Application Services to coordinate cross-aggregate operations

4. **Add Domain Events**:
   - Framework already exists in `HeavyIMS.Domain.Events`
   - Implement event publishing for cross-aggregate communication
   - Example: `PartPriceUpdated`, `InventoryLowStock`, `WorkOrderStatusChanged`

5. **Consider Bounded Contexts**:
   - Catalog Context (Part)
   - Operations Context (Inventory, WorkOrder)
   - Scheduling Context (Technician, WorkOrder scheduling)
   - Could evolve into microservices later
