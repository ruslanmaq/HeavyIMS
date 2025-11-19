using Microsoft.EntityFrameworkCore;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HeavyIMS.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework Core DbContext
    /// CENTRAL DATABASE ACCESS POINT
    ///
    /// KEY CONCEPTS:
    /// - DbSet<T>: Represents a table in the database
    /// - DbContext: Manages database connection and tracks changes
    /// - SaveChanges(): Persists all tracked changes to database
    ///
    /// DEMONSTRATES:
    /// - Entity Framework Core usage
    /// - Database context management
    /// - Audit trail automation (CreatedAt, UpdatedAt)
    /// </summary>
    public class HeavyIMSDbContext : DbContext
    {
        public HeavyIMSDbContext(DbContextOptions<HeavyIMSDbContext> options)
            : base(options)
        {
        }

        // DbSets represent tables in the database
        public DbSet<Technician> Technicians { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<WorkOrderPart> WorkOrderParts { get; set; }
        public DbSet<WorkOrderNotification> WorkOrderNotifications { get; set; }

        // Separate aggregates (DDD best practices)
        public DbSet<Part> Parts { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        /// <summary>
        /// Configure entity mappings using Fluent API
        /// DEMONSTRATES: Entity Framework configuration
        /// WHY FLUENT API? More control than data annotations
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ignore domain events - they are transient and should not be persisted
            // Domain events are collected in-memory and dispatched after SaveChanges
            modelBuilder.Ignore<DomainEvent>();

            // Part events
            modelBuilder.Ignore<PartCreated>();
            modelBuilder.Ignore<PartPriceUpdated>();
            modelBuilder.Ignore<PartDiscontinued>();

            // Inventory events
            modelBuilder.Ignore<InventoryLowStockDetected>();
            modelBuilder.Ignore<InventoryReserved>();
            modelBuilder.Ignore<InventoryIssued>();
            modelBuilder.Ignore<InventoryReceived>();
            modelBuilder.Ignore<InventoryAdjusted>();

            // Apply all configurations from separate configuration classes
            // DEMONSTRATES: Separation of concerns
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeavyIMSDbContext).Assembly);

            // Alternative: Apply individual configurations
            // modelBuilder.ApplyConfiguration(new TechnicianConfiguration());
            // modelBuilder.ApplyConfiguration(new WorkOrderConfiguration());
        }

        /// <summary>
        /// Override SaveChanges to add automatic audit timestamps
        /// DEMONSTRATES: Intercepting save operations for cross-cutting concerns
        /// BENEFITS:
        /// - Automatic CreatedAt/UpdatedAt tracking
        /// - No need to manually set timestamps in business logic
        /// - Consistent audit trail across all entities
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Get all entities that are being added or modified
            var entries = ChangeTracker.Entries();

            foreach (var entry in entries)
            {
                // Handle entities being added
                if (entry.State == EntityState.Added)
                {
                    // Use reflection to set CreatedAt if property exists
                    var createdAtProperty = entry.Entity.GetType().GetProperty("CreatedAt");
                    if (createdAtProperty != null && createdAtProperty.PropertyType == typeof(DateTime))
                    {
                        createdAtProperty.SetValue(entry.Entity, DateTime.UtcNow);
                    }
                }

                // Handle entities being modified
                if (entry.State == EntityState.Modified)
                {
                    // Use reflection to set UpdatedAt if property exists
                    var updatedAtProperty = entry.Entity.GetType().GetProperty("UpdatedAt");
                    if (updatedAtProperty != null && updatedAtProperty.PropertyType == typeof(DateTime?))
                    {
                        updatedAtProperty.SetValue(entry.Entity, DateTime.UtcNow);
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Configure database connection settings
        /// USED IN: Development environment with sensitive data
        /// NOTE: In production, use Configuration Management (appsettings.json)
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // This is typically done in Startup.cs/Program.cs using DI
            // Shown here for educational purposes

            if (!optionsBuilder.IsConfigured)
            {
                // Azure SQL Database connection string example
                // NEVER hardcode in production - use Azure Key Vault or Configuration
                // optionsBuilder.UseSqlServer(
                //     "Server=tcp:heavyims.database.windows.net,1433;" +
                //     "Initial Catalog=HeavyIMSDb;" +
                //     "Persist Security Info=False;" +
                //     "User ID=adminuser;" +
                //     "Password=YourPassword123!;" +
                //     "MultipleActiveResultSets=False;" +
                //     "Encrypt=True;" +
                //     "TrustServerCertificate=False;" +
                //     "Connection Timeout=30;");

                // For local development with SQL Server LocalDB
                optionsBuilder.UseSqlServer(
                    "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;");
            }

            // Enable sensitive data logging in Development only
            // IMPORTANT: Disable in Production for security
            #if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
            #endif
        }
    }
}
