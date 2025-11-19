using System;
using System.Collections.Generic;

namespace HeavyIMS.Application.DTOs
{
    /// <summary>
    /// DTO: Part catalog information
    /// USED BY: API responses, part listings
    /// </summary>
    public class PartDto
    {
        public Guid PartId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ProfitMargin { get; set; }
        public Guid? SupplierId { get; set; }
        public string SupplierPartNumber { get; set; }
        public int LeadTimeDays { get; set; }
        public int DefaultMinimumStockLevel { get; set; }
        public int DefaultMaximumStockLevel { get; set; }
        public int DefaultReorderQuantity { get; set; }
        public bool IsActive { get; set; }
        public bool IsDiscontinued { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO: Create new part in catalog
    /// USED BY: POST /api/parts
    /// </summary>
    public class CreatePartDto
    {
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
    }

    /// <summary>
    /// DTO: Update part information
    /// USED BY: PUT /api/parts/{id}
    /// </summary>
    public class UpdatePartDto
    {
        public string PartName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// DTO: Update part pricing
    /// USED BY: PUT /api/parts/{id}/pricing
    /// </summary>
    public class UpdatePartPricingDto
    {
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
    }

    /// <summary>
    /// DTO: Update supplier information
    /// USED BY: PUT /api/parts/{id}/supplier
    /// </summary>
    public class UpdatePartSupplierDto
    {
        public Guid SupplierId { get; set; }
        public string SupplierPartNumber { get; set; }
        public int LeadTimeDays { get; set; }
    }

    /// <summary>
    /// DTO: Update default stock levels
    /// USED BY: PUT /api/parts/{id}/stock-levels
    /// </summary>
    public class UpdatePartStockLevelsDto
    {
        public int MinimumStockLevel { get; set; }
        public int MaximumStockLevel { get; set; }
        public int ReorderQuantity { get; set; }
    }

    /// <summary>
    /// DTO: Part with inventory across all warehouses
    /// DEMONSTRATES: Cross-aggregate query coordination
    /// USED BY: GET /api/parts/{id} with inventory details
    /// </summary>
    public class PartWithInventoryDto
    {
        public PartDto Part { get; set; }
        public int TotalQuantityOnHand { get; set; }
        public int TotalQuantityReserved { get; set; }
        public int TotalAvailable { get; set; }
        public List<InventoryLocationSummaryDto> Locations { get; set; }

        public PartWithInventoryDto()
        {
            Locations = new List<InventoryLocationSummaryDto>();
        }
    }

    /// <summary>
    /// DTO: Summary of inventory at a specific location
    /// USED BY: Part detail with inventory breakdown
    /// </summary>
    public class InventoryLocationSummaryDto
    {
        public Guid InventoryId { get; set; }
        public string Warehouse { get; set; }
        public string BinLocation { get; set; }
        public int QuantityOnHand { get; set; }
        public int QuantityReserved { get; set; }
        public int Available { get; set; }
        public int MinimumStockLevel { get; set; }
        public int MaximumStockLevel { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsOutOfStock { get; set; }
    }
}
