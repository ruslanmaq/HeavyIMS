using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HeavyIMS.Domain.Entities;

namespace HeavyIMS.Infrastructure.Configurations
{
    /// <summary>
    /// Entity Framework Configuration for WorkOrder
    /// DEMONSTRATES: Complex entity configuration with relationships
    /// </summary>
    public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
    {
        public void Configure(EntityTypeBuilder<WorkOrder> builder)
        {
            builder.ToTable("WorkOrders");

            // Primary key
            builder.HasKey(wo => wo.Id);

            // Business key - Work Order Number
            builder.Property(wo => wo.WorkOrderNumber)
                .IsRequired()
                .HasMaxLength(50);

            // UNIQUE constraint on work order number
            builder.HasIndex(wo => wo.WorkOrderNumber)
                .IsUnique()
                .HasDatabaseName("IX_WorkOrders_WorkOrderNumber_Unique");

            // VALUE OBJECT: Equipment (EquipmentIdentifier)
            // Maps to existing columns for backwards compatibility
            builder.OwnsOne(wo => wo.Equipment, equipment =>
            {
                equipment.Property(e => e.VIN)
                    .HasColumnName("EquipmentVIN")
                    .IsRequired()
                    .HasMaxLength(17);  // Standard VIN length

                equipment.Property(e => e.Type)
                    .HasColumnName("EquipmentType")
                    .HasMaxLength(100);

                equipment.Property(e => e.Model)
                    .HasColumnName("EquipmentModel")
                    .HasMaxLength(100);
            });

            // Job details
            builder.Property(wo => wo.Description)
                .IsRequired()
                .HasMaxLength(2000);  // Longer text field

            builder.Property(wo => wo.DiagnosticNotes)
                .HasMaxLength(4000)
                .IsRequired(false);   // Optional field

            // Enums
            builder.Property(wo => wo.Priority)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(wo => wo.Status)
                .IsRequired()
                .HasConversion<int>();

            // Decimal properties - specify precision for money/hours
            // CRITICAL: Always specify precision for decimal in SQL Server
            builder.Property(wo => wo.EstimatedLaborHours)
                .HasPrecision(8, 2)   // 999999.99 max
                .HasDefaultValue(0);

            builder.Property(wo => wo.ActualLaborHours)
                .HasPrecision(8, 2)
                .HasDefaultValue(0);

            // VALUE OBJECT: EstimatedCost (Money)
            builder.OwnsOne(wo => wo.EstimatedCost, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("EstimatedCost")
                    .HasPrecision(18, 2)
                    .HasDefaultValue(0);

                money.Property(m => m.Currency)
                    .HasColumnName("EstimatedCost_Currency")
                    .HasMaxLength(3)
                    .HasDefaultValue("USD")
                    .IsRequired();
            });

            // VALUE OBJECT: ActualCost (Money)
            builder.OwnsOne(wo => wo.ActualCost, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("ActualCost")
                    .HasPrecision(18, 2)
                    .HasDefaultValue(0);

                money.Property(m => m.Currency)
                    .HasColumnName("ActualCost_Currency")
                    .HasMaxLength(3)
                    .HasDefaultValue("USD")
                    .IsRequired();
            });

            // VALUE OBJECT: ScheduledPeriod (DateRange) - Optional
            // Navigation is nullable (DateRange? ScheduledPeriod)
            builder.OwnsOne(wo => wo.ScheduledPeriod, period =>
            {
                period.Property(p => p.Start)
                    .HasColumnName("ScheduledStartDate");

                period.Property(p => p.End)
                    .HasColumnName("ScheduledEndDate");
            });
            builder.Navigation(wo => wo.ScheduledPeriod).IsRequired(false);

            // VALUE OBJECT: ActualPeriod (DateRange) - Optional
            // Navigation is nullable (DateRange? ActualPeriod)
            builder.OwnsOne(wo => wo.ActualPeriod, period =>
            {
                period.Property(p => p.Start)
                    .HasColumnName("ActualStartDate");

                period.Property(p => p.End)
                    .HasColumnName("ActualEndDate");
            });
            builder.Navigation(wo => wo.ActualPeriod).IsRequired(false);

            // Audit fields
            builder.Property(wo => wo.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(wo => wo.UpdatedAt)
                .IsRequired(false);

            builder.Property(wo => wo.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            // RELATIONSHIPS (DDD: No navigation properties between aggregates)

            // Foreign Key: WorkOrder -> Customer (ID reference only)
            builder.HasOne<Customer>()  // No navigation property
                .WithMany()              // No navigation property
                .HasForeignKey(wo => wo.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);  // Prevent customer deletion if they have work orders
                // BUSINESS RULE: Cannot delete customer with active work orders

            // Foreign Key: WorkOrder -> Technician (nullable, ID reference only)
            builder.HasOne<Technician>()  // No navigation property
                .WithMany()               // No navigation property
                .HasForeignKey(wo => wo.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.SetNull)  // If technician deleted, work order becomes unassigned
                .IsRequired(false);

            // One-to-Many: WorkOrder -> WorkOrderParts
            builder.HasMany(wo => wo.RequiredParts)
                .WithOne()
                .HasForeignKey(wop => wop.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);  // Delete parts list if work order deleted

            // One-to-Many: WorkOrder -> WorkOrderNotifications
            builder.HasMany(wo => wo.Notifications)
                .WithOne()
                .HasForeignKey(won => won.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // PERFORMANCE INDEXES
            // Index on foreign keys (EF Core creates these automatically, but explicit is clearer)
            builder.HasIndex(wo => wo.CustomerId)
                .HasDatabaseName("IX_WorkOrders_CustomerId");

            builder.HasIndex(wo => wo.AssignedTechnicianId)
                .HasDatabaseName("IX_WorkOrders_AssignedTechnicianId");

            // Index on status for filtering
            builder.HasIndex(wo => wo.Status)
                .HasDatabaseName("IX_WorkOrders_Status");

            // NOTE: Indexes on owned entity properties (value objects) need special handling
            // EF Core doesn't support direct indexing on owned types in HasIndex()
            // These indexes are created via the column names in the migration instead

            // Index on VIN for searching (via column name in database)
            // CREATE INDEX IX_WorkOrders_EquipmentVIN ON WorkOrders(EquipmentVIN)
            // This is handled in the migration or can be done with raw SQL

            // Composite index for common query: Get active work orders by status and date
            // CREATE INDEX IX_WorkOrders_Status_ScheduledDate ON WorkOrders(Status, ScheduledStartDate)
            // This is handled in the migration or can be done with raw SQL

            // Index on created date for reporting
            builder.HasIndex(wo => wo.CreatedAt)
                .HasDatabaseName("IX_WorkOrders_CreatedAt");

            // QUERY FILTER: Soft delete pattern (if implemented)
            // Uncomment if using soft delete instead of hard delete
            // builder.HasQueryFilter(wo => wo.IsDeleted == false);
        }
    }
}
