using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Interfaces;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HeavyIMS.Application.Services
{
    /// <summary>
    /// Service Implementation: Inventory Operations Management
    /// DEMONSTRATES: Application service orchestrating warehouse operations
    /// ADDRESSES CHALLENGE 2: Real-time inventory tracking, automated alerts
    /// </summary>
    public class InventoryService : IInventoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public InventoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        #region Query Operations

        public async Task<InventoryDto> GetInventoryByIdAsync(Guid inventoryId)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(inventoryId);
            return inventory != null ? MapToDto(inventory) : null;
        }

        public async Task<IEnumerable<InventoryDto>> GetInventoryByPartIdAsync(Guid partId)
        {
            var inventories = await _unitOfWork.Inventory.GetByPartIdAsync(partId);
            return inventories.Select(MapToDto);
        }

        public async Task<InventoryDto> GetInventoryByPartAndWarehouseAsync(Guid partId, string warehouse)
        {
            var inventory = await _unitOfWork.Inventory.GetByPartAndWarehouseAsync(partId, warehouse);
            return inventory != null ? MapToDto(inventory) : null;
        }

        public async Task<IEnumerable<InventoryDto>> GetInventoryByWarehouseAsync(string warehouse)
        {
            var inventories = await _unitOfWork.Inventory.GetByWarehouseAsync(warehouse);
            return inventories.Select(MapToDto);
        }

        public async Task<InventoryWithTransactionsDto> GetInventoryWithTransactionsAsync(Guid inventoryId)
        {
            var inventory = await _unitOfWork.Inventory.GetInventoryWithTransactionsAsync(inventoryId);
            if (inventory == null)
                return null;

            return new InventoryWithTransactionsDto
            {
                Inventory = MapToDto(inventory),
                Transactions = inventory.Transactions.Select(MapTransactionToDto).ToList()
            };
        }

        #endregion

        #region Alerts and Reporting

        /// <summary>
        /// Get low stock alerts with part information
        /// DEMONSTRATES: Cross-aggregate coordination for alerts
        /// CRITICAL FOR: Challenge 2 - Automated low stock alerts
        /// </summary>
        public async Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync()
        {
            var lowStockInventories = await _unitOfWork.Inventory.GetLowStockInventoryAsync();
            var alerts = new List<LowStockAlertDto>();

            foreach (var inventory in lowStockInventories)
            {
                // Get Part information from catalog aggregate
                var part = await _unitOfWork.Parts.GetByIdAsync(inventory.PartId);
                if (part == null) continue;

                alerts.Add(new LowStockAlertDto
                {
                    InventoryId = inventory.InventoryId,
                    PartId = inventory.PartId,
                    PartNumber = part.PartNumber,
                    PartName = part.PartName,
                    Warehouse = inventory.Warehouse,
                    CurrentQuantity = inventory.GetAvailableQuantity(),
                    MinimumStockLevel = inventory.MinimumStockLevel,
                    ReorderQuantity = inventory.CalculateReorderQuantity(),
                    // VALUE OBJECTS: Extract Amount from Money
                    UnitCost = part.UnitCost.Amount,
                    LeadTimeDays = part.LeadTimeDays
                });
            }

            return alerts;
        }

        public async Task<IEnumerable<InventoryDto>> GetOutOfStockInventoryAsync()
        {
            var inventories = await _unitOfWork.Inventory.GetOutOfStockInventoryAsync();
            return inventories.Select(MapToDto);
        }

        public async Task<IEnumerable<WarehouseInventorySummaryDto>> GetWarehouseSummariesAsync()
        {
            var allInventory = await _unitOfWork.Inventory.GetAllAsync();
            var grouped = allInventory.GroupBy(i => i.Warehouse);

            var summaries = new List<WarehouseInventorySummaryDto>();

            foreach (var group in grouped)
            {
                summaries.Add(await CreateWarehouseSummary(group.Key, group));
            }

            return summaries;
        }

        public async Task<WarehouseInventorySummaryDto> GetWarehouseSummaryAsync(string warehouse)
        {
            var inventories = await _unitOfWork.Inventory.GetByWarehouseAsync(warehouse);
            return await CreateWarehouseSummary(warehouse, inventories);
        }

        private async Task<WarehouseInventorySummaryDto> CreateWarehouseSummary(string warehouse, IEnumerable<Inventory> inventories)
        {
            var inventoryList = inventories.ToList();
            var totalValue = 0m;

            // Calculate total inventory value (need Part.UnitCost)
            foreach (var inventory in inventoryList)
            {
                var part = await _unitOfWork.Parts.GetByIdAsync(inventory.PartId);
                if (part != null)
                {
                    // VALUE OBJECTS: Use Money.Multiply() or extract Amount
                    totalValue += inventory.QuantityOnHand * part.UnitCost.Amount;
                }
            }

            return new WarehouseInventorySummaryDto
            {
                Warehouse = warehouse,
                TotalParts = inventoryList.Count,
                TotalQuantityOnHand = inventoryList.Sum(i => i.QuantityOnHand),
                TotalQuantityReserved = inventoryList.Sum(i => i.QuantityReserved),
                TotalAvailable = inventoryList.Sum(i => i.GetAvailableQuantity()),
                LowStockCount = inventoryList.Count(i => i.IsLowStock()),
                OutOfStockCount = inventoryList.Count(i => i.IsOutOfStock()),
                TotalInventoryValue = totalValue
            };
        }

        #endregion

        #region Command Operations - Create

        public async Task<InventoryDto> CreateInventoryLocationAsync(CreateInventoryDto dto)
        {
            // Validate part exists
            var part = await _unitOfWork.Parts.GetByIdAsync(dto.PartId);
            if (part == null)
                throw new InvalidOperationException($"Part {dto.PartId} not found.");

            // Check if inventory location already exists
            var existing = await _unitOfWork.Inventory.GetByPartAndWarehouseAsync(dto.PartId, dto.Warehouse);
            if (existing != null)
                throw new InvalidOperationException(
                    $"Inventory location for part {dto.PartId} at warehouse '{dto.Warehouse}' already exists.");

            // Create domain entity
            var inventory = Inventory.Create(
                dto.PartId,
                dto.Warehouse,
                dto.BinLocation,
                dto.MinimumStockLevel,
                dto.MaximumStockLevel);

            // Persist
            await _unitOfWork.Inventory.AddAsync(inventory);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(inventory);
        }

        #endregion

        #region Command Operations - Stock Movements

        public async Task<InventoryDto> ReservePartsAsync(ReserveInventoryDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByPartAndWarehouseAsync(dto.PartId, dto.Warehouse);
            if (inventory == null)
                throw new InvalidOperationException(
                    $"Inventory location for part {dto.PartId} at warehouse '{dto.Warehouse}' not found.");

            // Reserve using domain method
            inventory.ReserveParts(dto.Quantity, dto.WorkOrderId, dto.RequestedBy);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Publish InventoryReserved domain event

            return MapToDto(inventory);
        }

        public async Task<InventoryDto> ReleaseReservationAsync(ReleaseReservationDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(dto.InventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {dto.InventoryId} not found.");

            // Release using domain method
            inventory.ReleaseReservation(dto.Quantity, dto.WorkOrderId, dto.ReleasedBy);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(inventory);
        }

        public async Task<InventoryDto> IssuePartsAsync(IssuePartsDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(dto.InventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {dto.InventoryId} not found.");

            // Issue using domain method
            inventory.IssueParts(dto.Quantity, dto.WorkOrderId, dto.IssuedBy);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Publish InventoryIssued domain event
            // TODO: Check if low stock and publish InventoryLowStockDetected event

            return MapToDto(inventory);
        }

        public async Task<InventoryDto> ReceivePartsAsync(ReceivePartsDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(dto.InventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {dto.InventoryId} not found.");

            // Receive using domain method
            inventory.ReceiveParts(dto.Quantity, dto.ReceivedBy, dto.ReferenceNumber);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Publish InventoryReceived domain event

            return MapToDto(inventory);
        }

        public async Task<InventoryDto> AdjustInventoryAsync(AdjustInventoryDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(dto.InventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {dto.InventoryId} not found.");

            // Adjust using domain method
            inventory.AdjustQuantity(dto.NewQuantity, dto.Reason, dto.AdjustedBy);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Publish InventoryAdjusted domain event

            return MapToDto(inventory);
        }

        #endregion

        #region Command Operations - Location Management

        public async Task<InventoryDto> UpdateStockLevelsAsync(Guid inventoryId, UpdateInventoryStockLevelsDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(inventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {inventoryId} not found.");

            // Update using domain method
            inventory.UpdateStockLevels(dto.MinimumStockLevel, dto.MaximumStockLevel, dto.ReorderQuantity);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(inventory);
        }

        public async Task<InventoryDto> MoveInventoryAsync(Guid inventoryId, MoveInventoryDto dto)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(inventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {inventoryId} not found.");

            // Move using domain method
            inventory.MoveToBinLocation(dto.NewBinLocation, dto.MovedBy);

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(inventory);
        }

        public async Task DeactivateInventoryAsync(Guid inventoryId)
        {
            var inventory = await _unitOfWork.Inventory.GetByIdAsync(inventoryId);
            if (inventory == null)
                throw new InvalidOperationException($"Inventory {inventoryId} not found.");

            // Deactivate using domain method
            inventory.Deactivate();

            _unitOfWork.Inventory.Update(inventory);
            await _unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region Availability Checks

        public async Task<bool> IsQuantityAvailableAsync(Guid partId, string warehouse, int quantity)
        {
            return await _unitOfWork.Inventory.IsQuantityAvailableAsync(partId, warehouse, quantity);
        }

        public async Task<int> GetTotalQuantityOnHandAsync(Guid partId)
        {
            return await _unitOfWork.Inventory.GetTotalQuantityOnHandAsync(partId);
        }

        public async Task<int> GetTotalAvailableQuantityAsync(Guid partId)
        {
            return await _unitOfWork.Inventory.GetTotalAvailableQuantityAsync(partId);
        }

        #endregion

        #region Mapping Helpers

        private InventoryDto MapToDto(Inventory inventory)
        {
            return new InventoryDto
            {
                InventoryId = inventory.InventoryId,
                PartId = inventory.PartId,
                Warehouse = inventory.Warehouse,
                BinLocation = inventory.BinLocation,
                QuantityOnHand = inventory.QuantityOnHand,
                QuantityReserved = inventory.QuantityReserved,
                Available = inventory.GetAvailableQuantity(),
                MinimumStockLevel = inventory.MinimumStockLevel,
                MaximumStockLevel = inventory.MaximumStockLevel,
                ReorderQuantity = inventory.ReorderQuantity,
                IsLowStock = inventory.IsLowStock(),
                IsOutOfStock = inventory.IsOutOfStock(),
                IsActive = inventory.IsActive,
                CreatedAt = inventory.CreatedAt,
                UpdatedAt = inventory.UpdatedAt
            };
        }

        private InventoryTransactionDto MapTransactionToDto(InventoryTransaction transaction)
        {
            return new InventoryTransactionDto
            {
                TransactionId = transaction.TransactionId,
                InventoryId = transaction.InventoryId,
                TransactionType = transaction.TransactionType.ToString(),
                Quantity = transaction.Quantity,
                WorkOrderId = transaction.WorkOrderId,
                ReferenceNumber = transaction.ReferenceNumber,
                Notes = transaction.Notes,
                TransactionDate = transaction.TransactionDate,
                TransactionBy = transaction.TransactionBy
            };
        }

        #endregion
    }
}
