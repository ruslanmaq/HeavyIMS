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
    /// Service Implementation: Part Catalog Management
    /// DEMONSTRATES: Application service orchestrating catalog operations
    /// COORDINATES: Part aggregate and Inventory aggregate queries
    /// </summary>
    public class PartService : IPartService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PartService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        #region Query Operations

        public async Task<PartDto> GetPartByIdAsync(Guid partId)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            return part != null ? MapToDto(part) : null;
        }

        public async Task<PartDto> GetPartByPartNumberAsync(string partNumber)
        {
            var part = await _unitOfWork.Parts.GetByPartNumberAsync(partNumber);
            return part != null ? MapToDto(part) : null;
        }

        public async Task<IEnumerable<PartDto>> GetAllPartsAsync()
        {
            var parts = await _unitOfWork.Parts.GetAllAsync();
            return parts.Select(MapToDto);
        }

        public async Task<IEnumerable<PartDto>> GetActivePartsAsync()
        {
            var parts = await _unitOfWork.Parts.GetActivePartsAsync();
            return parts.Select(MapToDto);
        }

        public async Task<IEnumerable<PartDto>> GetDiscontinuedPartsAsync()
        {
            var parts = await _unitOfWork.Parts.GetDiscontinuedPartsAsync();
            return parts.Select(MapToDto);
        }

        public async Task<IEnumerable<PartDto>> SearchPartsAsync(string searchTerm)
        {
            var parts = await _unitOfWork.Parts.SearchPartsAsync(searchTerm);
            return parts.Select(MapToDto);
        }

        public async Task<IEnumerable<PartDto>> GetPartsByCategoryAsync(string category)
        {
            var parts = await _unitOfWork.Parts.GetPartsByCategoryAsync(category);
            return parts.Select(MapToDto);
        }

        public async Task<IEnumerable<PartDto>> GetPartsBySupplierAsync(Guid supplierId)
        {
            var parts = await _unitOfWork.Parts.GetPartsBySupplierAsync(supplierId);
            return parts.Select(MapToDto);
        }

        #endregion

        #region Cross-Aggregate Queries

        /// <summary>
        /// Get part with inventory across all warehouses
        /// DEMONSTRATES: Application service coordinating multiple aggregates
        /// CRITICAL: This is where we join catalog and inventory data
        /// </summary>
        public async Task<PartWithInventoryDto> GetPartWithInventoryAsync(Guid partId)
        {
            // Get Part from catalog aggregate
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                return null;

            // Get all Inventory locations for this part
            var inventories = await _unitOfWork.Inventory.GetByPartIdAsync(partId);
            var inventoryList = inventories.ToList();

            // Combine into DTO
            var dto = new PartWithInventoryDto
            {
                Part = MapToDto(part),
                TotalQuantityOnHand = inventoryList.Sum(i => i.QuantityOnHand),
                TotalQuantityReserved = inventoryList.Sum(i => i.QuantityReserved),
                TotalAvailable = inventoryList.Sum(i => i.GetAvailableQuantity()),
                Locations = inventoryList.Select(MapToLocationSummary).ToList()
            };

            return dto;
        }

        #endregion

        #region Command Operations

        public async Task<PartDto> CreatePartAsync(CreatePartDto dto)
        {
            // Validate unique part number
            var existing = await _unitOfWork.Parts.GetByPartNumberAsync(dto.PartNumber);
            if (existing != null)
                throw new InvalidOperationException($"Part number '{dto.PartNumber}' already exists.");

            // Create domain entity
            var part = Part.Create(
                dto.PartNumber,
                dto.PartName,
                dto.Description,
                dto.Category,
                dto.UnitCost,
                dto.UnitPrice);

            // Persist
            await _unitOfWork.Parts.AddAsync(part);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(part);
        }

        public async Task<PartDto> UpdatePartAsync(Guid partId, UpdatePartDto dto)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                throw new InvalidOperationException($"Part {partId} not found.");

            // Update using domain method
            part.UpdateInformation(dto.PartName, dto.Description, dto.Category);

            _unitOfWork.Parts.Update(part);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(part);
        }

        public async Task<PartDto> UpdatePricingAsync(Guid partId, UpdatePartPricingDto dto)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                throw new InvalidOperationException($"Part {partId} not found.");

            // Update using domain method
            part.UpdatePricing(dto.UnitCost, dto.UnitPrice);

            _unitOfWork.Parts.Update(part);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Publish PartPriceUpdated domain event

            return MapToDto(part);
        }

        public async Task<PartDto> UpdateSupplierAsync(Guid partId, UpdatePartSupplierDto dto)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                throw new InvalidOperationException($"Part {partId} not found.");

            // Update using domain method
            part.UpdateSupplierInfo(dto.SupplierId, dto.SupplierPartNumber, dto.LeadTimeDays);

            _unitOfWork.Parts.Update(part);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(part);
        }

        public async Task<PartDto> UpdateStockLevelsAsync(Guid partId, UpdatePartStockLevelsDto dto)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                throw new InvalidOperationException($"Part {partId} not found.");

            // Update using domain method
            part.SetDefaultStockLevels(dto.MinimumStockLevel, dto.MaximumStockLevel, dto.ReorderQuantity);

            _unitOfWork.Parts.Update(part);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(part);
        }

        public async Task<PartDto> DiscontinuePartAsync(Guid partId)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                throw new InvalidOperationException($"Part {partId} not found.");

            // Discontinue using domain method
            part.Discontinue();

            _unitOfWork.Parts.Update(part);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Publish PartDiscontinued domain event

            return MapToDto(part);
        }

        public async Task<PartDto> ReactivatePartAsync(Guid partId)
        {
            var part = await _unitOfWork.Parts.GetByIdAsync(partId);
            if (part == null)
                throw new InvalidOperationException($"Part {partId} not found.");

            // Reactivate using domain method
            part.Reactivate();

            _unitOfWork.Parts.Update(part);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(part);
        }

        #endregion

        #region Mapping Helpers

        private PartDto MapToDto(Part part)
        {
            return new PartDto
            {
                PartId = part.PartId,
                PartNumber = part.PartNumber,
                PartName = part.PartName,
                Description = part.Description,
                Category = part.Category,
                // VALUE OBJECTS: Extract Amount from Money
                UnitCost = part.UnitCost.Amount,
                UnitPrice = part.UnitPrice.Amount,
                ProfitMargin = part.GetProfitMargin(),
                SupplierId = part.SupplierId,
                SupplierPartNumber = part.SupplierPartNumber,
                LeadTimeDays = part.LeadTimeDays,
                DefaultMinimumStockLevel = part.DefaultMinimumStockLevel,
                DefaultMaximumStockLevel = part.DefaultMaximumStockLevel,
                DefaultReorderQuantity = part.DefaultReorderQuantity,
                IsActive = part.IsActive,
                IsDiscontinued = part.IsDiscontinued,
                CreatedAt = part.CreatedAt,
                UpdatedAt = part.UpdatedAt
            };
        }

        private InventoryLocationSummaryDto MapToLocationSummary(Inventory inventory)
        {
            return new InventoryLocationSummaryDto
            {
                InventoryId = inventory.InventoryId,
                Warehouse = inventory.Warehouse,
                BinLocation = inventory.BinLocation,
                QuantityOnHand = inventory.QuantityOnHand,
                QuantityReserved = inventory.QuantityReserved,
                Available = inventory.GetAvailableQuantity(),
                MinimumStockLevel = inventory.MinimumStockLevel,
                MaximumStockLevel = inventory.MaximumStockLevel,
                IsLowStock = inventory.IsLowStock(),
                IsOutOfStock = inventory.IsOutOfStock()
            };
        }

        #endregion
    }
}
