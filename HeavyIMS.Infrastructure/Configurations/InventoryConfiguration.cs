using HeavyIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeavyIMS.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration: Inventory entity (Operational Aggregate)
    /// DEMONSTRATES: Multi-warehouse inventory tracking configuration
    /// </summary>
    public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
    {
        public void Configure(EntityTypeBuilder<Inventory> builder)
        {
            // Table name
            builder.ToTable("Inventory");

            // Primary key
            builder.HasKey(i => i.InventoryId);

            // Properties
            builder.Property(i => i.PartId)
                .IsRequired();

            builder.Property(i => i.Warehouse)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(i => i.BinLocation)
                .HasMaxLength(50);

            builder.Property(i => i.QuantityOnHand)
                .IsRequired();

            builder.Property(i => i.QuantityReserved)
                .IsRequired();

            builder.Property(i => i.CreatedAt)
                .IsRequired();

            // Indexes
            // CRITICAL: Unique constraint on (PartId, Warehouse) - one inventory record per part per warehouse
            builder.HasIndex(i => new { i.PartId, i.Warehouse })
                .IsUnique()
                .HasDatabaseName("IX_Inventory_PartId_Warehouse");

            builder.HasIndex(i => i.Warehouse)
                .HasDatabaseName("IX_Inventory_Warehouse");

            builder.HasIndex(i => i.PartId)
                .HasDatabaseName("IX_Inventory_PartId");

            builder.HasIndex(i => i.IsActive)
                .HasDatabaseName("IX_Inventory_IsActive");

            // Relationships
            // NOTE: No navigation property to Part - separate aggregates
            // PartId is just a reference (Guid), not a foreign key with cascade
            // This is intentional for DDD aggregate separation

            // Owned collection: InventoryTransactions
            builder.HasMany(i => i.Transactions)
                .WithOne()
                .HasForeignKey(t => t.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>
    /// EF Core Configuration: InventoryTransaction entity
    /// DEMONSTRATES: Audit trail configuration
    /// </summary>
    public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
    {
        public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
        {
            // Table name
            builder.ToTable("InventoryTransactions");

            // Primary key
            builder.HasKey(t => t.TransactionId);

            // Properties
            builder.Property(t => t.InventoryId)
                .IsRequired();

            builder.Property(t => t.TransactionType)
                .IsRequired()
                .HasConversion<string>() // Store enum as string in database
                .HasMaxLength(20);

            builder.Property(t => t.Quantity)
                .IsRequired();

            builder.Property(t => t.ReferenceNumber)
                .HasMaxLength(100);

            builder.Property(t => t.Notes)
                .HasMaxLength(1000);

            builder.Property(t => t.TransactionDate)
                .IsRequired();

            builder.Property(t => t.TransactionBy)
                .IsRequired()
                .HasMaxLength(200);

            // Indexes
            builder.HasIndex(t => t.InventoryId)
                .HasDatabaseName("IX_InventoryTransactions_InventoryId");

            builder.HasIndex(t => t.WorkOrderId)
                .HasDatabaseName("IX_InventoryTransactions_WorkOrderId");

            builder.HasIndex(t => t.TransactionDate)
                .HasDatabaseName("IX_InventoryTransactions_TransactionDate");

            builder.HasIndex(t => t.TransactionType)
                .HasDatabaseName("IX_InventoryTransactions_TransactionType");
        }
    }
}
