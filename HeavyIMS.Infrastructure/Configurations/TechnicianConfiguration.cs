using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HeavyIMS.Domain.Entities;

namespace HeavyIMS.Infrastructure.Configurations
{
    /// <summary>
    /// Entity Framework Fluent API Configuration for Technician
    /// DEMONSTRATES: Entity Framework configuration best practices
    ///
    /// WHY SEPARATE CONFIGURATION CLASSES?
    /// - Keeps DbContext clean
    /// - Better organization (Single Responsibility Principle)
    /// - Easier to maintain and test
    /// </summary>
    public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
    {
        public void Configure(EntityTypeBuilder<Technician> builder)
        {
            // Table name
            builder.ToTable("Technicians");

            // Primary key
            builder.HasKey(t => t.Id);

            // Properties configuration
            builder.Property(t => t.FirstName)
                .IsRequired()              // NOT NULL constraint
                .HasMaxLength(100);        // VARCHAR(100)

            builder.Property(t => t.LastName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(t => t.Email)
                .IsRequired()
                .HasMaxLength(255);

            // UNIQUE constraint on email
            builder.HasIndex(t => t.Email)
                .IsUnique();

            builder.Property(t => t.PhoneNumber)
                .HasMaxLength(20);

            // Enum is stored as integer by default
            builder.Property(t => t.SkillLevel)
                .IsRequired()
                .HasConversion<int>();     // Store enum as INT

            builder.Property(t => t.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(t => t.MaxConcurrentJobs)
                .IsRequired()
                .HasDefaultValue(2);       // DEFAULT constraint

            // Computed column (not stored, calculated on read)
            // Note: EF Core doesn't support computed properties from multiple columns easily
            // So FullName is typically calculated in C# code

            // Audit fields
            builder.Property(t => t.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");  // SQL Server default

            builder.Property(t => t.UpdatedAt)
                .IsRequired(false);

            builder.Property(t => t.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // NOTE: Relationships configured from WorkOrder side (DDD: no navigation properties)
            // WorkOrder references Technician by ID only (WorkOrderConfiguration.cs)

            // PERFORMANCE: Index on frequently queried columns
            builder.HasIndex(t => t.Status)
                .HasDatabaseName("IX_Technicians_Status");

            builder.HasIndex(t => t.IsActive)
                .HasDatabaseName("IX_Technicians_IsActive");

            // Composite index for common query: active technicians with available status
            builder.HasIndex(t => new { t.IsActive, t.Status })
                .HasDatabaseName("IX_Technicians_Active_Status");
        }
    }
}
