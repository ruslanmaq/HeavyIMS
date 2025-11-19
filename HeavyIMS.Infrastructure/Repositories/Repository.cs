using Microsoft.EntityFrameworkCore;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace HeavyIMS.Infrastructure.Repositories
{
    /// <summary>
    /// Generic Repository Implementation
    /// DEMONSTRATES:
    /// - Repository Pattern implementation
    /// - Entity Framework Core usage
    /// - LINQ queries
    /// - Async/Await pattern
    ///
    /// WHY GENERIC?
    /// - Code reuse across all entities
    /// - DRY principle (Don't Repeat Yourself)
    /// - Consistent data access patterns
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly HeavyIMSDbContext _context;
        protected readonly DbSet<T> _dbSet;

        /// <summary>
        /// Constructor - Dependency Injection of DbContext
        /// DEMONSTRATES: Constructor Injection (DI pattern)
        /// </summary>
        public Repository(HeavyIMSDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
        }

        /// <summary>
        /// Get entity by ID
        /// DEMONSTRATES: Async programming with EF Core
        /// </summary>
        public virtual async Task<T> GetByIdAsync(Guid id)
        {
            // FindAsync is optimized for primary key lookups
            return await _dbSet.FindAsync(id);
        }

        /// <summary>
        /// Get all entities
        /// WARNING: Use with caution on large tables
        /// BETTER APPROACH: Use pagination (Skip/Take)
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            // ToListAsync() executes the query and returns results
            // AsNoTracking() improves performance for read-only queries
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        /// <summary>
        /// Find entities matching a predicate
        /// DEMONSTRATES: LINQ with Expression trees
        /// EXAMPLE USAGE: await repository.FindAsync(x => x.IsActive == true)
        /// </summary>
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            // Where() filters using the predicate
            // AsNoTracking() - read-only, faster performance
            return await _dbSet
                .Where(predicate)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get first entity matching predicate, or null
        /// DEMONSTRATES: LINQ FirstOrDefault with async
        /// </summary>
        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet
                .Where(predicate)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Add a new entity
        /// NOTE: Changes are not saved until SaveChangesAsync() is called
        /// </summary>
        public virtual async Task<T> AddAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // AddAsync() marks entity as Added in change tracker
            await _dbSet.AddAsync(entity);
            return entity;
        }

        /// <summary>
        /// Add multiple entities efficiently
        /// PERFORMANCE: Better than calling AddAsync() in a loop
        /// </summary>
        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            await _dbSet.AddRangeAsync(entities);
        }

        /// <summary>
        /// Update an entity
        /// NOTE: Entity must be tracked by context
        /// </summary>
        public virtual void Update(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Attach entity if not tracked, then mark as Modified
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        /// <summary>
        /// Delete an entity
        /// </summary>
        public virtual void Remove(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _dbSet.Remove(entity);
        }

        /// <summary>
        /// Delete multiple entities efficiently
        /// </summary>
        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            _dbSet.RemoveRange(entities);
        }

        /// <summary>
        /// Count entities matching predicate
        /// DEMONSTRATES: LINQ Count with optional filter
        /// </summary>
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            if (predicate == null)
                return await _dbSet.CountAsync();

            return await _dbSet.CountAsync(predicate);
        }

        /// <summary>
        /// Check if any entity exists matching predicate
        /// PERFORMANCE: More efficient than Count() > 0
        /// </summary>
        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }
    }
}
