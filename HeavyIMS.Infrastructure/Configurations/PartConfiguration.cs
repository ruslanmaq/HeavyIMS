using HeavyIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeavyIMS.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration: Part entity (Catalog Aggregate)
    /// DEMONSTRATES: Fluent API for entity configuration
    /// </summary>
    public class PartConfiguration : IEntityTypeConfiguration<Part>
    {
        public void Configure(EntityTypeBuilder<Part> builder)
        {
            // Table name
            builder.ToTable("Parts");

            // Primary key
            builder.HasKey(p => p.PartId);

            // Properties
            builder.Property(p => p.PartNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.PartName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.Description)
                .HasMaxLength(1000);

            builder.Property(p => p.Category)
                .HasMaxLength(100);

            // VALUE OBJECT: UnitCost (Money)
            // Maps to existing columns for backwards compatibility
            builder.OwnsOne(p => p.UnitCost, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("UnitCost")
                    .HasPrecision(18, 2);

                money.Property(m => m.Currency)
                    .HasColumnName("UnitCost_Currency")
                    .HasMaxLength(3)
                    .HasDefaultValue("USD")
                    .IsRequired();
            });

            // VALUE OBJECT: UnitPrice (Money)
            builder.OwnsOne(p => p.UnitPrice, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("UnitPrice")
                    .HasPrecision(18, 2);

                money.Property(m => m.Currency)
                    .HasColumnName("UnitPrice_Currency")
                    .HasMaxLength(3)
                    .HasDefaultValue("USD")
                    .IsRequired();
            });

            builder.Property(p => p.SupplierPartNumber)
                .HasMaxLength(50);

            builder.Property(p => p.CreatedAt)
                .IsRequired();

            // Indexes
            builder.HasIndex(p => p.PartNumber)
                .IsUnique()
                .HasDatabaseName("IX_Parts_PartNumber");

            builder.HasIndex(p => p.Category)
                .HasDatabaseName("IX_Parts_Category");

            builder.HasIndex(p => p.SupplierId)
                .HasDatabaseName("IX_Parts_SupplierId");

            builder.HasIndex(p => p.IsActive)
                .HasDatabaseName("IX_Parts_IsActive");

            // Seed data (optional - example parts)
            // builder.HasData(
            //     Part.Create("HYD-001", "Hydraulic Pump", "Main hydraulic pump for excavator", "Hydraulics", 1200.00m, 1800.00m)
            // );
        }
    }
}
