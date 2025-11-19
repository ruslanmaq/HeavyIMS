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
    /// Repository Implementation: Part (Catalog Aggregate)
    /// DEMONSTRATES: Specialized queries for catalog management
    /// </summary>
    public class PartRepository : Repository<Part>, IPartRepository
    {
        public PartRepository(HeavyIMSDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Part>> SearchPartsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAsync();

            var lowerSearchTerm = searchTerm.ToLower();

            return await _context.Set<Part>()
                .Where(p =>
                    p.PartNumber.ToLower().Contains(lowerSearchTerm) ||
                    p.PartName.ToLower().Contains(lowerSearchTerm) ||
                    p.Description.ToLower().Contains(lowerSearchTerm))
                .OrderBy(p => p.PartNumber)
                .ToListAsync();
        }

        public async Task<Part> GetByPartNumberAsync(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                return null;

            return await _context.Set<Part>()
                .FirstOrDefaultAsync(p => p.PartNumber == partNumber);
        }

        public async Task<IEnumerable<Part>> GetPartsByCategoryAsync(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return await GetAllAsync();

            return await _context.Set<Part>()
                .Where(p => p.Category == category)
                .OrderBy(p => p.PartNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Part>> GetActivePartsAsync()
        {
            return await _context.Set<Part>()
                .Where(p => p.IsActive && !p.IsDiscontinued)
                .OrderBy(p => p.PartNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Part>> GetDiscontinuedPartsAsync()
        {
            return await _context.Set<Part>()
                .Where(p => p.IsDiscontinued)
                .OrderBy(p => p.PartNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Part>> GetPartsBySupplierAsync(Guid supplierId)
        {
            return await _context.Set<Part>()
                .Where(p => p.SupplierId == supplierId)
                .OrderBy(p => p.PartNumber)
                .ToListAsync();
        }
    }
}
