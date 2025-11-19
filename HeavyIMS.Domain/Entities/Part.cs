using HeavyIMS.Domain.Events;
using HeavyIMS.Domain.ValueObjects;
using System;

namespace HeavyIMS.Domain.Entities
{
    /// <summary>
    /// Domain Entity: Part (Catalog Aggregate Root)
    /// Represents product catalog information for a part/component
    /// SEPARATION OF CONCERNS: Catalog data separate from inventory operations
    ///
    /// DDD Pattern: AGGREGATE ROOT (Catalog Bounded Context)
    /// - Independent lifecycle (managed by catalog/product team)
    /// - Consistency boundary for pricing, supplier, and categorization data
    /// - Referenced by Inventory and WorkOrderPart via PartId (cross-aggregate reference)
    /// - No direct navigation properties TO other aggregates
    /// - Inherits from AggregateRoot for domain event collection
    ///
    /// Why it's a Separate Aggregate from Inventory:
    /// 1. Different Teams: Catalog team manages Parts, warehouse team manages Inventory
    /// 2. Different Change Frequencies: Prices change rarely; inventory moves constantly
    /// 3. Multi-Warehouse Support: One Part can have multiple Inventory locations
    /// 4. Scalability: No contention between catalog updates and inventory operations
    /// 5. Bounded Contexts: Catalog (what exists) vs Operations (where it is)
    ///
    /// This aggregate manages:
    /// - Part master data (number, name, description)
    /// - Pricing information (cost and selling price)
    /// - Supplier relationships and lead times
    /// - Categorization and status (active/discontinued)
    /// - Default stock level templates for new inventory locations
    ///
    /// DOES NOT manage inventory quantities - see Inventory aggregate
    /// Cross-aggregate communication via domain events (PartPriceUpdated, PartDiscontinued)
    /// </summary>
    public class Part : AggregateRoot
    {
        public Guid PartId { get; private set; }

        // Part identification
        public string PartNumber { get; private set; }  // OEM part number (unique)
        public string PartName { get; private set; }
        public string Description { get; private set; }
        public string Category { get; private set; }    // Engine, Hydraulics, Electrical, etc.

        // Pricing (VALUE OBJECTS)
        // BEFORE: Two separate decimal properties
        // AFTER: Money value objects for catalog pricing with currency and validation
        public Money UnitCost { get; private set; }
        public Money UnitPrice { get; private set; }

        // Supplier information
        public Guid? SupplierId { get; private set; }
        public string SupplierPartNumber { get; private set; }
        public int LeadTimeDays { get; private set; }  // Standard lead time from supplier

        // Reorder defaults (used when creating inventory locations)
        public int DefaultMinimumStockLevel { get; private set; }
        public int DefaultMaximumStockLevel { get; private set; }
        public int DefaultReorderQuantity { get; private set; }

        // Audit
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDiscontinued { get; private set; }

        private Part() { }

        /// <summary>
        /// Factory Method: Create new part in catalog
        /// DEMONSTRATES: Catalog-focused creation without inventory concerns
        /// </summary>
        public static Part Create(
            string partNumber,
            string partName,
            string description,
            string category,
            decimal unitCost,
            decimal unitPrice)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                throw new ArgumentException("Part number is required", nameof(partNumber));

            if (string.IsNullOrWhiteSpace(partName))
                throw new ArgumentException("Part name is required", nameof(partName));

            // VALUE OBJECTS: Money validates non-negative automatically
            var cost = Money.Create(unitCost);
            var price = Money.Create(unitPrice);

            var part = new Part
            {
                PartId = Guid.NewGuid(),
                PartNumber = partNumber,
                PartName = partName,
                Description = description,
                Category = category,
                UnitCost = cost,
                UnitPrice = price,
                SupplierPartNumber = string.Empty,
                LeadTimeDays = 0,
                DefaultMinimumStockLevel = 0,
                DefaultMaximumStockLevel = 0,
                DefaultReorderQuantity = 0,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsDiscontinued = false
            };

            return part;
        }

        /// <summary>
        /// Update pricing information
        /// BUSINESS RULE: Price updates affect catalog only, inventory records reference this
        /// USES VALUE OBJECTS: Money for pricing
        /// </summary>
        public void UpdatePricing(decimal unitCost, decimal unitPrice)
        {
            var oldCost = UnitCost.Amount;
            var oldPrice = UnitPrice.Amount;

            // VALUE OBJECTS: Money validates non-negative automatically
            UnitCost = Money.Create(unitCost);
            UnitPrice = Money.Create(unitPrice);
            UpdatedAt = DateTime.UtcNow;

            // Raise domain event for cross-aggregate communication
            // This allows inventory valuation reports and analytics to update
            RaiseDomainEvent(new PartPriceUpdated(
                PartId,
                oldCost,
                unitCost,
                oldPrice,
                unitPrice
            ));
        }

        /// <summary>
        /// Update part information (description, category, etc.)
        /// </summary>
        public void UpdateInformation(string partName, string description, string category)
        {
            if (string.IsNullOrWhiteSpace(partName))
                throw new ArgumentException("Part name is required", nameof(partName));

            PartName = partName;
            Description = description;
            Category = category;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Update supplier information
        /// USED FOR: Integration with supplier APIs and procurement systems
        /// </summary>
        public void UpdateSupplierInfo(Guid supplierId, string supplierPartNumber, int leadTimeDays)
        {
            if (leadTimeDays < 0)
                throw new ArgumentException("Lead time cannot be negative", nameof(leadTimeDays));

            SupplierId = supplierId;
            SupplierPartNumber = supplierPartNumber;
            LeadTimeDays = leadTimeDays;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Set default stock levels for new inventory locations
        /// BUSINESS RULE: These are templates used when adding part to a new warehouse
        /// </summary>
        public void SetDefaultStockLevels(int minimumStockLevel, int maximumStockLevel, int reorderQuantity)
        {
            if (minimumStockLevel < 0)
                throw new ArgumentException("Minimum stock level cannot be negative", nameof(minimumStockLevel));

            if (maximumStockLevel < minimumStockLevel)
                throw new ArgumentException("Maximum must be >= minimum", nameof(maximumStockLevel));

            if (reorderQuantity < 0)
                throw new ArgumentException("Reorder quantity cannot be negative", nameof(reorderQuantity));

            DefaultMinimumStockLevel = minimumStockLevel;
            DefaultMaximumStockLevel = maximumStockLevel;
            DefaultReorderQuantity = reorderQuantity;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark part as discontinued
        /// BUSINESS RULE: Discontinued parts can't be added to new work orders
        /// </summary>
        public void Discontinue()
        {
            IsDiscontinued = true;
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;

            // Raise domain event for cross-aggregate communication
            // This alerts inventory system to stop reordering
            // and work order system to warn about using discontinued parts
            RaiseDomainEvent(new PartDiscontinued(
                PartId,
                PartNumber
            ));
        }

        /// <summary>
        /// Reactivate a discontinued part
        /// </summary>
        public void Reactivate()
        {
            if (IsDiscontinued)
            {
                IsDiscontinued = false;
            }

            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Calculate profit margin
        /// BUSINESS LOGIC: Used for pricing analysis and reports
        /// USES VALUE OBJECTS: Money arithmetic operations
        /// </summary>
        public decimal GetProfitMargin()
        {
            if (UnitPrice.Amount == 0)
                return 0;

            // VALUE OBJECTS: Money supports subtraction
            var profit = UnitPrice - UnitCost;
            return (profit.Amount / UnitPrice.Amount) * 100;
        }
    }
}
