using HeavyIMS.Domain.Events;
using System;
using System.Collections.Generic;

namespace HeavyIMS.Domain.Entities
{
    /// <summary>
    /// Domain Entity: Inventory (Operational Aggregate Root)
    /// Represents stock levels and movements for a specific warehouse location
    /// ADDRESSES CHALLENGE 2: Parts Delays & Inventory Chaos
    ///
    /// DDD Pattern: AGGREGATE ROOT (Operations Bounded Context)
    /// - Independent lifecycle (one Inventory per Part per Warehouse)
    /// - Transactional consistency boundary for stock movements and reservations
    /// - Contains InventoryTransaction entities (child entities within aggregate)
    /// - References Part aggregate by PartId only (cross-aggregate reference)
    /// - Inherits from AggregateRoot for domain event collection
    ///
    /// Why it's a Separate Aggregate from Part:
    /// 1. Warehouse Independence: Each warehouse location operates independently
    /// 2. High Transaction Frequency: Reservations/issues happen constantly
    /// 3. Eventual Consistency: Price updates from Part propagate via domain events
    /// 4. Different Lifecycles: Parts created once; inventory moved frequently
    /// 5. Transaction Isolation: Inventory operations don't lock catalog data
    ///
    /// SEPARATION OF CONCERNS: Operational inventory separate from catalog data
    ///
    /// This aggregate manages:
    /// - Stock levels (on hand, reserved, available)
    /// - Stock movements (receipts, issues, adjustments)
    /// - Location tracking (warehouse, bin)
    /// - Transaction audit trail (InventoryTransaction entities)
    /// - Low stock detection and reorder calculations
    ///
    /// DOES NOT manage catalog data - see Part aggregate
    /// Cross-aggregate communication via domain events (InventoryLowStock, InventoryIssued)
    /// </summary>
    public class Inventory : AggregateRoot
    {
        public Guid InventoryId { get; private set; }

        // Reference to Part catalog (NOT a navigation property - separate aggregate)
        public Guid PartId { get; private set; }

        // Location tracking
        public string Warehouse { get; private set; }
        public string BinLocation { get; private set; }  // Where part is stored (e.g., "A-12-3")

        // Stock quantities
        public int QuantityOnHand { get; private set; }
        public int QuantityReserved { get; private set; }  // Reserved for pending work orders

        // Stock level thresholds (location-specific, can differ from Part defaults)
        public int MinimumStockLevel { get; private set; }
        public int MaximumStockLevel { get; private set; }
        public int ReorderQuantity { get; private set; }

        // Audit
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public bool IsActive { get; private set; }

        // Navigation properties
        public ICollection<InventoryTransaction> Transactions { get; private set; }

        private Inventory()
        {
            Transactions = new List<InventoryTransaction>();
        }

        /// <summary>
        /// Factory Method: Create inventory location for a part
        /// DEMONSTRATES: Inventory created PER warehouse location
        /// </summary>
        public static Inventory Create(
            Guid partId,
            string warehouse,
            string binLocation,
            int minimumStockLevel,
            int maximumStockLevel)
        {
            if (partId == Guid.Empty)
                throw new ArgumentException("PartId is required", nameof(partId));

            if (string.IsNullOrWhiteSpace(warehouse))
                throw new ArgumentException("Warehouse is required", nameof(warehouse));

            if (minimumStockLevel < 0)
                throw new ArgumentException("Minimum stock level cannot be negative", nameof(minimumStockLevel));

            if (maximumStockLevel < minimumStockLevel)
                throw new ArgumentException("Maximum stock level must be >= minimum", nameof(maximumStockLevel));

            var inventory = new Inventory
            {
                InventoryId = Guid.NewGuid(),
                PartId = partId,
                Warehouse = warehouse,
                BinLocation = binLocation,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                MinimumStockLevel = minimumStockLevel,
                MaximumStockLevel = maximumStockLevel,
                ReorderQuantity = maximumStockLevel - minimumStockLevel,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            return inventory;
        }

        /// <summary>
        /// Domain Method: Check if inventory needs reordering
        /// CRITICAL FOR: Automated low-stock alerts (CHALLENGE 2)
        /// TRIGGERS: Notification system to alert purchasing
        /// </summary>
        public bool IsLowStock()
        {
            var availableQuantity = GetAvailableQuantity();
            return availableQuantity <= MinimumStockLevel;
        }

        /// <summary>
        /// Domain Method: Check if inventory is out of stock
        /// CRITICAL FOR: Preventing work order delays
        /// </summary>
        public bool IsOutOfStock()
        {
            return GetAvailableQuantity() <= 0;
        }

        /// <summary>
        /// Domain Method: Calculate available quantity
        /// BUSINESS RULE: Available = OnHand - Reserved
        /// USED BY: Work order assignment logic
        /// </summary>
        public int GetAvailableQuantity()
        {
            return QuantityOnHand - QuantityReserved;
        }

        /// <summary>
        /// Domain Method: Calculate how many to reorder
        /// BUSINESS LOGIC: Bring stock up to maximum level
        /// USED BY: Automated reordering system
        /// </summary>
        public int CalculateReorderQuantity()
        {
            if (!IsLowStock())
                return 0;

            var availableQuantity = GetAvailableQuantity();
            return MaximumStockLevel - availableQuantity;
        }

        /// <summary>
        /// Domain Method: Reserve parts for a work order
        /// BUSINESS RULE: Prevents double-allocation of parts at this location
        /// DEMONSTRATES: Inventory transaction management
        /// </summary>
        public void ReserveParts(int quantity, Guid workOrderId, string requestedBy)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));

            var available = GetAvailableQuantity();
            if (quantity > available)
                throw new InvalidOperationException(
                    $"Cannot reserve {quantity} parts at {Warehouse}. Only {available} available.");

            QuantityReserved += quantity;
            UpdatedAt = DateTime.UtcNow;

            // Record transaction for audit trail
            var transaction = InventoryTransaction.CreateReservation(
                InventoryId, quantity, workOrderId, requestedBy);
            Transactions.Add(transaction);

            // Raise domain event for cross-aggregate communication
            RaiseDomainEvent(new InventoryReserved(
                InventoryId,
                PartId,
                workOrderId,
                Warehouse,
                quantity,
                GetAvailableQuantity()
            ));
        }

        /// <summary>
        /// Domain Method: Release reserved parts back to available
        /// USED WHEN: Work order is cancelled or parts no longer needed
        /// </summary>
        public void ReleaseReservation(int quantity, Guid workOrderId, string releasedBy)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));

            if (quantity > QuantityReserved)
                throw new InvalidOperationException(
                    $"Cannot release {quantity} parts. Only {QuantityReserved} reserved.");

            QuantityReserved -= quantity;
            UpdatedAt = DateTime.UtcNow;

            // Record transaction
            var transaction = InventoryTransaction.CreateRelease(
                InventoryId, quantity, workOrderId, releasedBy);
            Transactions.Add(transaction);
        }

        /// <summary>
        /// Domain Method: Issue reserved parts (take from stock)
        /// BUSINESS RULE: Decreases both OnHand and Reserved
        /// USED WHEN: Technician actually uses the parts
        /// </summary>
        public void IssueParts(int quantity, Guid workOrderId, string issuedBy)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));

            if (quantity > QuantityReserved)
                throw new InvalidOperationException(
                    $"Cannot issue {quantity} parts. Only {QuantityReserved} reserved.");

            if (quantity > QuantityOnHand)
                throw new InvalidOperationException(
                    $"Cannot issue {quantity} parts. Only {QuantityOnHand} on hand.");

            QuantityOnHand -= quantity;
            QuantityReserved -= quantity;
            UpdatedAt = DateTime.UtcNow;

            // Record transaction
            var transaction = InventoryTransaction.CreateIssue(
                InventoryId, quantity, workOrderId, issuedBy);
            Transactions.Add(transaction);

            // Raise domain event for cross-aggregate communication
            RaiseDomainEvent(new InventoryIssued(
                InventoryId,
                PartId,
                workOrderId,
                Warehouse,
                quantity,
                QuantityOnHand
            ));

            // Check if issuing parts triggered low stock condition
            if (IsLowStock())
            {
                RaiseDomainEvent(new InventoryLowStockDetected(
                    InventoryId,
                    PartId,
                    Warehouse,
                    GetAvailableQuantity(),
                    MinimumStockLevel,
                    ReorderQuantity
                ));
            }
        }

        /// <summary>
        /// Domain Method: Receive new parts into inventory
        /// BUSINESS RULE: Increases OnHand quantity
        /// USED WHEN: Parts arrive from supplier
        /// </summary>
        public void ReceiveParts(int quantity, string receivedBy, string referenceNumber = null)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));

            QuantityOnHand += quantity;
            UpdatedAt = DateTime.UtcNow;

            // Record transaction
            var transaction = InventoryTransaction.CreateReceipt(
                InventoryId, quantity, receivedBy, referenceNumber);
            Transactions.Add(transaction);

            // Raise domain event for cross-aggregate communication
            RaiseDomainEvent(new InventoryReceived(
                InventoryId,
                PartId,
                Warehouse,
                quantity,
                QuantityOnHand,
                referenceNumber ?? string.Empty
            ));
        }

        /// <summary>
        /// Domain Method: Adjust inventory for discrepancies
        /// USED FOR: Physical inventory counts and corrections
        /// </summary>
        public void AdjustQuantity(int newQuantity, string reason, string adjustedBy)
        {
            if (newQuantity < 0)
                throw new ArgumentException("Quantity cannot be negative", nameof(newQuantity));

            if (newQuantity < QuantityReserved)
                throw new InvalidOperationException(
                    $"Cannot adjust to {newQuantity}. {QuantityReserved} parts are reserved.");

            var oldQuantity = QuantityOnHand;
            var difference = newQuantity - QuantityOnHand;
            QuantityOnHand = newQuantity;
            UpdatedAt = DateTime.UtcNow;

            // Record adjustment transaction
            var transaction = InventoryTransaction.CreateAdjustment(
                InventoryId, difference, reason, adjustedBy);
            Transactions.Add(transaction);

            // Raise domain event for cross-aggregate communication
            RaiseDomainEvent(new InventoryAdjusted(
                InventoryId,
                PartId,
                Warehouse,
                oldQuantity,
                newQuantity,
                difference,
                reason
            ));

            // Check if adjustment triggered low stock condition
            if (IsLowStock())
            {
                RaiseDomainEvent(new InventoryLowStockDetected(
                    InventoryId,
                    PartId,
                    Warehouse,
                    GetAvailableQuantity(),
                    MinimumStockLevel,
                    ReorderQuantity
                ));
            }
        }

        /// <summary>
        /// Update min/max levels for this location
        /// BUSINESS RULE: Allows dynamic threshold adjustment based on demand patterns
        /// </summary>
        public void UpdateStockLevels(int minimumStockLevel, int maximumStockLevel, int reorderQuantity)
        {
            if (minimumStockLevel < 0)
                throw new ArgumentException("Minimum stock level cannot be negative");

            if (maximumStockLevel < minimumStockLevel)
                throw new ArgumentException("Maximum must be >= minimum");

            if (reorderQuantity < 0)
                throw new ArgumentException("Reorder quantity cannot be negative");

            MinimumStockLevel = minimumStockLevel;
            MaximumStockLevel = maximumStockLevel;
            ReorderQuantity = reorderQuantity;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Move inventory to a different bin location within same warehouse
        /// </summary>
        public void MoveToBinLocation(string newBinLocation, string movedBy)
        {
            if (string.IsNullOrWhiteSpace(newBinLocation))
                throw new ArgumentException("Bin location is required", nameof(newBinLocation));

            var oldLocation = BinLocation;
            BinLocation = newBinLocation;
            UpdatedAt = DateTime.UtcNow;

            // Record transaction for audit
            var transaction = InventoryTransaction.CreateAdjustment(
                InventoryId, 0, $"Moved from {oldLocation} to {newBinLocation}", movedBy);
            Transactions.Add(transaction);
        }

        /// <summary>
        /// Deactivate this inventory location
        /// USED WHEN: Warehouse location is closed or part is no longer stocked here
        /// </summary>
        public void Deactivate()
        {
            if (QuantityOnHand > 0)
                throw new InvalidOperationException(
                    $"Cannot deactivate inventory with {QuantityOnHand} parts on hand. Transfer or adjust to zero first.");

            if (QuantityReserved > 0)
                throw new InvalidOperationException(
                    $"Cannot deactivate inventory with {QuantityReserved} parts reserved.");

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Entity: InventoryTransaction
    /// Audit trail for all inventory movements
    /// CRITICAL FOR: Compliance, troubleshooting, and analytics
    ///
    /// DDD Pattern: ENTITY (within Inventory aggregate)
    /// - Has identity (unique TransactionId)
    /// - Part of Inventory aggregate boundary
    /// - Immutable once created (audit record)
    /// - Created only through Inventory domain methods
    ///
    /// Why it's an Entity (not Aggregate Root):
    /// - No independent lifecycle (created by Inventory aggregate)
    /// - Always accessed through parent Inventory
    /// - Consistency enforced by Inventory aggregate
    /// - Cannot be modified or deleted (audit trail)
    ///
    /// Transaction Types:
    /// - Receipt: Parts received from supplier
    /// - Issue: Parts issued to work order (decreases stock)
    /// - Reservation: Parts reserved for pending work order
    /// - Release: Reserved parts released back to available
    /// - Adjustment: Manual inventory corrections
    /// - Return: Parts returned from completed work order
    /// </summary>
    public class InventoryTransaction
    {
        public Guid TransactionId { get; private set; }
        public Guid InventoryId { get; private set; }
        public InventoryTransactionType TransactionType { get; private set; }
        public int Quantity { get; private set; }  // Positive for receipts, negative for issues
        public Guid? WorkOrderId { get; private set; }
        public string ReferenceNumber { get; private set; }
        public string Notes { get; private set; }
        public DateTime TransactionDate { get; private set; }
        public string TransactionBy { get; private set; }

        private InventoryTransaction() { }

        public static InventoryTransaction CreateReservation(
            Guid inventoryId, int quantity, Guid workOrderId, string requestedBy)
        {
            return new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventoryId,
                TransactionType = InventoryTransactionType.Reservation,
                Quantity = quantity,
                WorkOrderId = workOrderId,
                ReferenceNumber = string.Empty,
                Notes = $"Reserved {quantity} parts for work order",
                TransactionDate = DateTime.UtcNow,
                TransactionBy = requestedBy
            };
        }

        public static InventoryTransaction CreateRelease(
            Guid inventoryId, int quantity, Guid workOrderId, string releasedBy)
        {
            return new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventoryId,
                TransactionType = InventoryTransactionType.Release,
                Quantity = -quantity,  // Negative for releases
                WorkOrderId = workOrderId,
                ReferenceNumber = string.Empty,
                Notes = $"Released {quantity} parts reservation",
                TransactionDate = DateTime.UtcNow,
                TransactionBy = releasedBy
            };
        }

        public static InventoryTransaction CreateIssue(
            Guid inventoryId, int quantity, Guid workOrderId, string issuedBy)
        {
            return new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventoryId,
                TransactionType = InventoryTransactionType.Issue,
                Quantity = -quantity,  // Negative for decreases
                WorkOrderId = workOrderId,
                ReferenceNumber = string.Empty,
                Notes = $"Issued {quantity} parts to work order",
                TransactionDate = DateTime.UtcNow,
                TransactionBy = issuedBy
            };
        }

        public static InventoryTransaction CreateReceipt(
            Guid inventoryId, int quantity, string receivedBy, string referenceNumber)
        {
            return new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventoryId,
                TransactionType = InventoryTransactionType.Receipt,
                Quantity = quantity,
                ReferenceNumber = referenceNumber ?? string.Empty,
                Notes = $"Received {quantity} parts",
                TransactionDate = DateTime.UtcNow,
                TransactionBy = receivedBy
            };
        }

        public static InventoryTransaction CreateAdjustment(
            Guid inventoryId, int quantityDifference, string reason, string adjustedBy)
        {
            return new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventoryId,
                TransactionType = InventoryTransactionType.Adjustment,
                Quantity = quantityDifference,
                ReferenceNumber = string.Empty,
                Notes = reason,
                TransactionDate = DateTime.UtcNow,
                TransactionBy = adjustedBy
            };
        }
    }

    public enum InventoryTransactionType
    {
        Receipt,      // Parts received from supplier
        Issue,        // Parts issued to work order
        Reservation,  // Parts reserved for work order
        Release,      // Reserved parts released back to available
        Adjustment,   // Inventory count adjustment
        Return        // Parts returned from work order
    }
}
