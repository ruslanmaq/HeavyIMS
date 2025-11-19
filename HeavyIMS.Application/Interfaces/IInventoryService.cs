using HeavyIMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavyIMS.Application.Interfaces
{
    /// <summary>
    /// Service Interface: Inventory Operations Management
    /// DEMONSTRATES: Application service coordinating warehouse operations
    /// ADDRESSES CHALLENGE 2: Real-time inventory tracking, low stock alerts
    /// </summary>
    public interface IInventoryService
    {
        // Query operations
        Task<InventoryDto> GetInventoryByIdAsync(Guid inventoryId);
        Task<IEnumerable<InventoryDto>> GetInventoryByPartIdAsync(Guid partId);
        Task<InventoryDto> GetInventoryByPartAndWarehouseAsync(Guid partId, string warehouse);
        Task<IEnumerable<InventoryDto>> GetInventoryByWarehouseAsync(string warehouse);
        Task<InventoryWithTransactionsDto> GetInventoryWithTransactionsAsync(Guid inventoryId);

        // Alerts and reporting
        Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync();
        Task<IEnumerable<InventoryDto>> GetOutOfStockInventoryAsync();
        Task<IEnumerable<WarehouseInventorySummaryDto>> GetWarehouseSummariesAsync();
        Task<WarehouseInventorySummaryDto> GetWarehouseSummaryAsync(string warehouse);

        // Command operations - Create
        Task<InventoryDto> CreateInventoryLocationAsync(CreateInventoryDto dto);

        // Command operations - Stock movements
        Task<InventoryDto> ReservePartsAsync(ReserveInventoryDto dto);
        Task<InventoryDto> ReleaseReservationAsync(ReleaseReservationDto dto);
        Task<InventoryDto> IssuePartsAsync(IssuePartsDto dto);
        Task<InventoryDto> ReceivePartsAsync(ReceivePartsDto dto);
        Task<InventoryDto> AdjustInventoryAsync(AdjustInventoryDto dto);

        // Command operations - Location management
        Task<InventoryDto> UpdateStockLevelsAsync(Guid inventoryId, UpdateInventoryStockLevelsDto dto);
        Task<InventoryDto> MoveInventoryAsync(Guid inventoryId, MoveInventoryDto dto);
        Task DeactivateInventoryAsync(Guid inventoryId);

        // Availability checks
        Task<bool> IsQuantityAvailableAsync(Guid partId, string warehouse, int quantity);
        Task<int> GetTotalQuantityOnHandAsync(Guid partId);
        Task<int> GetTotalAvailableQuantityAsync(Guid partId);
    }
}
