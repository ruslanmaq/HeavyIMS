using HeavyIMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HeavyIMS.Application.DTOs
{
    /// <summary>
    /// Data Transfer Objects (DTOs)
    /// PURPOSE:
    /// - Define API contracts (what data is sent/received)
    /// - Separate from domain models (domain can change without breaking API)
    /// - Add validation attributes
    /// - Control what data is exposed
    ///
    /// WHY NOT USE DOMAIN ENTITIES DIRECTLY?
    /// - Domain models have business logic (not needed in API)
    /// - Navigation properties cause circular references in JSON
    /// - Security: Don't expose sensitive internal data
    /// - Versioning: Can have multiple DTO versions for same entity
    /// </summary>

    /// <summary>
    /// DTO for creating a new work order
    /// DEMONSTRATES: Input validation attributes
    /// </summary>
    public class CreateWorkOrderDto
    {
        [Required(ErrorMessage = "Equipment VIN is required")]
        [StringLength(17, MinimumLength = 17, ErrorMessage = "VIN must be 17 characters")]
        public string EquipmentVIN { get; set; }

        [Required]
        [StringLength(100)]
        public string EquipmentType { get; set; }

        [StringLength(100)]
        public string EquipmentModel { get; set; }

        [Required(ErrorMessage = "Customer ID is required")]
        public Guid CustomerId { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 10)]
        public string Description { get; set; }

        [Range(1, 4, ErrorMessage = "Priority must be between 1 (Low) and 4 (Critical)")]
        public WorkOrderPriority Priority { get; set; }

        [Range(0, 999, ErrorMessage = "Estimated labor hours must be between 0 and 999")]
        public decimal EstimatedLaborHours { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Estimated cost cannot be negative")]
        public decimal EstimatedCost { get; set; }

        [Required]
        public string CreatedBy { get; set; }
    }

    /// <summary>
    /// DTO for work order response
    /// </summary>
    public class WorkOrderDto
    {
        public Guid Id { get; set; }
        public string WorkOrderNumber { get; set; }
        public string EquipmentVIN { get; set; }
        public string EquipmentType { get; set; }
        public string EquipmentModel { get; set; }
        public string Description { get; set; }
        public WorkOrderPriority Priority { get; set; }
        public WorkOrderStatus Status { get; set; }

        // Technician information (flattened from navigation property)
        public Guid? AssignedTechnicianId { get; set; }
        public string AssignedTechnicianName { get; set; }

        // Customer information (flattened)
        public string CustomerName { get; set; }

        // Estimates
        public decimal EstimatedLaborHours { get; set; }
        public decimal EstimatedCost { get; set; }

        // Dates
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledStartDate { get; set; }
        public DateTime? ScheduledEndDate { get; set; }

        // Computed properties
        public bool IsDelayed { get; set; }
    }

    /// <summary>
    /// DTO for assigning work order to technician
    /// </summary>
    public class AssignWorkOrderDto
    {
        [Required]
        public Guid TechnicianId { get; set; }

        [Required]
        public string AssignedBy { get; set; }

        public DateTime? ScheduledStartDate { get; set; }
        public DateTime? ScheduledEndDate { get; set; }
    }

    /// <summary>
    /// DTO for updating work order status
    /// </summary>
    public class UpdateWorkOrderStatusDto
    {
        [Required]
        [Range(0, 5)]
        public WorkOrderStatus NewStatus { get; set; }

        public string Notes { get; set; }
    }

    /// <summary>
    /// DTO for parts reservation
    /// </summary>
    public class PartReservationDto
    {
        [Required]
        public Guid PartId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// DTO for reserving parts for work order
    /// </summary>
    public class ReservePartsDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one part must be specified")]
        public List<PartReservationDto> Parts { get; set; }

        [Required]
        public string ReservedBy { get; set; }
    }
}
