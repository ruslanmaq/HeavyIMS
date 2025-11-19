using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HeavyIMS.Infrastructure.Repositories
{
    /// <summary>
    /// Repository Implementation: Inventory (Operational Aggregate)
    /// DEMONSTRATES: Multi-warehouse inventory queries
    /// ADDRESSES CHALLENGE 2: Real-time inventory tracking
    /// </summary>
    public class InventoryRepository : Repository<Inventory>, IInventoryRepository
    {
        public InventoryRepository(HeavyIMSDbContext context) : base(context)
        {
        }

        /// <summary>
        /// Override GetByIdAsync to include Transactions collection
        /// This is needed because the Inventory aggregate modifies transactions when methods like
        /// ReserveParts, IssueParts, etc. are called
        /// </summary>
        public override async Task<Inventory> GetByIdAsync(Guid id)
        {
            return await _context.Set<Inventory>()
                .Include(i => i.Transactions)
                .FirstOrDefaultAsync(i => i.InventoryId == id);
        }

        public async Task<IEnumerable<Inventory>> GetByPartIdAsync(Guid partId)
        {
            return await _context.Set<Inventory>()
                .Where(i => i.PartId == partId && i.IsActive)
                .OrderBy(i => i.Warehouse)
                .ToListAsync();
        }

        public async Task<Inventory> GetByPartAndWarehouseAsync(Guid partId, string warehouse)
        {
            if (string.IsNullOrWhiteSpace(warehouse))
                return null;

            return await _context.Set<Inventory>()
                .FirstOrDefaultAsync(i =>
                    i.PartId == partId &&
                    i.Warehouse == warehouse &&
                    i.IsActive);
        }

        public async Task<IEnumerable<Inventory>> GetByWarehouseAsync(string warehouse)
        {
            if (string.IsNullOrWhiteSpace(warehouse))
                return await GetAllAsync();

            return await _context.Set<Inventory>()
                .Where(i => i.Warehouse == warehouse && i.IsActive)
                .OrderBy(i => i.BinLocation)
                .ToListAsync();
        }

        public async Task<IEnumerable<Inventory>> GetLowStockInventoryAsync()
        {
            return await _context.Set<Inventory>()
                .Where(i =>
                    i.IsActive &&
                    (i.QuantityOnHand - i.QuantityReserved) <= i.MinimumStockLevel)
                .OrderBy(i => i.Warehouse)
                .ThenBy(i => i.PartId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Inventory>> GetOutOfStockInventoryAsync()
        {
            return await _context.Set<Inventory>()
                .Where(i =>
                    i.IsActive &&
                    (i.QuantityOnHand - i.QuantityReserved) <= 0)
                .OrderBy(i => i.Warehouse)
                .ThenBy(i => i.PartId)
                .ToListAsync();
        }

        public async Task<Inventory> GetInventoryWithTransactionsAsync(Guid inventoryId)
        {
            return await _context.Set<Inventory>()
                .Include(i => i.Transactions)
                .FirstOrDefaultAsync(i => i.InventoryId == inventoryId);
        }

        public async Task<bool> IsQuantityAvailableAsync(Guid partId, string warehouse, int requiredQuantity)
        {
            var inventory = await GetByPartAndWarehouseAsync(partId, warehouse);

            if (inventory == null)
                return false;

            var available = inventory.GetAvailableQuantity();
            return available >= requiredQuantity;
        }

        public async Task<int> GetTotalQuantityOnHandAsync(Guid partId)
        {
            var inventories = await GetByPartIdAsync(partId);
            return inventories.Sum(i => i.QuantityOnHand);
        }

        public async Task<int> GetTotalAvailableQuantityAsync(Guid partId)
        {
            var inventories = await GetByPartIdAsync(partId);
            return inventories.Sum(i => i.GetAvailableQuantity());
        }
    }
}
