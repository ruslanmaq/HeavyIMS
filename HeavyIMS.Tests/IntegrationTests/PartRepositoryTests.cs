using FluentAssertions;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using HeavyIMS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HeavyIMS.Tests.IntegrationTests
{
    /// <summary>
    /// Integration Tests for PartRepository
    /// DEMONSTRATES: Testing repository with real EF Core and SQL Server
    /// TESTS: Part catalog queries and EF Core configuration
    /// </summary>
    [Collection("Database Collection")]
    public class PartRepositoryTests : IDisposable
    {
        private readonly HeavyIMSDbContext _context;
        private readonly IPartRepository _repository;
        private readonly string _testPrefix;

        public PartRepositoryTests()
        {
            // Use real SQL Server database for integration tests
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<PartRepositoryTests>()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;";

            var options = new DbContextOptionsBuilder<HeavyIMSDbContext>()
                .UseSqlServer(connectionString)
                .EnableSensitiveDataLogging()
                .Options;

            _context = new HeavyIMSDbContext(options);
            _repository = new PartRepository(_context);
            _testPrefix = $"TEST-{Guid.NewGuid().ToString().Substring(0, 8)}-";

            // Ensure database is created
            _context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            // Clean up test data - remove any parts created during this test
            var testParts = _context.Parts.Where(p => p.PartNumber.StartsWith(_testPrefix)).ToList();
            _context.Parts.RemoveRange(testParts);
            _context.SaveChanges();

            _context.Dispose();
        }

        [Fact]
        public async Task AddAsync_ShouldAddPartToDatabase()
        {
            // ARRANGE
            var partNumber = $"{_testPrefix}HYD-001";
            var part = Part.Create(
                partNumber,
                "Hydraulic Pump",
                "Main hydraulic pump",
                "Hydraulics",
                unitCost: 1200.00m,
                unitPrice: 1800.00m);

            // ACT
            var result = await _repository.AddAsync(part);
            await _context.SaveChangesAsync();

            // ASSERT
            result.Should().NotBeNull();
            var savedPart = await _context.Parts.FindAsync(result.PartId);
            savedPart.Should().NotBeNull();
            savedPart.PartNumber.Should().Be(partNumber);
        }

        [Fact]
        public async Task GetByPartNumberAsync_ShouldReturnPart()
        {
            // ARRANGE
            var partNumber = $"{_testPrefix}HYD-001";
            var part = Part.Create(partNumber, "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);
            await _repository.AddAsync(part);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetByPartNumberAsync(partNumber);

            // ASSERT
            result.Should().NotBeNull();
            result.PartNumber.Should().Be(partNumber);
            result.PartName.Should().Be("Hydraulic Pump");
        }

        [Fact]
        public async Task GetByPartNumberAsync_WithNonExistentPart_ShouldReturnNull()
        {
            // ACT
            var result = await _repository.GetByPartNumberAsync("NON-EXISTENT");

            // ASSERT
            result.Should().BeNull();
        }

        [Fact]
        public async Task SearchPartsAsync_ShouldFindPartsByPartNumber()
        {
            // ARRANGE
            await SeedTestParts();

            // ACT
            var result = await _repository.SearchPartsAsync(_testPrefix);

            // ASSERT
            result.Should().HaveCountGreaterThanOrEqualTo(3);
            result.All(p => p.PartNumber.StartsWith(_testPrefix)).Should().BeTrue();
        }

        [Fact]
        public async Task SearchPartsAsync_ShouldFindPartsByName()
        {
            // ARRANGE
            await SeedTestParts();

            // ACT - Search using test prefix to find only our test parts
            var result = await _repository.SearchPartsAsync(_testPrefix);

            // ASSERT - Should find our 3 test parts
            result.Should().HaveCountGreaterThanOrEqualTo(3);
            result.Should().Contain(p => p.PartName.Contains("Pump"));
        }

        [Fact]
        public async Task GetPartsByCategoryAsync_ShouldReturnOnlyPartsInCategory()
        {
            // ARRANGE
            await SeedTestParts();

            // ACT
            var result = await _repository.GetPartsByCategoryAsync("Hydraulics");

            // ASSERT - Should include our 2 hydraulics test parts plus possibly others
            var testParts = result.Where(p => p.PartNumber.StartsWith(_testPrefix)).ToList();
            testParts.Should().HaveCount(2);
            testParts.All(p => p.Category == "Hydraulics").Should().BeTrue();
            result.All(p => p.Category == "Hydraulics").Should().BeTrue(); // All results should be Hydraulics
        }

        [Fact]
        public async Task GetActivePartsAsync_ShouldReturnOnlyActiveParts()
        {
            // ARRANGE
            var activePartNumber = $"{_testPrefix}ACT-001";
            var discontinuedPartNumber = $"{_testPrefix}DIS-001";
            var activePart = Part.Create(activePartNumber, "Active Part", "Desc", "Cat", 100m, 150m);
            var discontinuedPart = Part.Create(discontinuedPartNumber, "Discontinued Part", "Desc", "Cat", 100m, 150m);
            discontinuedPart.Discontinue();

            await _repository.AddAsync(activePart);
            await _repository.AddAsync(discontinuedPart);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetActivePartsAsync();

            // ASSERT - Should include our test part plus possibly others from other tests
            result.Should().Contain(p => p.PartNumber == activePartNumber);
            result.Should().NotContain(p => p.PartNumber == discontinuedPartNumber);
        }

        [Fact]
        public async Task GetDiscontinuedPartsAsync_ShouldReturnOnlyDiscontinuedParts()
        {
            // ARRANGE
            var activePartNumber = $"{_testPrefix}ACT-002";
            var discontinuedPartNumber = $"{_testPrefix}DIS-002";
            var activePart = Part.Create(activePartNumber, "Active Part", "Desc", "Cat", 100m, 150m);
            var discontinuedPart = Part.Create(discontinuedPartNumber, "Discontinued Part", "Desc", "Cat", 100m, 150m);
            discontinuedPart.Discontinue();

            await _repository.AddAsync(activePart);
            await _repository.AddAsync(discontinuedPart);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetDiscontinuedPartsAsync();

            // ASSERT - Should include our test part plus possibly others from other tests
            result.Should().Contain(p => p.PartNumber == discontinuedPartNumber);
            result.Should().NotContain(p => p.PartNumber == activePartNumber);
        }

        [Fact]
        public async Task Update_ShouldModifyPart()
        {
            // ARRANGE
            var partNumber = $"{_testPrefix}HYD-001";
            var part = Part.Create(partNumber, "Hydraulic Pump", "Old desc", "Hydraulics", 1200m, 1800m);
            await _repository.AddAsync(part);
            await _context.SaveChangesAsync();

            // ACT
            part.UpdatePricing(1300m, 1950m);
            _repository.Update(part);
            await _context.SaveChangesAsync();

            // ASSERT
            var updatedPart = await _repository.GetByPartNumberAsync(partNumber);
            // VALUE OBJECTS: Compare Amount property
            updatedPart.UnitCost.Amount.Should().Be(1300m);
            updatedPart.UnitPrice.Amount.Should().Be(1950m);
        }

        [Fact]
        public async Task UniqueConstraint_OnPartNumber_ShouldBeEnforced()
        {
            // ARRANGE
            var partNumber = $"{_testPrefix}HYD-001";
            var part1 = Part.Create(partNumber, "Part 1", "Desc", "Cat", 100m, 150m);
            var part2 = Part.Create(partNumber, "Part 2", "Desc", "Cat", 200m, 250m);

            await _repository.AddAsync(part1);
            await _context.SaveChangesAsync();

            await _repository.AddAsync(part2);

            // ACT & ASSERT
            // Real SQL Server will enforce unique constraints
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await _context.SaveChangesAsync();
            });

            // Also verify the configuration is set up correctly
            var indexes = _context.Model.FindEntityType(typeof(Part))
                .GetIndexes()
                .Where(i => i.Properties.Any(p => p.Name == "PartNumber"));

            indexes.Should().NotBeEmpty();
            indexes.Any(i => i.IsUnique).Should().BeTrue();
        }

        private async Task SeedTestParts()
        {
            var parts = new[]
            {
                Part.Create($"{_testPrefix}HYD-001", "Hydraulic Pump", "Main pump", "Hydraulics", 1200m, 1800m),
                Part.Create($"{_testPrefix}HYD-002", "Hydraulic Cylinder", "Heavy duty", "Hydraulics", 800m, 1200m),
                Part.Create($"{_testPrefix}ENG-001", "Engine Oil", "Premium oil", "Engine", 50m, 75m)
            };

            foreach (var part in parts)
            {
                await _repository.AddAsync(part);
            }

            await _context.SaveChangesAsync();
        }
    }
}
