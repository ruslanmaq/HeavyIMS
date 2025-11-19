using System;

namespace HeavyIMS.Domain.Events
{
    /// <summary>
    /// Event: Part price was updated in catalog
    /// SUBSCRIBERS: Reporting systems, pricing analysis, inventory valuation
    /// </summary>
    public class PartPriceUpdated : DomainEvent
    {
        public Guid PartId { get; }
        public decimal OldUnitCost { get; }
        public decimal NewUnitCost { get; }
        public decimal OldUnitPrice { get; }
        public decimal NewUnitPrice { get; }

        public PartPriceUpdated(Guid partId, decimal oldUnitCost, decimal newUnitCost, decimal oldUnitPrice, decimal newUnitPrice)
        {
            PartId = partId;
            OldUnitCost = oldUnitCost;
            NewUnitCost = newUnitCost;
            OldUnitPrice = oldUnitPrice;
            NewUnitPrice = newUnitPrice;
        }
    }

    /// <summary>
    /// Event: Part was discontinued in catalog
    /// SUBSCRIBERS: Inventory management (stop reordering), work order system (warn on usage)
    /// </summary>
    public class PartDiscontinued : DomainEvent
    {
        public Guid PartId { get; }
        public string PartNumber { get; }

        public PartDiscontinued(Guid partId, string partNumber)
        {
            PartId = partId;
            PartNumber = partNumber;
        }
    }

    /// <summary>
    /// Event: New part was added to catalog
    /// SUBSCRIBERS: Inventory system (optionally create inventory locations)
    /// </summary>
    public class PartCreated : DomainEvent
    {
        public Guid PartId { get; }
        public string PartNumber { get; }
        public string PartName { get; }
        public string Category { get; }

        public PartCreated(Guid partId, string partNumber, string partName, string category)
        {
            PartId = partId;
            PartNumber = partNumber;
            PartName = partName;
            Category = category;
        }
    }
}
