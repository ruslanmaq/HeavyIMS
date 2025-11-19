using HeavyIMS.Application.DTOs;
using HeavyIMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavyIMS.Application.Interfaces
{
    /// <summary>
    /// Work Order Service Interface
    /// DEMONSTRATES: Interface Segregation Principle (SOLID)
    ///
    /// WHY INTERFACES?
    /// - Dependency Inversion: Depend on abstractions, not concretions
    /// - Testability: Easy to create mocks for unit testing
    /// - Flexibility: Can swap implementations without changing consumers
    /// - Multiple implementations: Can have different implementations for different scenarios
    /// </summary>
    public interface IWorkOrderService
    {
        Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto dto);
        Task<WorkOrderDto> GetWorkOrderByIdAsync(Guid id);
        Task<IEnumerable<WorkOrderDto>> GetPendingWorkOrdersAsync();
        Task<WorkOrderDto> AssignWorkOrderAsync(Guid workOrderId, Guid technicianId, string assignedBy);
        Task<WorkOrderDto> UpdateStatusAsync(Guid workOrderId, WorkOrderStatus newStatus);
        Task ReservePartsForWorkOrderAsync(Guid workOrderId, List<PartReservationDto> partReservations, string reservedBy);
    }

    /// <summary>
    /// Technician Service Interface
    /// </summary>
    public interface ITechnicianService
    {
        Task<IEnumerable<TechnicianDto>> GetAvailableTechniciansAsync();
        Task<TechnicianDto> GetTechnicianWorkloadAsync(Guid technicianId);
    }

    /// <summary>
    /// Notification Service Interface
    /// ADDRESSES CHALLENGE 3: Automated notifications
    /// </summary>
    public interface INotificationService
    {
        Task SendWorkOrderCreatedNotificationAsync(Guid workOrderId, string customerEmail, string customerPhone);
        Task SendWorkOrderAssignedNotificationAsync(Guid workOrderId, string technicianEmail, string customerEmail);
        Task SendWorkOrderStatusChangedNotificationAsync(Guid workOrderId, WorkOrderStatus oldStatus, WorkOrderStatus newStatus);
        Task SendLowStockAlertAsync(Guid partId, int currentQuantity, int minimumQuantity);
    }

    // DTOs for other services (simplified)
    public class TechnicianDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public TechnicianStatus Status { get; set; }
        public int ActiveJobCount { get; set; }
        public decimal WorkloadPercentage { get; set; }
    }
}
