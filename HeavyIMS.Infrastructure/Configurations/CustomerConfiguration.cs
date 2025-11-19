using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HeavyIMS.Domain.Entities;

namespace HeavyIMS.Infrastructure.Configurations
{
    /// <summary>
    /// Entity Framework Fluent API Configuration for Customer
    /// DEMONSTRATES: Entity Framework configuration with Value Objects
    ///
    /// WHY SEPARATE CONFIGURATION CLASSES?
    /// - Keeps DbContext clean
    /// - Better organization (Single Responsibility Principle)
    /// - Easier to maintain and test
    /// </summary>
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            // Table name
            builder.ToTable("Customers");

            // Primary key
            builder.HasKey(c => c.Id);

            // Properties configuration
            builder.Property(c => c.CompanyName)
                .IsRequired()              // NOT NULL constraint
                .HasMaxLength(200);        // VARCHAR(200)

            builder.Property(c => c.ContactName)
                .HasMaxLength(100);

            builder.Property(c => c.Email)
                .IsRequired()
                .HasMaxLength(255);

            // UNIQUE constraint on email
            builder.HasIndex(c => c.Email)
                .IsUnique()
                .HasDatabaseName("IX_Customers_Email");

            builder.Property(c => c.PhoneNumber)
                .HasMaxLength(20);

            // VALUE OBJECT: Address
            // Maps Address value object to separate columns
            builder.OwnsOne(c => c.Address, address =>
            {
                address.Property(a => a.Street)
                    .HasColumnName("Address_Street")
                    .IsRequired()
                    .HasMaxLength(200);

                address.Property(a => a.Street2)
                    .HasColumnName("Address_Street2")
                    .HasMaxLength(200);

                address.Property(a => a.City)
                    .HasColumnName("Address_City")
                    .IsRequired()
                    .HasMaxLength(100);

                address.Property(a => a.State)
                    .HasColumnName("Address_State")
                    .IsRequired()
                    .HasMaxLength(2);  // US state abbreviation

                address.Property(a => a.ZipCode)
                    .HasColumnName("Address_ZipCode")
                    .IsRequired()
                    .HasMaxLength(10);  // Supports ZIP+4 format

                address.Property(a => a.Country)
                    .HasColumnName("Address_Country")
                    .HasMaxLength(3)    // ISO 3166-1 alpha-3
                    .HasDefaultValue("USA")
                    .IsRequired();
            });

            // Communication preferences
            builder.Property(c => c.PreferEmailNotifications)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(c => c.PreferSMSNotifications)
                .IsRequired()
                .HasDefaultValue(true);

            // Audit fields
            builder.Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");  // SQL Server default

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // NOTE: Relationships configured from WorkOrder side (DDD: no navigation properties)
            // WorkOrder references Customer by ID only (WorkOrderConfiguration.cs)

            // PERFORMANCE: Index on frequently queried columns
            builder.HasIndex(c => c.IsActive)
                .HasDatabaseName("IX_Customers_IsActive");

            builder.HasIndex(c => c.CompanyName)
                .HasDatabaseName("IX_Customers_CompanyName");
        }
    }
}
