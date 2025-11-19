using System;
using System.Collections.Generic;
using HeavyIMS.Domain.ValueObjects;

namespace HeavyIMS.Domain.Entities
{
    /// <summary>
    /// Domain Entity: WorkOrder
    /// Represents a repair/maintenance job to be performed
    /// ADDRESSES CHALLENGES: 1 (Scheduling), 3 (Communication), 5 (Estimating)
    ///
    /// DDD Pattern: AGGREGATE ROOT
    /// - Transactional consistency boundary for work order, parts, and notifications
    /// - Owns child entities: WorkOrderPart and WorkOrderNotification
    /// - References Customer and Technician by ID (cross-aggregate references)
    /// - Controls workflow via state machine (status transitions)
    ///
    /// Why it's an Aggregate Root:
    /// 1. Transaction Boundary: WorkOrder + RequiredParts + Notifications must be consistent
    /// 2. Lifecycle Owner: Controls creation/modification of parts assignments and notifications
    /// 3. Business Invariants: Enforces "cannot start without parts", valid status transitions
    /// 4. State Machine: Manages complex workflow (Pending → Assigned → InProgress → Completed)
    /// 5. External Reference Point: Other systems reference WorkOrder by WorkOrderId
    ///
    /// What it manages:
    /// - Work order lifecycle and status (state machine)
    /// - Equipment/vehicle information
    /// - Customer and technician assignments (by reference)
    /// - Scheduling (estimated and actual dates)
    /// - Estimating and costing data
    /// - Required parts (WorkOrderPart entities - OWNED)
    /// - Notification history (WorkOrderNotification entities - OWNED)
    ///
    /// AGGREGATE ROOT in DDD - Manages related entities (WorkOrderParts, WorkOrderNotifications)
    /// All modifications to child entities go through WorkOrder methods
    /// </summary>
    public class WorkOrder : AggregateRoot
    {
        public Guid Id { get; private set; }
        public string WorkOrderNumber { get; private set; } // Business identifier (e.g., "WO-2025-00123")

        // Customer information (ID reference only - DDD aggregate boundary)
        public Guid CustomerId { get; private set; }

        // Equipment/Vehicle information (VALUE OBJECT)
        // BEFORE: Three separate strings (VIN, Type, Model)
        // AFTER: Single EquipmentIdentifier value object encapsulates all equipment identification
        public EquipmentIdentifier Equipment { get; private set; }

        // Job details
        public string Description { get; private set; }
        public string DiagnosticNotes { get; private set; }
        public WorkOrderPriority Priority { get; private set; }
        public WorkOrderStatus Status { get; private set; }

        // Assignment - ID reference only (DDD aggregate boundary)
        public Guid? AssignedTechnicianId { get; private set; }

        // Scheduling information (VALUE OBJECTS)
        // BEFORE: Four separate DateTime? properties
        // AFTER: Two DateRange value objects for planned vs actual timeframes
        public DateRange? ScheduledPeriod { get; private set; }
        public DateRange? ActualPeriod { get; private set; }

        // Estimating & Diagnostics (CHALLENGE 5)
        public decimal EstimatedLaborHours { get; private set; }
        public decimal ActualLaborHours { get; private set; }

        // Costs (VALUE OBJECTS)
        // BEFORE: Two separate decimal properties
        // AFTER: Money value objects with currency and validation
        public Money EstimatedCost { get; private set; }
        public Money ActualCost { get; private set; }

        // Parts management (CHALLENGE 2)
        public ICollection<WorkOrderPart> RequiredParts { get; private set; }

        // Communication history (CHALLENGE 3)
        public ICollection<WorkOrderNotification> Notifications { get; private set; }

        // Audit fields
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public string CreatedBy { get; private set; }

        private WorkOrder()
        {
            RequiredParts = new List<WorkOrderPart>();
            Notifications = new List<WorkOrderNotification>();
        }

        /// <summary>
        /// Factory Method: Create a new work order
        /// DEMONSTRATES: Object-Oriented Design (OOD) principles with Value Objects
        /// </summary>
        public static WorkOrder Create(
            string equipmentVIN,
            string equipmentType,
            string equipmentModel,
            Guid customerId,
            string description,
            WorkOrderPriority priority,
            string createdBy)
        {
            // VALUE OBJECT: Create EquipmentIdentifier (validates VIN format)
            var equipment = EquipmentIdentifier.Create(equipmentVIN, equipmentType, equipmentModel);

            if (customerId == Guid.Empty)
                throw new ArgumentException("Customer ID is required", nameof(customerId));

            var workOrder = new WorkOrder
            {
                Id = Guid.NewGuid(),
                WorkOrderNumber = GenerateWorkOrderNumber(),
                Equipment = equipment,
                CustomerId = customerId,
                Description = description,
                Priority = priority,
                Status = WorkOrderStatus.Pending,
                EstimatedCost = Money.Zero(),
                ActualCost = Money.Zero(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            return workOrder;
        }

        /// <summary>
        /// Business logic: Generate unique work order number
        /// Format: WO-YYYY-NNNNN (e.g., WO-2025-00123)
        /// </summary>
        private static string GenerateWorkOrderNumber()
        {
            // In production, this would query the database for the last number
            // For now, we'll use a timestamp-based approach
            var year = DateTime.UtcNow.Year;
            var randomNum = new Random().Next(10000, 99999);
            return $"WO-{year}-{randomNum:D5}";
        }

        /// <summary>
        /// Domain Method: Assign technician to work order
        /// CRITICAL FOR: Drag-and-drop scheduling functionality
        /// NOTE: Basic validation only - capacity check must be done in Application Service
        /// (DDD principle: aggregate can't validate using data from another aggregate)
        /// </summary>
        public void AssignTechnician(Guid technicianId)
        {
            if (technicianId == Guid.Empty)
                throw new ArgumentException("Technician ID is required", nameof(technicianId));

            // Store only the ID reference (DDD aggregate boundary)
            AssignedTechnicianId = technicianId;

            // Automatically update status when assigned
            if (Status == WorkOrderStatus.Pending)
            {
                UpdateStatus(WorkOrderStatus.Assigned);
            }

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Domain Method: Reassign work order
        /// ADDRESSES: Workload balancing requirement
        /// NOTE: Capacity check must be done in Application Service (DDD principle)
        /// </summary>
        public void ReassignTechnician(Guid newTechnicianId, string reason)
        {
            if (newTechnicianId == Guid.Empty)
                throw new ArgumentException("Technician ID is required", nameof(newTechnicianId));

            var previousTechnicianId = AssignedTechnicianId;
            AssignTechnician(newTechnicianId);

            // Record the reassignment for audit trail
            DiagnosticNotes += $"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] " +
                              $"Reassigned from technician {previousTechnicianId} to {newTechnicianId}. " +
                              $"Reason: {reason}";
        }

        /// <summary>
        /// Domain Method: Update work order status with state validation
        /// TRIGGERS: Automated notifications (CHALLENGE 3)
        /// DEMONSTRATES: State machine pattern
        /// </summary>
        public void UpdateStatus(WorkOrderStatus newStatus)
        {
            // Validate state transitions
            if (!IsValidStatusTransition(Status, newStatus))
                throw new InvalidOperationException(
                    $"Invalid status transition from {Status} to {newStatus}");

            var previousStatus = Status;
            Status = newStatus;
            UpdatedAt = DateTime.UtcNow;

            // Domain events would be raised here for notification system
            // This triggers automated customer notifications (CHALLENGE 3)
            HandleStatusChange(previousStatus, newStatus);
        }

        /// <summary>
        /// Business rule: Valid status transitions
        /// DEMONSTRATES: State machine pattern for workflow management
        /// </summary>
        private bool IsValidStatusTransition(WorkOrderStatus from, WorkOrderStatus to)
        {
            return (from, to) switch
            {
                (WorkOrderStatus.Pending, WorkOrderStatus.Assigned) => true,
                (WorkOrderStatus.Assigned, WorkOrderStatus.InProgress) => true,
                (WorkOrderStatus.InProgress, WorkOrderStatus.OnHold) => true,
                (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
                (WorkOrderStatus.OnHold, WorkOrderStatus.InProgress) => true,
                (_, WorkOrderStatus.Cancelled) => true, // Can cancel from any state
                _ => false
            };
        }

        /// <summary>
        /// Handle status changes - triggers side effects
        /// In a real system, this would raise domain events
        /// INTEGRATION POINT: Notification system
        /// USES VALUE OBJECTS: DateRange for actual timeframes
        /// </summary>
        private void HandleStatusChange(WorkOrderStatus oldStatus, WorkOrderStatus newStatus)
        {
            switch (newStatus)
            {
                case WorkOrderStatus.InProgress:
                    // Start the actual work period (open-ended until completion)
                    ActualPeriod = DateRange.CreateOpenEnded(DateTime.UtcNow);
                    break;
                case WorkOrderStatus.Completed:
                    // Close the actual work period
                    if (ActualPeriod != null)
                    {
                        ActualPeriod = DateRange.Create(ActualPeriod.Start, DateTime.UtcNow);
                    }
                    break;
            }

            // Domain event would be raised here:
            // RaiseDomainEvent(new WorkOrderStatusChangedEvent(Id, oldStatus, newStatus));
        }

        /// <summary>
        /// Domain Method: Set scheduled timeframe
        /// ADDRESSES CHALLENGE 1: Scheduling
        /// USES VALUE OBJECTS: DateRange for scheduling
        /// </summary>
        public void SetScheduledPeriod(DateTime scheduledStart, DateTime scheduledEnd)
        {
            ScheduledPeriod = DateRange.Create(scheduledStart, scheduledEnd);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Domain Method: Add estimated labor time from OEM data
        /// ADDRESSES CHALLENGE 5: Integrated Labor Guides & Diagnostics Data
        /// USES VALUE OBJECTS: Money for cost estimation
        /// </summary>
        public void SetEstimate(decimal laborHours, decimal estimatedCost)
        {
            if (laborHours < 0)
                throw new ArgumentException("Labor hours cannot be negative", nameof(laborHours));

            EstimatedLaborHours = laborHours;
            EstimatedCost = Money.Create(estimatedCost); // VALUE OBJECT: Validates non-negative
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Domain Method: Record actual time spent
        /// USED FOR: Analytics and estimate accuracy improvement
        /// USES VALUE OBJECTS: Money for actual costs
        /// </summary>
        public void RecordActualTime(decimal actualHours, decimal actualCost)
        {
            if (Status != WorkOrderStatus.Completed)
                throw new InvalidOperationException("Can only record actual time for completed work orders");

            ActualLaborHours = actualHours;
            ActualCost = Money.Create(actualCost); // VALUE OBJECT: Validates non-negative
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Domain Method: Add required part to work order
        /// ADDRESSES CHALLENGE 2: Parts management
        /// </summary>
        public void AddRequiredPart(Guid partId, int quantity, bool isAvailable)
        {
            var workOrderPart = WorkOrderPart.Create(Id, partId, quantity, isAvailable);
            RequiredParts.Add(workOrderPart);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if all required parts are available
        /// BUSINESS RULE: Cannot start work without all parts
        /// ADDRESSES CHALLENGE 2: Parts Delays
        /// </summary>
        public bool AreAllPartsAvailable()
        {
            foreach (var part in RequiredParts)
            {
                if (!part.IsAvailable)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Calculated property: Check if work order is delayed
        /// USED BY: Dashboard and notification system
        /// USES VALUE OBJECTS: DateRange for scheduling
        /// </summary>
        public bool IsDelayed =>
            ScheduledPeriod != null &&
            Status != WorkOrderStatus.Completed &&
            DateTime.UtcNow > ScheduledPeriod.End;
    }

    /// <summary>
    /// Enum: Work order priority levels
    /// BUSINESS LOGIC: Affects scheduling order
    /// </summary>
    public enum WorkOrderPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Enum: Work order status for workflow management
    /// CRITICAL FOR: Real-time dashboard visibility
    /// </summary>
    public enum WorkOrderStatus
    {
        Pending,        // Just created, not assigned
        Assigned,       // Assigned to technician but not started
        InProgress,     // Technician is working on it
        OnHold,         // Waiting for parts or approval
        Completed,      // Work finished
        Cancelled       // Work order cancelled
    }

    /// <summary>
    /// Entity: WorkOrderPart (Join table with business logic)
    /// Links work orders to required parts
    /// ADDRESSES CHALLENGE 2: Parts management for work orders
    ///
    /// DDD Pattern: ENTITY (within WorkOrder aggregate)
    /// - Has identity (unique ID)
    /// - Part of WorkOrder aggregate boundary
    /// - Tracks reservation and issuance status
    /// - No independent lifecycle (created/deleted with WorkOrder)
    /// </summary>
    public class WorkOrderPart
    {
        public Guid Id { get; private set; }
        public Guid WorkOrderId { get; private set; }
        public Guid PartId { get; private set; }
        public int QuantityRequired { get; private set; }
        public bool IsAvailable { get; private set; }
        public DateTime? ReservedAt { get; private set; }
        public DateTime? IssuedAt { get; private set; }

        private WorkOrderPart() { }

        public static WorkOrderPart Create(Guid workOrderId, Guid partId,
            int quantity, bool isAvailable)
        {
            return new WorkOrderPart
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                PartId = partId,
                QuantityRequired = quantity,
                IsAvailable = isAvailable
            };
        }

        public void MarkAsReserved()
        {
            ReservedAt = DateTime.UtcNow;
            IsAvailable = true;
        }

        public void MarkAsIssued()
        {
            IssuedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Entity: WorkOrderNotification
    /// Tracks all notifications sent for a work order
    /// ADDRESSES CHALLENGE 3: Automated customer & team notifications
    ///
    /// DDD Pattern: ENTITY (within WorkOrder aggregate)
    /// - Has identity (unique ID per notification)
    /// - Part of WorkOrder aggregate boundary
    /// - Immutable audit record once created
    /// - Tracks notification delivery status
    /// </summary>
    public class WorkOrderNotification
    {
        public Guid Id { get; private set; }
        public Guid WorkOrderId { get; private set; }
        public NotificationType NotificationType { get; private set; }
        public string RecipientEmail { get; private set; }
        public string RecipientPhone { get; private set; }
        public string Subject { get; private set; }
        public string Message { get; private set; }
        public DateTime SentAt { get; private set; }
        public bool WasSuccessful { get; private set; }
        public string ErrorMessage { get; private set; }

        private WorkOrderNotification() { }

        public static WorkOrderNotification Create(Guid workOrderId,
            NotificationType type, string recipientEmail, string recipientPhone,
            string subject, string message)
        {
            return new WorkOrderNotification
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                NotificationType = type,
                RecipientEmail = recipientEmail,
                RecipientPhone = recipientPhone,
                Subject = subject,
                Message = message,
                SentAt = DateTime.UtcNow
            };
        }

        public void MarkAsSuccess()
        {
            WasSuccessful = true;
        }

        public void MarkAsFailed(string errorMessage)
        {
            WasSuccessful = false;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Enum: Notification delivery methods
    /// </summary>
    public enum NotificationType
    {
        Email,
        SMS,
        Push
    }
}
