using HeavyIMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavyIMS.Application.Interfaces
{
    /// <summary>
    /// Service Interface: Part Catalog Management
    /// DEMONSTRATES: Application service coordinating catalog operations
    /// SEPARATION: Catalog management separate from inventory operations
    /// </summary>
    public interface IPartService
    {
        // Query operations
        Task<PartDto> GetPartByIdAsync(Guid partId);
        Task<PartDto> GetPartByPartNumberAsync(string partNumber);
        Task<IEnumerable<PartDto>> GetAllPartsAsync();
        Task<IEnumerable<PartDto>> GetActivePartsAsync();
        Task<IEnumerable<PartDto>> GetDiscontinuedPartsAsync();
        Task<IEnumerable<PartDto>> SearchPartsAsync(string searchTerm);
        Task<IEnumerable<PartDto>> GetPartsByCategoryAsync(string category);
        Task<IEnumerable<PartDto>> GetPartsBySupplierAsync(Guid supplierId);

        // Cross-aggregate queries (coordinates with Inventory)
        Task<PartWithInventoryDto> GetPartWithInventoryAsync(Guid partId);

        // Command operations
        Task<PartDto> CreatePartAsync(CreatePartDto dto);
        Task<PartDto> UpdatePartAsync(Guid partId, UpdatePartDto dto);
        Task<PartDto> UpdatePricingAsync(Guid partId, UpdatePartPricingDto dto);
        Task<PartDto> UpdateSupplierAsync(Guid partId, UpdatePartSupplierDto dto);
        Task<PartDto> UpdateStockLevelsAsync(Guid partId, UpdatePartStockLevelsDto dto);
        Task<PartDto> DiscontinuePartAsync(Guid partId);
        Task<PartDto> ReactivatePartAsync(Guid partId);
    }
}
