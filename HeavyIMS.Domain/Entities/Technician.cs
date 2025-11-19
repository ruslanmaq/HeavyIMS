using System;
using System.Collections.Generic;

namespace HeavyIMS.Domain.Entities
{
    /// <summary>
    /// Domain Entity: Technician
    /// Represents a technician who performs repair/maintenance work
    /// ADDRESSES CHALLENGE 1: Technician Shortage & Workload Imbalance
    ///
    /// DDD Pattern: AGGREGATE ROOT
    /// - Independent lifecycle (managed separately from work assignments)
    /// - Consistency boundary for technician data, skill level, and capacity
    /// - Referenced by WorkOrder aggregate via TechnicianId
    /// - Resource management aggregate (scheduling system)
    ///
    /// Why it's an Aggregate Root:
    /// 1. Independent Identity: Technicians exist independently of work orders
    /// 2. Workload Business Logic: Contains capacity rules and availability checks
    /// 3. Resource Constraints: Enforces max concurrent jobs based on skill level
    /// 4. Status Management: Controls availability for scheduling system
    ///
    /// DDD PRINCIPLE: Rich Domain Model
    /// - Encapsulates business logic within the entity (CanAcceptNewJob, GetWorkloadPercentage)
    /// - Contains validation rules (max capacity based on skill level)
    /// - Maintains invariants (data consistency rules)
    ///
    /// What it manages:
    /// - Technician personal data and contact information
    /// - Skill level (Junior → Expert) and derived capacity
    /// - Availability status (Available, Busy, OnLeave, etc.)
    /// - Active/inactive status for employment tracking
    ///
    /// Cross-Aggregate References:
    /// - WorkOrders reference Technician by ID only (no navigation property - DDD principle)
    /// - To get technician's work orders, query WorkOrder aggregate with TechnicianId filter
    /// - Assignment happens through WorkOrder.AssignTechnician() method
    /// </summary>
    public class Technician : AggregateRoot
    {
        // Primary Key - Entity Framework convention
        public Guid Id { get; private set; }

        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string PhoneNumber { get; private set; }

        /// <summary>
        /// Skill level affects job assignment capability
        /// BUSINESS RULE: Higher skilled technicians can handle complex jobs
        /// </summary>
        public TechnicianSkillLevel SkillLevel { get; private set; }

        /// <summary>
        /// Current workload status for scheduling optimization
        /// ADDRESSES: Real-time visibility requirement
        /// </summary>
        public TechnicianStatus Status { get; private set; }

        /// <summary>
        /// Maximum concurrent jobs this technician can handle
        /// BUSINESS RULE: Prevents overbooking
        /// </summary>
        public int MaxConcurrentJobs { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public bool IsActive { get; private set; }

        // Private constructor for EF Core
        private Technician()
        {
        }

        /// <summary>
        /// Factory Method Pattern - Controlled object creation
        /// Ensures all required fields are provided and valid
        /// </summary>
        public static Technician Create(
            string firstName,
            string lastName,
            string email,
            string phoneNumber,
            TechnicianSkillLevel skillLevel)
        {
            // VALIDATION: Business rule enforcement
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name is required", nameof(firstName));

            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Last name is required", nameof(lastName));

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                throw new ArgumentException("Valid email is required", nameof(email));

            var technician = new Technician
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = phoneNumber,
                SkillLevel = skillLevel,
                Status = TechnicianStatus.Available,
                MaxConcurrentJobs = skillLevel switch
                {
                    TechnicianSkillLevel.Junior => 2,
                    TechnicianSkillLevel.Intermediate => 3,
                    TechnicianSkillLevel.Senior => 4,
                    TechnicianSkillLevel.Expert => 5,
                    _ => 2
                },
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            return technician;
        }

        /// <summary>
        /// Domain method - Business logic for checking workload capacity
        /// CRITICAL FOR: Preventing overbooking in scheduling system
        /// NOTE: Receives active job count from application service (DDD principle - no navigation property)
        /// </summary>
        public bool CanAcceptNewJob(int currentActiveJobCount)
        {
            if (!IsActive || Status == TechnicianStatus.OnLeave)
                return false;

            return currentActiveJobCount < MaxConcurrentJobs;
        }

        /// <summary>
        /// Domain method - Calculate current workload percentage
        /// USED BY: Digital dashboard for real-time visibility
        /// NOTE: Receives active job count from application service (DDD principle - no navigation property)
        /// </summary>
        public decimal GetWorkloadPercentage(int currentActiveJobCount)
        {
            if (!IsActive) return 0;

            return (decimal)currentActiveJobCount / MaxConcurrentJobs * 100;
        }

        /// <summary>
        /// Update technician status - Encapsulated state change
        /// </summary>
        public void UpdateStatus(TechnicianStatus newStatus)
        {
            Status = newStatus;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            Status = TechnicianStatus.Inactive;
            UpdatedAt = DateTime.UtcNow;
        }

        // Full name computed property for display purposes
        public string FullName => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// Enum: Technician skill levels
    /// BUSINESS LOGIC: Determines job assignment capability and capacity
    /// </summary>
    public enum TechnicianSkillLevel
    {
        Junior = 1,
        Intermediate = 2,
        Senior = 3,
        Expert = 4
    }

    /// <summary>
    /// Enum: Technician availability status
    /// CRITICAL FOR: Real-time scheduling dashboard
    /// </summary>
    public enum TechnicianStatus
    {
        Available,      // Ready to accept new jobs
        Busy,           // At max capacity
        OnJob,          // Currently working on a job
        OnLeave,        // Unavailable (vacation, sick, etc.)
        Inactive        // No longer with company
    }
}
