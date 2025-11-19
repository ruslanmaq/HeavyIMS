using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HeavyIMS.Infrastructure.Repositories
{
    /// <summary>
    /// Unit of Work Pattern Implementation
    ///
    /// WHAT IS UNIT OF WORK?
    /// - Maintains a list of objects affected by a business transaction
    /// - Coordinates writing changes to database
    /// - Ensures all changes succeed or fail together (ACID transaction)
    ///
    /// WHY USE IT?
    /// 1. Transaction Management: Multiple operations in one transaction
    /// 2. Consistency: All changes committed together
    /// 3. Performance: Single database round-trip for SaveChanges
    ///
    /// EXAMPLE SCENARIO:
    /// - Assign work order to technician
    /// - Reserve parts for work order
    /// - Send notification to customer
    /// ALL must succeed or ALL must rollback
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly HeavyIMSDbContext _context;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private IDbContextTransaction _transaction;

        // Lazy initialization of repositories
        private ITechnicianRepository _technicians;
        private IWorkOrderRepository _workOrders;
        private IRepository<Customer> _customers;

        // Separate aggregates (DDD best practices)
        private IPartRepository _parts;
        private IInventoryRepository _inventory;

        /// <summary>
        /// Constructor - Dependency Injection
        /// DEMONSTRATES: Constructor injection of DbContext and Event Dispatcher
        /// </summary>
        public UnitOfWork(
            HeavyIMSDbContext context,
            IDomainEventDispatcher eventDispatcher)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        }

        /// <summary>
        /// Lazy property for Technician repository
        /// PATTERN: Lazy Initialization
        /// WHY? Only create repository when first accessed (performance)
        /// </summary>
        public ITechnicianRepository Technicians
        {
            get
            {
                if (_technicians == null)
                {
                    _technicians = new TechnicianRepository(_context);
                }
                return _technicians;
            }
        }

        /// <summary>
        /// Lazy property for WorkOrder repository
        /// </summary>
        public IWorkOrderRepository WorkOrders
        {
            get
            {
                if (_workOrders == null)
                {
                    _workOrders = new WorkOrderRepository(_context);
                }
                return _workOrders;
            }
        }

        /// <summary>
        /// Lazy property for Customer repository
        /// Uses generic repository (no specialized methods needed)
        /// </summary>
        public IRepository<Customer> Customers
        {
            get
            {
                if (_customers == null)
                {
                    _customers = new Repository<Customer>(_context);
                }
                return _customers;
            }
        }

        /// <summary>
        /// Lazy property for Part repository (Catalog Aggregate)
        /// DEMONSTRATES: Separate aggregate for catalog management
        /// </summary>
        public IPartRepository Parts
        {
            get
            {
                if (_parts == null)
                {
                    _parts = new PartRepository(_context);
                }
                return _parts;
            }
        }

        /// <summary>
        /// Lazy property for Inventory repository (Operational Aggregate)
        /// DEMONSTRATES: Separate aggregate for warehouse operations
        /// </summary>
        public IInventoryRepository Inventory
        {
            get
            {
                if (_inventory == null)
                {
                    _inventory = new InventoryRepository(_context);
                }
                return _inventory;
            }
        }

        /// <summary>
        /// Save all changes to database and dispatch domain events
        /// CRITICAL METHOD: Commits all changes across all repositories
        /// DEMONSTRATES: Unit of Work pattern core functionality + Domain Events
        ///
        /// DOMAIN EVENTS FLOW:
        /// 1. Collect events from all modified aggregate roots
        /// 2. Save changes to database (transaction)
        /// 3. If save succeeds, dispatch events to handlers
        /// 4. Clear events from aggregates
        ///
        /// WHY THIS ORDER?
        /// - Events only fire if database changes persist successfully
        /// - Ensures consistency between database state and event notifications
        /// - If database fails, no events are published (no false notifications)
        /// </summary>
        /// <returns>Number of state entries written to database</returns>
        public async Task<int> SaveChangesAsync()
        {
            try
            {
                // STEP 1: Collect domain events from all modified aggregates
                var domainEvents = new List<Domain.Events.DomainEvent>();

                foreach (var entry in _context.ChangeTracker.Entries<AggregateRoot>())
                {
                    if (entry.Entity.HasDomainEvents)
                    {
                        domainEvents.AddRange(entry.Entity.DomainEvents);
                    }
                }

                // STEP 2: Save changes to database (transaction)
                var result = await _context.SaveChangesAsync();

                // STEP 3: If save succeeds, dispatch events
                if (domainEvents.Any())
                {
                    await _eventDispatcher.DispatchAsync(domainEvents);

                    // STEP 4: Clear events from aggregates
                    foreach (var entry in _context.ChangeTracker.Entries<AggregateRoot>())
                    {
                        entry.Entity.ClearDomainEvents();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                // Log exception (use ILogger in production)
                // Re-throw to let caller handle
                throw new Exception("Error saving changes to database", ex);
            }
        }

        /// <summary>
        /// Begin explicit database transaction
        /// USED FOR: Complex multi-step operations requiring rollback capability
        ///
        /// WHEN TO USE?
        /// - Operations across multiple aggregates
        /// - Operations that must be atomic but span multiple SaveChanges calls
        /// - Operations requiring isolation from other transactions
        /// </summary>
        public async Task BeginTransactionAsync()
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }

            // Begin database transaction with default isolation level
            _transaction = await _context.Database.BeginTransactionAsync();

            // ALTERNATIVE: Specify isolation level
            // _transaction = await _context.Database.BeginTransactionAsync(
            //     System.Data.IsolationLevel.ReadCommitted);
        }

        /// <summary>
        /// Commit the current transaction
        /// DEMONSTRATES: Transaction control
        /// </summary>
        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction in progress");
            }

            try
            {
                // Commit transaction to database
                await _transaction.CommitAsync();
            }
            catch
            {
                // Rollback on error
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                // Dispose transaction
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// Rollback the current transaction
        /// CRITICAL FOR: Ensuring data consistency on errors
        /// </summary>
        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction in progress");
            }

            try
            {
                // Rollback all changes
                await _transaction.RollbackAsync();
            }
            finally
            {
                // Dispose transaction
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// Dispose pattern implementation
        /// DEMONSTRATES: IDisposable best practices
        /// CRITICAL FOR: Releasing database connections
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose transaction if exists
                _transaction?.Dispose();

                // Dispose DbContext (releases connection)
                _context?.Dispose();
            }
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. WHY UNIT OF WORK?
 *    - Ensures all repository operations use same DbContext instance
 *    - Provides transaction boundary for multiple operations
 *    - Centralizes SaveChanges() call
 *
 * 2. EXAMPLE USAGE:
 *    using (var unitOfWork = new UnitOfWork(context))
 *    {
 *        // Operation 1: Create work order
 *        var workOrder = WorkOrder.Create(...);
 *        await unitOfWork.WorkOrders.AddAsync(workOrder);
 *
 *        // Operation 2: Reserve parts from specific warehouse
 *        var inventory = await unitOfWork.Inventory.GetByPartAndWarehouseAsync(partId, "Main");
 *        inventory.ReserveParts(5, workOrder.Id, "user@example.com");
 *
 *        // Commit both operations together
 *        await unitOfWork.SaveChangesAsync();
 *    }
 *
 * 3. TRANSACTION EXAMPLE:
 *    await unitOfWork.BeginTransactionAsync();
 *    try
 *    {
 *        // Multiple save operations
 *        await unitOfWork.SaveChangesAsync();
 *        // More operations...
 *        await unitOfWork.SaveChangesAsync();
 *
 *        await unitOfWork.CommitTransactionAsync();
 *    }
 *    catch
 *    {
 *        await unitOfWork.RollbackTransactionAsync();
 *        throw;
 *    }
 *
 * 4. BENEFITS:
 *    - Atomicity: All or nothing
 *    - Consistency: Related changes stay together
 *    - Testability: Easy to mock IUnitOfWork
 *    - Flexibility: Can swap implementations
 */
