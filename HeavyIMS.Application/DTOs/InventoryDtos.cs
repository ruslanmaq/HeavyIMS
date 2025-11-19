using System;
using System.Collections.Generic;

namespace HeavyIMS.Application.DTOs
{
    /// <summary>
    /// DTO: Inventory at a specific warehouse location
    /// USED BY: API responses, inventory listings
    /// </summary>
    public class InventoryDto
    {
        public Guid InventoryId { get; set; }
        public Guid PartId { get; set; }
        public string Warehouse { get; set; }
        public string BinLocation { get; set; }
        public int QuantityOnHand { get; set; }
        public int QuantityReserved { get; set; }
        public int Available { get; set; }
        public int MinimumStockLevel { get; set; }
        public int MaximumStockLevel { get; set; }
        public int ReorderQuantity { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsOutOfStock { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO: Create new inventory location for a part
    /// USED BY: POST /api/inventory
    /// </summary>
    public class CreateInventoryDto
    {
        public Guid PartId { get; set; }
        public string Warehouse { get; set; }
        public string BinLocation { get; set; }
        public int MinimumStockLevel { get; set; }
        public int MaximumStockLevel { get; set; }
    }

    /// <summary>
    /// DTO: Reserve parts from specific inventory location for a work order
    /// USED BY: POST /api/inventory/reserve
    /// NOTE: Different from WorkOrderDtos.ReservePartsDto (which is for work order bulk operations)
    /// </summary>
    public class ReserveInventoryDto
    {
        public Guid PartId { get; set; }
        public string Warehouse { get; set; }
        public int Quantity { get; set; }
        public Guid WorkOrderId { get; set; }
        public string RequestedBy { get; set; }
    }

    /// <summary>
    /// DTO: Issue parts to a work order (physical consumption)
    /// USED BY: POST /api/inventory/issue
    /// </summary>
    public class IssuePartsDto
    {
        public Guid InventoryId { get; set; }
        public int Quantity { get; set; }
        public Guid WorkOrderId { get; set; }
        public string IssuedBy { get; set; }
    }

    /// <summary>
    /// DTO: Receive parts from supplier
    /// USED BY: POST /api/inventory/receive
    /// </summary>
    public class ReceivePartsDto
    {
        public Guid InventoryId { get; set; }
        public int Quantity { get; set; }
        public string ReceivedBy { get; set; }
        public string ReferenceNumber { get; set; }
    }

    /// <summary>
    /// DTO: Adjust inventory quantity (cycle count)
    /// USED BY: POST /api/inventory/adjust
    /// </summary>
    public class AdjustInventoryDto
    {
        public Guid InventoryId { get; set; }
        public int NewQuantity { get; set; }
        public string Reason { get; set; }
        public string AdjustedBy { get; set; }
    }

    /// <summary>
    /// DTO: Release reserved parts
    /// USED BY: POST /api/inventory/release
    /// </summary>
    public class ReleaseReservationDto
    {
        public Guid InventoryId { get; set; }
        public int Quantity { get; set; }
        public Guid WorkOrderId { get; set; }
        public string ReleasedBy { get; set; }
    }

    /// <summary>
    /// DTO: Update stock levels for an inventory location
    /// USED BY: PUT /api/inventory/{id}/stock-levels
    /// </summary>
    public class UpdateInventoryStockLevelsDto
    {
        public int MinimumStockLevel { get; set; }
        public int MaximumStockLevel { get; set; }
        public int ReorderQuantity { get; set; }
    }

    /// <summary>
    /// DTO: Move inventory to different bin location
    /// USED BY: PUT /api/inventory/{id}/bin-location
    /// </summary>
    public class MoveInventoryDto
    {
        public string NewBinLocation { get; set; }
        public string MovedBy { get; set; }
    }

    /// <summary>
    /// DTO: Inventory transaction (audit trail)
    /// USED BY: GET /api/inventory/{id}/transactions
    /// </summary>
    public class InventoryTransactionDto
    {
        public Guid TransactionId { get; set; }
        public Guid InventoryId { get; set; }
        public string TransactionType { get; set; }
        public int Quantity { get; set; }
        public Guid? WorkOrderId { get; set; }
        public string ReferenceNumber { get; set; }
        public string Notes { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionBy { get; set; }
    }

    /// <summary>
    /// DTO: Inventory with transaction history
    /// USED BY: GET /api/inventory/{id} with full details
    /// </summary>
    public class InventoryWithTransactionsDto
    {
        public InventoryDto Inventory { get; set; }
        public List<InventoryTransactionDto> Transactions { get; set; }

        public InventoryWithTransactionsDto()
        {
            Transactions = new List<InventoryTransactionDto>();
        }
    }

    /// <summary>
    /// DTO: Low stock alert
    /// USED BY: GET /api/inventory/lowstock
    /// </summary>
    public class LowStockAlertDto
    {
        public Guid InventoryId { get; set; }
        public Guid PartId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Warehouse { get; set; }
        public int CurrentQuantity { get; set; }
        public int MinimumStockLevel { get; set; }
        public int ReorderQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public int LeadTimeDays { get; set; }
    }

    /// <summary>
    /// DTO: Inventory summary by warehouse
    /// USED BY: Dashboard, reporting
    /// </summary>
    public class WarehouseInventorySummaryDto
    {
        public string Warehouse { get; set; }
        public int TotalParts { get; set; }
        public int TotalQuantityOnHand { get; set; }
        public int TotalQuantityReserved { get; set; }
        public int TotalAvailable { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public decimal TotalInventoryValue { get; set; }
    }
}
