using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Interfaces;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HeavyIMS.Application.Services
{
    /// <summary>
    /// Work Order Service - Application Layer
    /// DEMONSTRATES:
    /// - Service Layer Pattern (business logic separate from controllers)
    /// - Dependency Injection usage
    /// - Distributed Caching
    /// - Exception handling
    /// - Async/Await patterns
    ///
    /// ADDRESSES: Challenges 1, 2, 3 (Scheduling, Parts, Communication)
    ///
    /// WHY SERVICE LAYER?
    /// - Keeps controllers thin (controllers just route requests)
    /// - Reusable business logic (can call from multiple controllers)
    /// - Easier to unit test
    /// - Centralized transaction management
    /// </summary>
    public class WorkOrderService : IWorkOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventoryService _inventoryService;
        private readonly INotificationService _notificationService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<WorkOrderService> _logger;

        // Cache key constants
        private const string PENDING_WORKORDERS_CACHE_KEY = "workorders:pending";
        private const string DELAYED_WORKORDERS_CACHE_KEY = "workorders:delayed";

        /// <summary>
        /// Constructor - Dependency Injection
        /// DEMONSTRATES: Multiple dependencies injected via constructor
        /// BENEFITS:
        /// - Explicit dependencies (easy to see what service needs)
        /// - Immutable dependencies (readonly fields)
        /// - Easy to mock for unit testing
        /// </summary>
        public WorkOrderService(
            IUnitOfWork unitOfWork,
            IInventoryService inventoryService,
            INotificationService notificationService,
            IDistributedCache cache,
            ILogger<WorkOrderService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create a new work order
        /// DEMONSTRATES:
        /// - Domain entity creation using factory methods
        /// - Unit of Work transaction
        /// - Exception handling with logging
        /// - Cache invalidation
        /// </summary>
        public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto dto)
        {
            try
            {
                _logger.LogInformation("Creating work order for VIN: {VIN}", dto.EquipmentVIN);

                // VALIDATION: Check if customer exists
                var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
                if (customer == null)
                {
                    throw new ArgumentException($"Customer not found: {dto.CustomerId}");
                }

                // DOMAIN MODEL: Create entity using factory method
                var workOrder = WorkOrder.Create(
                    dto.EquipmentVIN,
                    dto.EquipmentType,
                    dto.EquipmentModel,
                    dto.CustomerId,
                    dto.Description,
                    dto.Priority,
                    dto.CreatedBy);

                // Set estimate if provided (CHALLENGE 5: Estimating)
                if (dto.EstimatedLaborHours > 0)
                {
                    workOrder.SetEstimate(dto.EstimatedLaborHours, dto.EstimatedCost);
                }

                // REPOSITORY: Add to database (via Unit of Work)
                await _unitOfWork.WorkOrders.AddAsync(workOrder);

                // UNIT OF WORK: Save changes
                await _unitOfWork.SaveChangesAsync();

                // CACHE INVALIDATION: Clear pending work orders cache
                await _cache.RemoveAsync(PENDING_WORKORDERS_CACHE_KEY);

                _logger.LogInformation("Work order created successfully: {WorkOrderNumber}",
                    workOrder.WorkOrderNumber);

                // NOTIFICATION: Send confirmation to customer (CHALLENGE 3)
                await _notificationService.SendWorkOrderCreatedNotificationAsync(
                    workOrder.Id, customer.Email, customer.PreferSMSNotifications ? customer.PhoneNumber : null);

                // MAP to DTO for return
                return await MapToDtoAsync(workOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating work order for VIN: {VIN}", dto.EquipmentVIN);
                throw;
            }
        }

        /// <summary>
        /// Assign work order to technician
        /// DEMONSTRATES:
        /// - Complex business operation with multiple steps
        /// - Explicit transaction management
        /// - Domain logic encapsulation
        /// - Automated notifications
        /// - DDD: Load aggregates independently (no navigation properties)
        ///
        /// ADDRESSES CHALLENGE 1: Drag-and-drop scheduling
        /// </summary>
        public async Task<WorkOrderDto> AssignWorkOrderAsync(
            Guid workOrderId,
            Guid technicianId,
            string assignedBy)
        {
            // BEGIN EXPLICIT TRANSACTION
            // WHY? Multiple operations must succeed/fail together
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                _logger.LogInformation(
                    "Assigning work order {WorkOrderId} to technician {TechnicianId}",
                    workOrderId, technicianId);

                // 1. Get work order (aggregate loaded independently)
                var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(workOrderId);
                if (workOrder == null)
                {
                    throw new ArgumentException($"Work order not found: {workOrderId}");
                }

                // 2. Get technician (aggregate loaded independently)
                var technician = await _unitOfWork.Technicians.GetByIdAsync(technicianId);
                if (technician == null)
                {
                    throw new ArgumentException($"Technician not found: {technicianId}");
                }

                // 3. VALIDATION: Check if technician is active
                if (!technician.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Cannot assign inactive technician {technician.FullName}");
                }

                // 4. VALIDATION: Check technician capacity (DDD: query active jobs for this aggregate)
                var activeJobCount = await _unitOfWork.WorkOrders
                    .CountActiveWorkOrdersByTechnicianAsync(technicianId);

                if (!technician.CanAcceptNewJob(activeJobCount))
                {
                    throw new InvalidOperationException(
                        $"Technician {technician.FullName} is at full capacity");
                }

                // 5. DOMAIN LOGIC: Assign technician (stores ID only)
                workOrder.AssignTechnician(technicianId);

                // 6. Update technician status if needed
                var newJobCount = activeJobCount + 1;
                if (technician.Status == TechnicianStatus.Available && !technician.CanAcceptNewJob(newJobCount))
                {
                    technician.UpdateStatus(TechnicianStatus.Busy);
                }

                // 7. Save changes
                await _unitOfWork.SaveChangesAsync();

                // 8. COMMIT TRANSACTION
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation(
                    "Work order {WorkOrderNumber} assigned to {TechnicianName}",
                    workOrder.WorkOrderNumber, technician.FullName);

                // 9. NOTIFICATION: Load customer separately for notification
                var customer = await _unitOfWork.Customers.GetByIdAsync(workOrder.CustomerId);

                // 10. NOTIFICATION: Inform technician and customer (CHALLENGE 3)
                await _notificationService.SendWorkOrderAssignedNotificationAsync(
                    workOrderId, technician.Email, customer.Email);

                // 11. CACHE INVALIDATION
                await _cache.RemoveAsync(PENDING_WORKORDERS_CACHE_KEY);

                return await MapToDtoAsync(workOrder);
            }
            catch (Exception ex)
            {
                // ROLLBACK on error
                await _unitOfWork.RollbackTransactionAsync();

                _logger.LogError(ex,
                    "Error assigning work order {WorkOrderId} to technician {TechnicianId}",
                    workOrderId, technicianId);
                throw;
            }
        }

        /// <summary>
        /// Get pending work orders with caching
        /// DEMONSTRATES:
        /// - Distributed caching pattern
        /// - Cache-aside pattern (lazy loading)
        /// - Serialization/Deserialization
        ///
        /// PERFORMANCE BENEFIT: Reduces database load for frequently accessed data
        /// </summary>
        public async Task<IEnumerable<WorkOrderDto>> GetPendingWorkOrdersAsync()
        {
            try
            {
                // 1. TRY TO GET FROM CACHE
                var cachedData = await _cache.GetStringAsync(PENDING_WORKORDERS_CACHE_KEY);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("Returning pending work orders from cache");

                    // DESERIALIZE from JSON
                    return JsonSerializer.Deserialize<IEnumerable<WorkOrderDto>>(cachedData);
                }

                // 2. CACHE MISS: Get from database
                _logger.LogInformation("Cache miss: Loading pending work orders from database");

                var workOrders = await _unitOfWork.WorkOrders.GetPendingWorkOrdersAsync();

                // Map to DTOs (load related aggregates for each)
                var dtos = new List<WorkOrderDto>();
                foreach (var workOrder in workOrders)
                {
                    dtos.Add(await MapToDtoAsync(workOrder));
                }

                // 3. STORE IN CACHE for next time
                var serializedData = JsonSerializer.Serialize(dtos);

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    // Sliding expiration: Reset timer on access
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                };

                await _cache.SetStringAsync(
                    PENDING_WORKORDERS_CACHE_KEY,
                    serializedData,
                    cacheOptions);

                _logger.LogInformation("Cached {Count} pending work orders", dtos.Count);

                return dtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending work orders");
                throw;
            }
        }

        /// <summary>
        /// Reserve parts for work order
        /// DEMONSTRATES:
        /// - Multi-step transaction
        /// - Domain model interaction
        /// - Business rule enforcement
        ///
        /// ADDRESSES CHALLENGE 2: Parts management
        /// </summary>
        public async Task ReservePartsForWorkOrderAsync(
            Guid workOrderId,
            List<PartReservationDto> partReservations,
            string reservedBy)
        {
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(workOrderId);
                if (workOrder == null)
                {
                    throw new ArgumentException($"Work order not found: {workOrderId}");
                }

                foreach (var reservation in partReservations)
                {
                    // Check if part exists in catalog
                    var part = await _unitOfWork.Parts.GetByIdAsync(reservation.PartId);
                    if (part == null)
                    {
                        throw new ArgumentException($"Part not found in catalog: {reservation.PartId}");
                    }

                    // Check total availability across all warehouses
                    var totalAvailable = await _inventoryService.GetTotalAvailableQuantityAsync(reservation.PartId);
                    if (totalAvailable < reservation.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient inventory for part {part.PartNumber}. " +
                            $"Required: {reservation.Quantity}, Available: {totalAvailable}");
                    }

                    // Reserve from available inventory locations
                    // TODO: Implement smart warehouse selection logic (closest, highest stock, etc.)
                    var inventoryLocations = await _inventoryService.GetInventoryByPartIdAsync(reservation.PartId);
                    var remainingToReserve = reservation.Quantity;

                    foreach (var location in inventoryLocations.Where(l => l.QuantityOnHand - l.QuantityReserved > 0))
                    {
                        if (remainingToReserve <= 0) break;

                        var availableAtLocation = location.QuantityOnHand - location.QuantityReserved;
                        var quantityToReserve = Math.Min(remainingToReserve, availableAtLocation);

                        // Reserve from this warehouse using InventoryService
                        await _inventoryService.ReservePartsAsync(new ReserveInventoryDto
                        {
                            PartId = reservation.PartId,
                            Warehouse = location.Warehouse,
                            Quantity = quantityToReserve,
                            WorkOrderId = workOrderId,
                            RequestedBy = reservedBy
                        });

                        remainingToReserve -= quantityToReserve;
                    }

                    // Add to work order
                    workOrder.AddRequiredPart(
                        reservation.PartId,
                        reservation.Quantity,
                        isAvailable: true);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation(
                    "Reserved {Count} parts for work order {WorkOrderId}",
                    partReservations.Count, workOrderId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Error reserving parts for work order {WorkOrderId}", workOrderId);
                throw;
            }
        }

        /// <summary>
        /// Get work order by ID
        /// DDD: Load aggregate independently without navigation properties
        /// </summary>
        public async Task<WorkOrderDto> GetWorkOrderByIdAsync(Guid id)
        {
            var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(id);
            if (workOrder == null)
            {
                throw new ArgumentException($"Work order not found: {id}");
            }

            return await MapToDtoAsync(workOrder);
        }

        /// <summary>
        /// Update work order status
        /// DEMONSTRATES: State machine validation via domain model
        /// TRIGGERS: Automated notifications (CHALLENGE 3)
        /// </summary>
        public async Task<WorkOrderDto> UpdateStatusAsync(
            Guid workOrderId,
            WorkOrderStatus newStatus)
        {
            try
            {
                var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(workOrderId);
                if (workOrder == null)
                {
                    throw new ArgumentException($"Work order not found: {workOrderId}");
                }

                var oldStatus = workOrder.Status;

                // DOMAIN METHOD: Validates state transition
                workOrder.UpdateStatus(newStatus);

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Work order {WorkOrderNumber} status changed from {OldStatus} to {NewStatus}",
                    workOrder.WorkOrderNumber, oldStatus, newStatus);

                // NOTIFICATION: Send status update (CHALLENGE 3)
                await _notificationService.SendWorkOrderStatusChangedNotificationAsync(
                    workOrderId, oldStatus, newStatus);

                return await MapToDtoAsync(workOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating work order status for {WorkOrderId}", workOrderId);
                throw;
            }
        }

        /// <summary>
        /// Helper method: Map domain entity to DTO
        /// DEMONSTRATES: Separation of domain models and API contracts
        /// WHY DTOs? Control exactly what data is exposed via API
        /// DDD: Loads related aggregates independently (no navigation properties)
        /// </summary>
        private async Task<WorkOrderDto> MapToDtoAsync(WorkOrder workOrder)
        {
            // Load related aggregates independently (DDD principle)
            var customer = await _unitOfWork.Customers.GetByIdAsync(workOrder.CustomerId);

            Technician technician = null;
            if (workOrder.AssignedTechnicianId.HasValue)
            {
                technician = await _unitOfWork.Technicians.GetByIdAsync(workOrder.AssignedTechnicianId.Value);
            }

            return new WorkOrderDto
            {
                Id = workOrder.Id,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                // VALUE OBJECTS: Access nested properties from EquipmentIdentifier
                EquipmentVIN = workOrder.Equipment.VIN,
                EquipmentType = workOrder.Equipment.Type,
                EquipmentModel = workOrder.Equipment.Model,
                Description = workOrder.Description,
                Priority = workOrder.Priority,
                Status = workOrder.Status,
                EstimatedLaborHours = workOrder.EstimatedLaborHours,
                // VALUE OBJECTS: Extract Amount from Money
                EstimatedCost = workOrder.EstimatedCost.Amount,
                AssignedTechnicianId = workOrder.AssignedTechnicianId,
                AssignedTechnicianName = technician?.FullName,
                CustomerName = customer?.CompanyName,
                CreatedAt = workOrder.CreatedAt,
                // VALUE OBJECTS: Extract Start from DateRange
                ScheduledStartDate = workOrder.ScheduledPeriod?.Start,
                IsDelayed = workOrder.IsDelayed
            };
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. WHY SERVICE LAYER?
 *    - Separates business logic from controllers
 *    - Reusable across multiple entry points (API, CLI, etc.)
 *    - Easier to test (mock dependencies)
 *
 * 2. DEPENDENCY INJECTION BENEFITS:
 *    - Testability: Inject mocks for unit testing
 *    - Flexibility: Swap implementations without changing code
 *    - Explicit dependencies: Clear what service needs
 *
 * 3. CACHING STRATEGY:
 *    - Cache-aside pattern: Load from DB on miss
 *    - Distributed cache: Share across multiple API instances
 *    - Invalidation: Clear cache when data changes
 *
 * 4. TRANSACTION MANAGEMENT:
 *    - Explicit transactions for multi-step operations
 *    - Rollback on error ensures consistency
 *    - Unit of Work coordinates repositories
 *
 * 5. LOGGING:
 *    - Structured logging with parameters
 *    - Different log levels (Info, Warning, Error)
 *    - Helps with debugging and monitoring
 */
