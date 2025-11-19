using System;

namespace HeavyIMS.Domain.Events
{
    /// <summary>
    /// Event: Inventory fell below minimum stock level
    /// SUBSCRIBERS: Purchasing system (trigger reorder), notification service (alert buyers)
    /// CRITICAL FOR: Challenge 2 - Parts delays prevention
    /// </summary>
    public class InventoryLowStockDetected : DomainEvent
    {
        public Guid InventoryId { get; }
        public Guid PartId { get; }
        public string Warehouse { get; }
        public int CurrentQuantity { get; }
        public int MinimumStockLevel { get; }
        public int ReorderQuantity { get; }

        public InventoryLowStockDetected(
            Guid inventoryId,
            Guid partId,
            string warehouse,
            int currentQuantity,
            int minimumStockLevel,
            int reorderQuantity)
        {
            InventoryId = inventoryId;
            PartId = partId;
            Warehouse = warehouse;
            CurrentQuantity = currentQuantity;
            MinimumStockLevel = minimumStockLevel;
            ReorderQuantity = reorderQuantity;
        }
    }

    /// <summary>
    /// Event: Parts were reserved for a work order
    /// SUBSCRIBERS: Work order system (update status), analytics (track reservations)
    /// </summary>
    public class InventoryReserved : DomainEvent
    {
        public Guid InventoryId { get; }
        public Guid PartId { get; }
        public Guid WorkOrderId { get; }
        public string Warehouse { get; }
        public int QuantityReserved { get; }
        public int RemainingAvailable { get; }

        public InventoryReserved(
            Guid inventoryId,
            Guid partId,
            Guid workOrderId,
            string warehouse,
            int quantityReserved,
            int remainingAvailable)
        {
            InventoryId = inventoryId;
            PartId = partId;
            WorkOrderId = workOrderId;
            Warehouse = warehouse;
            QuantityReserved = quantityReserved;
            RemainingAvailable = remainingAvailable;
        }
    }

    /// <summary>
    /// Event: Parts were issued (physically taken from inventory)
    /// SUBSCRIBERS: Costing system (update work order costs), low stock checker
    /// </summary>
    public class InventoryIssued : DomainEvent
    {
        public Guid InventoryId { get; }
        public Guid PartId { get; }
        public Guid WorkOrderId { get; }
        public string Warehouse { get; }
        public int QuantityIssued { get; }
        public int RemainingOnHand { get; }

        public InventoryIssued(
            Guid inventoryId,
            Guid partId,
            Guid workOrderId,
            string warehouse,
            int quantityIssued,
            int remainingOnHand)
        {
            InventoryId = inventoryId;
            PartId = partId;
            WorkOrderId = workOrderId;
            Warehouse = warehouse;
            QuantityIssued = quantityIssued;
            RemainingOnHand = remainingOnHand;
        }
    }

    /// <summary>
    /// Event: Parts were received into inventory
    /// SUBSCRIBERS: Purchasing system (mark PO received), analytics (track lead times)
    /// </summary>
    public class InventoryReceived : DomainEvent
    {
        public Guid InventoryId { get; }
        public Guid PartId { get; }
        public string Warehouse { get; }
        public int QuantityReceived { get; }
        public int NewQuantityOnHand { get; }
        public string ReferenceNumber { get; }

        public InventoryReceived(
            Guid inventoryId,
            Guid partId,
            string warehouse,
            int quantityReceived,
            int newQuantityOnHand,
            string referenceNumber)
        {
            InventoryId = inventoryId;
            PartId = partId;
            Warehouse = warehouse;
            QuantityReceived = quantityReceived;
            NewQuantityOnHand = newQuantityOnHand;
            ReferenceNumber = referenceNumber;
        }
    }

    /// <summary>
    /// Event: Inventory quantity was adjusted (cycle count, correction)
    /// SUBSCRIBERS: Audit system (track discrepancies), analytics (inventory accuracy metrics)
    /// </summary>
    public class InventoryAdjusted : DomainEvent
    {
        public Guid InventoryId { get; }
        public Guid PartId { get; }
        public string Warehouse { get; }
        public int OldQuantity { get; }
        public int NewQuantity { get; }
        public int Difference { get; }
        public string Reason { get; }

        public InventoryAdjusted(
            Guid inventoryId,
            Guid partId,
            string warehouse,
            int oldQuantity,
            int newQuantity,
            int difference,
            string reason)
        {
            InventoryId = inventoryId;
            PartId = partId;
            Warehouse = warehouse;
            OldQuantity = oldQuantity;
            NewQuantity = newQuantity;
            Difference = difference;
            Reason = reason;
        }
    }
}
