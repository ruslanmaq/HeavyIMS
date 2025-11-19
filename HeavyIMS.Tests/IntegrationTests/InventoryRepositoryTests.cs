using FluentAssertions;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using HeavyIMS.Infrastructure.Data;
using HeavyIMS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HeavyIMS.Tests.IntegrationTests
{
    /// <summary>
    /// Integration Tests for InventoryRepository
    /// DEMONSTRATES: Testing multi-warehouse inventory operations with EF Core
    /// TESTS: Inventory operational aggregate, warehouse separation, unique constraints
    /// NOTE: Now uses real SQL Server database for more accurate integration testing
    /// </summary>
    [Collection("Database Collection")]
    public class InventoryRepositoryTests : IDisposable
    {
        private readonly HeavyIMSDbContext _context;
        private readonly IInventoryRepository _repository;
        private readonly Guid _testPartId;

        public InventoryRepositoryTests()
        {
            // Use real SQL Server database for integration tests
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<InventoryRepositoryTests>()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;";

            var options = new DbContextOptionsBuilder<HeavyIMSDbContext>()
                .UseSqlServer(connectionString)
                .EnableSensitiveDataLogging()
                .Options;

            _context = new HeavyIMSDbContext(options);
            _repository = new InventoryRepository(_context);
            _testPartId = Guid.NewGuid();

            // Ensure database is created
            _context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            try
            {
                // Clear any tracked entities to avoid concurrency issues
                _context.ChangeTracker.Clear();

                // Clean up test data - remove child transactions first, then inventory records
                var testInventories = _context.Inventory.Where(i => i.PartId == _testPartId).ToList();
                var inventoryIds = testInventories.Select(i => i.InventoryId).ToList();

                // Delete child transactions first (foreign key constraint)
                var testTransactions = _context.InventoryTransactions.Where(t => inventoryIds.Contains(t.InventoryId)).ToList();
                _context.InventoryTransactions.RemoveRange(testTransactions);

                // Then delete inventory records
                _context.Inventory.RemoveRange(testInventories);

                _context.SaveChanges();
            }
            catch
            {
                // Ignore cleanup errors
            }
            finally
            {
                _context.Dispose();
            }
        }

        [Fact]
        public async Task AddAsync_ShouldAddInventoryLocation()
        {
            // ARRANGE
            var inventory = Inventory.Create(
                _testPartId,
                "Warehouse-Main",
                "A-12-3",
                minimumStockLevel: 10,
                maximumStockLevel: 50);

            // ACT
            var result = await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ASSERT
            result.Should().NotBeNull();
            var saved = await _context.Inventory.FindAsync(result.InventoryId);
            saved.Should().NotBeNull();
            saved.Warehouse.Should().Be("Warehouse-Main");
            saved.BinLocation.Should().Be("A-12-3");
        }

        [Fact]
        public async Task GetByPartIdAsync_ShouldReturnAllLocationsForPart()
        {
            // ARRANGE
            await SeedMultiWarehouseInventory();

            // ACT
            var result = await _repository.GetByPartIdAsync(_testPartId);

            // ASSERT
            result.Should().HaveCount(3);
            result.Select(i => i.Warehouse).Should().Contain(new[] { "Warehouse-Main", "Warehouse-East", "Warehouse-West" });
        }

        [Fact]
        public async Task GetByPartAndWarehouseAsync_ShouldReturnSpecificLocation()
        {
            // ARRANGE
            await SeedMultiWarehouseInventory();

            // ACT
            var result = await _repository.GetByPartAndWarehouseAsync(_testPartId, "Warehouse-Main");

            // ASSERT
            result.Should().NotBeNull();
            result.Warehouse.Should().Be("Warehouse-Main");
            result.BinLocation.Should().Be("A-12-3");
        }

        [Fact]
        public async Task GetByPartAndWarehouseAsync_WithNonExistentWarehouse_ShouldReturnNull()
        {
            // ARRANGE
            await SeedMultiWarehouseInventory();

            // ACT
            var result = await _repository.GetByPartAndWarehouseAsync(_testPartId, "NonExistent");

            // ASSERT
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByWarehouseAsync_ShouldReturnAllInventoryAtWarehouse()
        {
            // ARRANGE
            await SeedMultiWarehouseInventory();

            var otherPartId = Guid.NewGuid();
            var otherInventory = Inventory.Create(otherPartId, "Warehouse-Main", "B-10-5", 5, 30);
            await _repository.AddAsync(otherInventory);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetByWarehouseAsync("Warehouse-Main");

            // ASSERT
            result.Should().HaveCountGreaterThanOrEqualTo(2);
            result.All(i => i.Warehouse == "Warehouse-Main").Should().BeTrue();
        }

        [Fact]
        public async Task GetLowStockInventoryAsync_ShouldReturnOnlyLowStockLocations()
        {
            // ARRANGE
            var partId = Guid.NewGuid();

            // Low stock location (3 on hand, 10 minimum)
            var lowStockInventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            lowStockInventory.ReceiveParts(3, "admin", "PO-001");

            // Normal stock location (30 on hand, 10 minimum)
            var normalInventory = Inventory.Create(partId, "Warehouse-East", "B-05-1", 10, 50);
            normalInventory.ReceiveParts(30, "admin", "PO-002");

            await _repository.AddAsync(lowStockInventory);
            await _repository.AddAsync(normalInventory);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetLowStockInventoryAsync();

            // ASSERT
            result.Should().Contain(i => i.Warehouse == "Warehouse-Main");
            result.Should().NotContain(i => i.Warehouse == "Warehouse-East");
        }

        [Fact]
        public async Task GetOutOfStockInventoryAsync_ShouldReturnOnlyEmptyLocations()
        {
            // ARRANGE
            var partId = Guid.NewGuid();

            // Out of stock location
            var outOfStockInventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            // Don't receive any parts - starts at 0

            // In stock location
            var inStockInventory = Inventory.Create(partId, "Warehouse-East", "B-05-1", 10, 50);
            inStockInventory.ReceiveParts(20, "admin", "PO-001");

            await _repository.AddAsync(outOfStockInventory);
            await _repository.AddAsync(inStockInventory);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetOutOfStockInventoryAsync();

            // ASSERT
            result.Should().Contain(i => i.Warehouse == "Warehouse-Main");
            result.Should().NotContain(i => i.Warehouse == "Warehouse-East");
        }

        [Fact]
        public async Task GetInventoryWithTransactionsAsync_ShouldIncludeTransactions()
        {
            // ARRANGE
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(30, "admin", "PO-001");
            inventory.ReserveParts(10, Guid.NewGuid(), "tech@example.com");

            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetInventoryWithTransactionsAsync(inventory.InventoryId);

            // ASSERT
            result.Should().NotBeNull();
            result.Transactions.Should().HaveCountGreaterThanOrEqualTo(2); // Receipt + Reservation
        }

        [Fact]
        public async Task IsQuantityAvailableAsync_WithSufficientStock_ShouldReturnTrue()
        {
            // ARRANGE
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(30, "admin", "PO-001");
            inventory.ReserveParts(10, Guid.NewGuid(), "tech@example.com");
            // Available = 30 - 10 = 20

            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.IsQuantityAvailableAsync(_testPartId, "Warehouse-Main", 15);

            // ASSERT
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsQuantityAvailableAsync_WithInsufficientStock_ShouldReturnFalse()
        {
            // ARRANGE
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(10, "admin", "PO-001");
            inventory.ReserveParts(5, Guid.NewGuid(), "tech@example.com");
            // Available = 10 - 5 = 5

            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.IsQuantityAvailableAsync(_testPartId, "Warehouse-Main", 10);

            // ASSERT
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetTotalQuantityOnHandAsync_ShouldSumAcrossWarehouses()
        {
            // ARRANGE
            await SeedMultiWarehouseInventory();

            // ACT
            var result = await _repository.GetTotalQuantityOnHandAsync(_testPartId);

            // ASSERT
            result.Should().Be(65); // 30 + 20 + 15
        }

        [Fact]
        public async Task GetTotalAvailableQuantityAsync_ShouldSumAvailableAcrossWarehouses()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var workOrderId = Guid.NewGuid();

            var inv1 = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            inv1.ReceiveParts(30, "admin", "PO-001");
            inv1.ReserveParts(10, workOrderId, "tech@example.com"); // Available: 20

            var inv2 = Inventory.Create(partId, "Warehouse-East", "B-05-1", 5, 30);
            inv2.ReceiveParts(15, "admin", "PO-002"); // Available: 15

            await _repository.AddAsync(inv1);
            await _repository.AddAsync(inv2);
            await _context.SaveChangesAsync();

            // ACT
            var result = await _repository.GetTotalAvailableQuantityAsync(partId);

            // ASSERT
            result.Should().Be(35); // 20 + 15
        }

        [Fact]
        public async Task Update_ShouldModifyInventory()
        {
            // ARRANGE
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(30, "admin", "PO-001");
            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ACT
            inventory.UpdateStockLevels(15, 60, 25);
            _repository.Update(inventory);
            await _context.SaveChangesAsync();

            // ASSERT
            var updated = await _repository.GetByIdAsync(inventory.InventoryId);
            updated.MinimumStockLevel.Should().Be(15);
            updated.MaximumStockLevel.Should().Be(60);
            updated.ReorderQuantity.Should().Be(25);
        }

        [Fact]
        public async Task UniqueConstraint_OnPartIdAndWarehouse_ShouldBeConfigured()
        {
            // ARRANGE & ACT
            // Verify the unique index is configured on (PartId, Warehouse)
            var entityType = _context.Model.FindEntityType(typeof(Inventory));
            var indexes = entityType.GetIndexes()
                .Where(i => i.Properties.Any(p => p.Name == "PartId") &&
                           i.Properties.Any(p => p.Name == "Warehouse"));

            // ASSERT
            indexes.Should().NotBeEmpty();
            indexes.Any(i => i.IsUnique).Should().BeTrue();
        }

        [Fact]
        public async Task ReserveParts_ShouldCreateTransactionRecord()
        {
            // ARRANGE
            var workOrderId = Guid.NewGuid();
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(30, "admin", "PO-001");
            inventory.ReserveParts(10, workOrderId, "tech@example.com");

            // ACT - Save entity with all domain operations completed
            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ASSERT
            var inventoryId = inventory.InventoryId;
            var withTransactions = await _repository.GetInventoryWithTransactionsAsync(inventoryId);
            withTransactions.Transactions.Should().HaveCountGreaterThanOrEqualTo(2);
            withTransactions.Transactions.Should().Contain(t => t.TransactionType == InventoryTransactionType.Reservation);
        }

        [Fact]
        public async Task IssueParts_ShouldDecreaseQuantityAndCreateTransaction()
        {
            // ARRANGE
            var workOrderId = Guid.NewGuid();
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(30, "admin", "PO-001");
            inventory.ReserveParts(10, workOrderId, "tech@example.com");
            inventory.IssueParts(10, workOrderId, "tech@example.com");

            // ACT - Save entity with all domain operations completed
            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ASSERT
            var inventoryId = inventory.InventoryId;
            var updated = await _repository.GetByIdAsync(inventoryId);
            updated.QuantityOnHand.Should().Be(20); // 30 - 10
            updated.QuantityReserved.Should().Be(0); // 10 - 10

            var withTransactions = await _repository.GetInventoryWithTransactionsAsync(inventoryId);
            withTransactions.Transactions.Should().Contain(t => t.TransactionType == InventoryTransactionType.Issue);
        }

        [Fact]
        public async Task MoveToBinLocation_ShouldUpdateLocationAndCreateAuditTrail()
        {
            // ARRANGE
            var inventory = Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.MoveToBinLocation("A-15-7", "supervisor@example.com");

            // ACT - Save entity with all domain operations completed
            await _repository.AddAsync(inventory);
            await _context.SaveChangesAsync();

            // ASSERT
            var inventoryId = inventory.InventoryId;
            var updated = await _repository.GetByIdAsync(inventoryId);
            updated.BinLocation.Should().Be("A-15-7");

            var withTransactions = await _repository.GetInventoryWithTransactionsAsync(inventoryId);
            withTransactions.Transactions.Should().Contain(t =>
                t.TransactionType == InventoryTransactionType.Adjustment &&
                t.Notes.Contains("Moved from"));
        }

        private async Task SeedMultiWarehouseInventory()
        {
            var locations = new[]
            {
                Inventory.Create(_testPartId, "Warehouse-Main", "A-12-3", 10, 50),
                Inventory.Create(_testPartId, "Warehouse-East", "B-05-1", 5, 30),
                Inventory.Create(_testPartId, "Warehouse-West", "C-08-2", 8, 40)
            };

            locations[0].ReceiveParts(30, "admin", "PO-001");
            locations[1].ReceiveParts(20, "admin", "PO-002");
            locations[2].ReceiveParts(15, "admin", "PO-003");

            foreach (var location in locations)
            {
                await _repository.AddAsync(location);
            }

            await _context.SaveChangesAsync();
        }
    }
}
