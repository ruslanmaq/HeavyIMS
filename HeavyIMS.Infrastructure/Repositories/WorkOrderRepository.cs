using Microsoft.EntityFrameworkCore;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HeavyIMS.Infrastructure.Repositories
{
    /// <summary>
    /// Specialized Repository for Work Orders
    /// DEMONSTRATES: Complex LINQ queries for business operations
    /// ADDRESSES: Challenges 1, 2, 3, 5 (Scheduling, Parts, Communication, Estimating)
    ///
    /// DDD COMPLIANCE: No navigation properties between aggregates
    /// - WorkOrder references Customer and Technician by ID only
    /// - Related aggregates must be loaded separately via their repositories
    /// - Application service coordinates cross-aggregate queries
    /// </summary>
    public class WorkOrderRepository : Repository<WorkOrder>, IWorkOrderRepository
    {
        public WorkOrderRepository(HeavyIMSDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<WorkOrder>> GetPendingWorkOrdersAsync()
        {
            // DDD: No navigation properties - load WorkOrder aggregate only
            return await _dbSet
                .Where(wo => wo.Status == WorkOrderStatus.Pending)
                .OrderByDescending(wo => wo.Priority)  // Critical jobs first
                .ThenBy(wo => wo.CreatedAt)            // Then by creation time
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<WorkOrder>> GetByTechnicianIdAsync(Guid technicianId)
        {
            // DDD: Load WorkOrder aggregate with owned entities (RequiredParts)
            // Customer and Technician must be loaded separately via their repositories
            return await _dbSet
                .Include(wo => wo.RequiredParts)  // Owned entity - OK to include
                .Where(wo => wo.AssignedTechnicianId == technicianId)
                .Where(wo => wo.Status != WorkOrderStatus.Completed
                          && wo.Status != WorkOrderStatus.Cancelled)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Count active work orders for a technician (DDD cross-aggregate query)
        /// CRITICAL FOR: Technician capacity validation
        /// </summary>
        public async Task<int> CountActiveWorkOrdersByTechnicianAsync(Guid technicianId)
        {
            return await _dbSet
                .Where(wo => wo.AssignedTechnicianId == technicianId)
                .Where(wo => wo.Status != WorkOrderStatus.Completed
                          && wo.Status != WorkOrderStatus.Cancelled)
                .CountAsync();
        }

        public async Task<IEnumerable<WorkOrder>> GetDelayedWorkOrdersAsync()
        {
            // DDD: No navigation properties - return WorkOrder aggregate only
            // VALUE OBJECTS: ScheduledPeriod replaces individual date properties
            var now = DateTime.UtcNow;
            return await _dbSet
                .Where(wo => wo.ScheduledPeriod != null)
                .Where(wo => wo.ScheduledPeriod.End < now)
                .Where(wo => wo.Status != WorkOrderStatus.Completed)
                .Where(wo => wo.Status != WorkOrderStatus.Cancelled)
                .OrderBy(wo => wo.ScheduledPeriod.End)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<WorkOrder>> GetWorkOrdersWaitingForPartsAsync()
        {
            // RequiredParts is an owned entity within WorkOrder aggregate - OK to include
            return await _dbSet
                .Include(wo => wo.RequiredParts)
                .Where(wo => wo.Status == WorkOrderStatus.OnHold)
                .Where(wo => wo.RequiredParts.Any(p => !p.IsAvailable))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<WorkOrder> GetWorkOrderWithDetailsAsync(Guid id)
        {
            // DDD: Include only owned entities (RequiredParts, Notifications)
            // Customer and Technician must be loaded separately by application service
            return await _dbSet
                .Include(wo => wo.RequiredParts)      // Owned entity - within aggregate
                .Include(wo => wo.Notifications)      // Owned entity - within aggregate
                .AsNoTracking()
                .FirstOrDefaultAsync(wo => wo.Id == id);
        }

        public async Task<IEnumerable<WorkOrder>> SearchAsync(string searchTerm)
        {
            // DDD: Can only search within WorkOrder aggregate properties
            // To search by customer name, application service must:
            // 1. Query Customers by name to get CustomerIds
            // 2. Query WorkOrders by those CustomerIds
            // VALUE OBJECTS: Equipment.VIN instead of EquipmentVIN
            return await _dbSet
                .Where(wo => wo.WorkOrderNumber.Contains(searchTerm)
                          || wo.Equipment.VIN.Contains(searchTerm))
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
