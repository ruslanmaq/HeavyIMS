using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace HeavyIMS.Domain.Interfaces
{
    /// <summary>
    /// Generic Repository Interface
    /// DEMONSTRATES: Repository Pattern (DDD principle)
    /// BENEFITS:
    /// - Abstraction over data access
    /// - Easy to mock for unit testing
    /// - Separates domain logic from database concerns
    ///
    /// WHY GENERIC? Code reuse across all entities
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IRepository<T> where T : class
    {
        // READ operations
        Task<T> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        // WRITE operations
        Task<T> AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);

        // COUNT/EXISTS
        Task<int> CountAsync(Expression<Func<T, bool>> predicate = null);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    }

    /// <summary>
    /// Specialized Repository for Technicians
    /// DEMONSTRATES: Interface Segregation Principle (SOLID)
    /// WHY NEEDED? Technician-specific queries for scheduling
    ///
    /// DDD COMPLIANCE: No navigation properties between aggregates
    /// - To get a technician's work orders, query WorkOrderRepository with TechnicianId filter
    /// - Application service coordinates cross-aggregate queries
    /// </summary>
    public interface ITechnicianRepository : IRepository<Entities.Technician>
    {
        /// <summary>
        /// Find technicians available for assignment
        /// CRITICAL FOR: Drag-and-drop scheduling (CHALLENGE 1)
        /// </summary>
        Task<IEnumerable<Entities.Technician>> GetAvailableTechniciansAsync();

        /// <summary>
        /// Get technicians with current workload under capacity
        /// USED BY: Auto-assignment algorithms
        /// NOTE: Capacity check requires cross-aggregate query via WorkOrderRepository
        /// </summary>
        Task<IEnumerable<Entities.Technician>> GetTechniciansWithCapacityAsync();

        /// <summary>
        /// Get technicians by skill level
        /// BUSINESS LOGIC: Match technician expertise to job complexity
        /// </summary>
        Task<IEnumerable<Entities.Technician>> GetBySkillLevelAsync(
            Entities.TechnicianSkillLevel skillLevel);
    }

    /// <summary>
    /// Specialized Repository for Work Orders
    /// ADDRESSES: Scheduling and workload management queries
    /// </summary>
    public interface IWorkOrderRepository : IRepository<Entities.WorkOrder>
    {
        /// <summary>
        /// Get all pending work orders (not assigned)
        /// USED BY: Scheduling dashboard to show unassigned work
        /// </summary>
        Task<IEnumerable<Entities.WorkOrder>> GetPendingWorkOrdersAsync();

        /// <summary>
        /// Get work orders assigned to a specific technician
        /// CRITICAL FOR: Technician workload calculation
        /// </summary>
        Task<IEnumerable<Entities.WorkOrder>> GetByTechnicianIdAsync(Guid technicianId);

        /// <summary>
        /// Count active work orders for a technician
        /// CRITICAL FOR: Capacity checking before assignment (DDD: cross-aggregate query)
        /// USED BY: Application service to validate Technician.CanAcceptNewJob()
        /// </summary>
        Task<int> CountActiveWorkOrdersByTechnicianAsync(Guid technicianId);

        /// <summary>
        /// Get delayed work orders
        /// TRIGGERS: Automated delay notifications (CHALLENGE 3)
        /// </summary>
        Task<IEnumerable<Entities.WorkOrder>> GetDelayedWorkOrdersAsync();

        /// <summary>
        /// Get work orders waiting for parts
        /// ADDRESSES CHALLENGE 2: Parts delay visibility
        /// </summary>
        Task<IEnumerable<Entities.WorkOrder>> GetWorkOrdersWaitingForPartsAsync();

        /// <summary>
        /// Get work order with all related data (includes)
        /// DEMONSTRATES: Eager loading to prevent N+1 queries
        /// </summary>
        Task<Entities.WorkOrder> GetWorkOrderWithDetailsAsync(Guid id);

        /// <summary>
        /// Search work orders by equipment VIN or work order number
        /// USED BY: Search functionality in UI
        /// </summary>
        Task<IEnumerable<Entities.WorkOrder>> SearchAsync(string searchTerm);
    }

    /// <summary>
    /// Specialized Repository for Parts (Catalog Aggregate)
    /// MANAGES: Product catalog, pricing, supplier relationships
    /// SEPARATION: Catalog data separate from inventory operations
    /// </summary>
    public interface IPartRepository : IRepository<Entities.Part>
    {
        /// <summary>
        /// Search parts by part number or name
        /// USED BY: Part lookup when creating work orders or inventory locations
        /// </summary>
        Task<IEnumerable<Entities.Part>> SearchPartsAsync(string searchTerm);

        /// <summary>
        /// Get part by part number (unique identifier)
        /// USED BY: Part lookups, integrations with external systems
        /// </summary>
        Task<Entities.Part> GetByPartNumberAsync(string partNumber);

        /// <summary>
        /// Get parts by category
        /// USED BY: Filtered part searches, catalog browsing
        /// </summary>
        Task<IEnumerable<Entities.Part>> GetPartsByCategoryAsync(string category);

        /// <summary>
        /// Get active (non-discontinued) parts
        /// USED BY: Part selection in work orders
        /// </summary>
        Task<IEnumerable<Entities.Part>> GetActivePartsAsync();

        /// <summary>
        /// Get discontinued parts
        /// USED BY: Inventory cleanup, replacement part suggestions
        /// </summary>
        Task<IEnumerable<Entities.Part>> GetDiscontinuedPartsAsync();

        /// <summary>
        /// Get parts by supplier
        /// USED BY: Supplier management, bulk ordering
        /// </summary>
        Task<IEnumerable<Entities.Part>> GetPartsBySupplierAsync(Guid supplierId);
    }

    /// <summary>
    /// Specialized Repository for Inventory (Operational Aggregate)
    /// ADDRESSES CHALLENGE 2: Real-time inventory tracking per warehouse location
    /// SEPARATION: Operational inventory separate from catalog data
    /// </summary>
    public interface IInventoryRepository : IRepository<Entities.Inventory>
    {
        /// <summary>
        /// Get all inventory locations for a specific part
        /// USED BY: Total quantity calculations, multi-warehouse visibility
        /// </summary>
        Task<IEnumerable<Entities.Inventory>> GetByPartIdAsync(Guid partId);

        /// <summary>
        /// Get inventory for a specific part at a specific warehouse
        /// USED BY: Warehouse-specific operations, reservations
        /// </summary>
        Task<Entities.Inventory> GetByPartAndWarehouseAsync(Guid partId, string warehouse);

        /// <summary>
        /// Get all inventory at a specific warehouse
        /// USED BY: Warehouse dashboards, cycle counting
        /// </summary>
        Task<IEnumerable<Entities.Inventory>> GetByWarehouseAsync(string warehouse);

        /// <summary>
        /// Get inventory locations below minimum stock level
        /// CRITICAL FOR: Automated low-stock alerts
        /// TRIGGERS: Notification to purchasing department
        /// </summary>
        Task<IEnumerable<Entities.Inventory>> GetLowStockInventoryAsync();

        /// <summary>
        /// Get inventory locations that are out of stock
        /// CRITICAL FOR: Preventing work order delays
        /// </summary>
        Task<IEnumerable<Entities.Inventory>> GetOutOfStockInventoryAsync();

        /// <summary>
        /// Get inventory with transaction history
        /// DEMONSTRATES: Including related collections
        /// USED FOR: Audit trail and troubleshooting
        /// </summary>
        Task<Entities.Inventory> GetInventoryWithTransactionsAsync(Guid inventoryId);

        /// <summary>
        /// Check if sufficient quantity is available at specific warehouse
        /// BUSINESS LOGIC: Validates part availability before work order creation
        /// </summary>
        Task<bool> IsQuantityAvailableAsync(Guid partId, string warehouse, int requiredQuantity);

        /// <summary>
        /// Get total quantity across all warehouses for a part
        /// USED BY: Total inventory reports, availability checks
        /// </summary>
        Task<int> GetTotalQuantityOnHandAsync(Guid partId);

        /// <summary>
        /// Get total available quantity (on hand - reserved) for a part across all warehouses
        /// BUSINESS LOGIC: Global availability check
        /// </summary>
        Task<int> GetTotalAvailableQuantityAsync(Guid partId);
    }

    /// <summary>
    /// Unit of Work Pattern
    /// CRITICAL FOR: Transaction management
    /// DEMONSTRATES: Database transaction boundaries
    ///
    /// WHY NEEDED?
    /// - Multiple repository operations in single transaction
    /// - Ensures data consistency
    /// - Example: Assigning work order + reserving parts = single transaction
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // Repository access
        ITechnicianRepository Technicians { get; }
        IWorkOrderRepository WorkOrders { get; }
        IRepository<Entities.Customer> Customers { get; }

        // Separate aggregates (DDD best practices)
        IPartRepository Parts { get; }
        IInventoryRepository Inventory { get; }

        /// <summary>
        /// Commit all changes to database
        /// DEMONSTRATES: Transaction boundary
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Begin explicit database transaction
        /// USED FOR: Complex multi-step operations
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commit transaction
        /// </summary>
        Task CommitTransactionAsync();

        /// <summary>
        /// Rollback transaction on error
        /// ENSURES: Data consistency on failures
        /// </summary>
        Task RollbackTransactionAsync();
    }
}
