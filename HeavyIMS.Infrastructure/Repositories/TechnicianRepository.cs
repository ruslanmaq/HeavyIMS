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
    /// Specialized Repository for Technicians
    /// DEMONSTRATES:
    /// - Inheritance from generic repository
    /// - Domain-specific queries using LINQ
    /// - Complex filtering logic
    ///
    /// DDD COMPLIANCE: No navigation properties between aggregates
    /// - Technician aggregate has no WorkOrders navigation property
    /// - To get workload, use WorkOrderRepository.CountActiveWorkOrdersByTechnicianAsync()
    /// - Application service coordinates cross-aggregate queries
    ///
    /// ADDRESSES CHALLENGE 1: Workload balancing and scheduling
    /// </summary>
    public class TechnicianRepository : Repository<Technician>, ITechnicianRepository
    {
        public TechnicianRepository(HeavyIMSDbContext context) : base(context)
        {
        }

        /// <summary>
        /// Get available technicians (active and not on leave)
        /// CRITICAL FOR: Drag-and-drop scheduling system
        /// NOTE: Capacity checking requires cross-aggregate query via WorkOrderRepository
        /// </summary>
        public async Task<IEnumerable<Technician>> GetAvailableTechniciansAsync()
        {
            // DDD: Return only Technician aggregate, no navigation properties
            return await _dbSet
                .Where(t => t.IsActive)              // Only active technicians
                .Where(t => t.Status != TechnicianStatus.OnLeave)
                .Where(t => t.Status != TechnicianStatus.Inactive)
                .AsNoTracking()                       // Read-only for performance
                .ToListAsync();
        }

        /// <summary>
        /// Get technicians with available capacity
        /// BUSINESS LOGIC: Filters technicians who are active and not on leave
        /// NOTE: Actual capacity check must be done in application service using WorkOrderRepository
        /// </summary>
        public async Task<IEnumerable<Technician>> GetTechniciansWithCapacityAsync()
        {
            // DDD: Repository returns Technician aggregate only
            // Application service must:
            // 1. Get these technicians
            // 2. For each, query WorkOrderRepository.CountActiveWorkOrdersByTechnicianAsync()
            // 3. Call technician.CanAcceptNewJob(activeJobCount)

            return await _dbSet
                .Where(t => t.IsActive)
                .Where(t => t.Status != TechnicianStatus.OnLeave)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get technicians by skill level
        /// DEMONSTRATES: Simple filtering with enum
        /// USED FOR: Assigning jobs requiring specific expertise
        /// </summary>
        public async Task<IEnumerable<Technician>> GetBySkillLevelAsync(
            TechnicianSkillLevel skillLevel)
        {
            return await _dbSet
                .Where(t => t.IsActive)
                .Where(t => t.SkillLevel == skillLevel)
                .AsNoTracking()
                .ToListAsync();

            // LINQ TO SQL TRANSLATION:
            // This query translates to SQL like:
            // SELECT * FROM Technicians
            // WHERE IsActive = 1 AND SkillLevel = @skillLevel
        }
    }
}
