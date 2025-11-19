using FluentAssertions;
using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Services;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HeavyIMS.Tests.UnitTests
{
    /// <summary>
    /// Unit Tests for InventoryService
    /// DEMONSTRATES: Testing warehouse operations with multi-location support
    /// TESTS: Inventory aggregate operations (DDD Operational Aggregate)
    /// </summary>
    public class InventoryServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IInventoryRepository> _mockInventoryRepo;
        private readonly Mock<IPartRepository> _mockPartRepo;
        private readonly InventoryService _service;

        public InventoryServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockInventoryRepo = new Mock<IInventoryRepository>();
            _mockPartRepo = new Mock<IPartRepository>();

            _mockUnitOfWork.Setup(u => u.Inventory).Returns(_mockInventoryRepo.Object);
            _mockUnitOfWork.Setup(u => u.Parts).Returns(_mockPartRepo.Object);

            _service = new InventoryService(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task CreateInventoryLocationAsync_WithValidData_ShouldCreateLocation()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var part = Part.Create("HYD-001", "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);

            var dto = new CreateInventoryDto
            {
                PartId = partId,
                Warehouse = "Warehouse-Main",
                BinLocation = "A-12-3",
                MinimumStockLevel = 10,
                MaximumStockLevel = 50
            };

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId))
                .ReturnsAsync(part);

            _mockInventoryRepo
                .Setup(r => r.GetByPartAndWarehouseAsync(partId, dto.Warehouse))
                .ReturnsAsync((Inventory)null);

            _mockInventoryRepo
                .Setup(r => r.AddAsync(It.IsAny<Inventory>()))
                .ReturnsAsync((Inventory i) => i);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.CreateInventoryLocationAsync(dto);

            // ASSERT
            result.Should().NotBeNull();
            result.PartId.Should().Be(partId);
            result.Warehouse.Should().Be(dto.Warehouse);
            result.BinLocation.Should().Be(dto.BinLocation);
            result.MinimumStockLevel.Should().Be(dto.MinimumStockLevel);
            result.MaximumStockLevel.Should().Be(dto.MaximumStockLevel);

            _mockInventoryRepo.Verify(r => r.AddAsync(It.IsAny<Inventory>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateInventoryLocationAsync_WithDuplicateLocation_ShouldThrowException()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var part = Part.Create("HYD-001", "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);
            var existingInventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);

            var dto = new CreateInventoryDto
            {
                PartId = partId,
                Warehouse = "Warehouse-Main",
                BinLocation = "A-12-3",
                MinimumStockLevel = 10,
                MaximumStockLevel = 50
            };

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId))
                .ReturnsAsync(part);

            _mockInventoryRepo
                .Setup(r => r.GetByPartAndWarehouseAsync(partId, dto.Warehouse))
                .ReturnsAsync(existingInventory);

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.CreateInventoryLocationAsync(dto));

            _mockInventoryRepo.Verify(r => r.AddAsync(It.IsAny<Inventory>()), Times.Never);
        }

        [Fact]
        public async Task ReservePartsAsync_WithSufficientQuantity_ShouldReserveParts()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var workOrderId = Guid.NewGuid();
            var inventoryId = Guid.NewGuid();

            var inventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory, inventoryId);
            inventory.ReceiveParts(30, "admin", "PO-001");

            var dto = new ReserveInventoryDto
            {
                PartId = partId,
                Warehouse = "Warehouse-Main",
                Quantity = 5,
                WorkOrderId = workOrderId,
                RequestedBy = "tech@example.com"
            };

            _mockInventoryRepo
                .Setup(r => r.GetByPartAndWarehouseAsync(partId, dto.Warehouse))
                .ReturnsAsync(inventory);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.ReservePartsAsync(dto);

            // ASSERT
            result.Should().NotBeNull();
            result.QuantityReserved.Should().Be(5);
            result.QuantityOnHand.Should().Be(30);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ReservePartsAsync_WithInsufficientQuantity_ShouldThrowException()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var workOrderId = Guid.NewGuid();

            var inventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory.ReceiveParts(5, "admin", "PO-001"); // Only 5 on hand

            var dto = new ReserveInventoryDto
            {
                PartId = partId,
                Warehouse = "Warehouse-Main",
                Quantity = 10, // Trying to reserve 10
                WorkOrderId = workOrderId,
                RequestedBy = "tech@example.com"
            };

            _mockInventoryRepo
                .Setup(r => r.GetByPartAndWarehouseAsync(partId, dto.Warehouse))
                .ReturnsAsync(inventory);

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.ReservePartsAsync(dto));

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task IssuePartsAsync_WithReservedParts_ShouldIssueParts()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var workOrderId = Guid.NewGuid();
            var inventoryId = Guid.NewGuid();

            var inventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory, inventoryId);
            inventory.ReceiveParts(30, "admin", "PO-001");
            inventory.ReserveParts(10, workOrderId, "tech@example.com");

            var dto = new IssuePartsDto
            {
                InventoryId = inventoryId,
                Quantity = 10,
                WorkOrderId = workOrderId,
                IssuedBy = "tech@example.com"
            };

            _mockInventoryRepo
                .Setup(r => r.GetByIdAsync(inventoryId))
                .ReturnsAsync(inventory);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.IssuePartsAsync(dto);

            // ASSERT
            result.Should().NotBeNull();
            result.QuantityOnHand.Should().Be(20); // 30 - 10
            result.QuantityReserved.Should().Be(0); // 10 - 10

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ReceivePartsAsync_ShouldIncreaseQuantity()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var inventoryId = Guid.NewGuid();

            var inventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory, inventoryId);
            inventory.ReceiveParts(10, "admin", "PO-001"); // Initial 10

            var dto = new ReceivePartsDto
            {
                InventoryId = inventoryId,
                Quantity = 20,
                ReceivedBy = "admin",
                ReferenceNumber = "PO-002"
            };

            _mockInventoryRepo
                .Setup(r => r.GetByIdAsync(inventoryId))
                .ReturnsAsync(inventory);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.ReceivePartsAsync(dto);

            // ASSERT
            result.Should().NotBeNull();
            result.QuantityOnHand.Should().Be(30); // 10 + 20

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetLowStockAlertsAsync_ShouldReturnLowStockLocations()
        {
            // ARRANGE
            var partId1 = Guid.NewGuid();
            var partId2 = Guid.NewGuid();

            var inventory1 = Inventory.Create(partId1, "Warehouse-Main", "A-12-3", 10, 50);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory1, Guid.NewGuid());
            inventory1.ReceiveParts(8, "admin", "PO-001"); // Below minimum of 10

            var inventory2 = Inventory.Create(partId2, "Warehouse-East", "B-05-1", 5, 30);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory2, Guid.NewGuid());
            inventory2.ReceiveParts(3, "admin", "PO-002"); // Below minimum of 5

            var part1 = Part.Create("HYD-001", "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);
            typeof(Part).GetProperty("PartId").SetValue(part1, partId1);

            var part2 = Part.Create("ENG-001", "Engine Oil", "Desc", "Engine", 50m, 75m);
            typeof(Part).GetProperty("PartId").SetValue(part2, partId2);

            _mockInventoryRepo
                .Setup(r => r.GetLowStockInventoryAsync())
                .ReturnsAsync(new List<Inventory> { inventory1, inventory2 });

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId1))
                .ReturnsAsync(part1);

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId2))
                .ReturnsAsync(part2);

            // ACT
            var result = await _service.GetLowStockAlertsAsync();

            // ASSERT
            result.Should().HaveCount(2);
            result.All(a => a.CurrentQuantity <= a.MinimumStockLevel).Should().BeTrue();
        }

        [Fact]
        public async Task GetTotalQuantityOnHandAsync_ShouldSumAcrossWarehouses()
        {
            // ARRANGE
            var partId = Guid.NewGuid();

            var inventory1 = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory1.ReceiveParts(30, "admin", "PO-001");

            var inventory2 = Inventory.Create(partId, "Warehouse-East", "B-05-1", 5, 30);
            inventory2.ReceiveParts(15, "admin", "PO-002");

            _mockInventoryRepo
                .Setup(r => r.GetTotalQuantityOnHandAsync(partId))
                .ReturnsAsync(45);

            // ACT
            var result = await _service.GetTotalQuantityOnHandAsync(partId);

            // ASSERT
            result.Should().Be(45);
        }

        [Fact]
        public async Task GetTotalAvailableQuantityAsync_ShouldSumAvailableAcrossWarehouses()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var workOrderId = Guid.NewGuid();

            var inventory1 = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            inventory1.ReceiveParts(30, "admin", "PO-001");
            inventory1.ReserveParts(10, workOrderId, "tech@example.com"); // 30 - 10 = 20 available

            var inventory2 = Inventory.Create(partId, "Warehouse-East", "B-05-1", 5, 30);
            inventory2.ReceiveParts(15, "admin", "PO-002"); // 15 available

            // Total available = 20 + 15 = 35
            _mockInventoryRepo
                .Setup(r => r.GetTotalAvailableQuantityAsync(partId))
                .ReturnsAsync(35);

            // ACT
            var result = await _service.GetTotalAvailableQuantityAsync(partId);

            // ASSERT
            result.Should().Be(35);
        }

        [Fact]
        public async Task AdjustInventoryAsync_ShouldUpdateQuantity()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var inventoryId = Guid.NewGuid();

            var inventory = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory, inventoryId);
            inventory.ReceiveParts(30, "admin", "PO-001");

            var dto = new AdjustInventoryDto
            {
                InventoryId = inventoryId,
                NewQuantity = 28, // Cycle count found 28 instead of 30
                Reason = "Cycle count adjustment",
                AdjustedBy = "supervisor@example.com"
            };

            _mockInventoryRepo
                .Setup(r => r.GetByIdAsync(inventoryId))
                .ReturnsAsync(inventory);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.AdjustInventoryAsync(dto);

            // ASSERT
            result.Should().NotBeNull();
            result.QuantityOnHand.Should().Be(28);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task IsQuantityAvailableAsync_WithSufficientStock_ShouldReturnTrue()
        {
            // ARRANGE
            var partId = Guid.NewGuid();

            _mockInventoryRepo
                .Setup(r => r.IsQuantityAvailableAsync(partId, "Warehouse-Main", 10))
                .ReturnsAsync(true);

            // ACT
            var result = await _service.IsQuantityAvailableAsync(partId, "Warehouse-Main", 10);

            // ASSERT
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsQuantityAvailableAsync_WithInsufficientStock_ShouldReturnFalse()
        {
            // ARRANGE
            var partId = Guid.NewGuid();

            _mockInventoryRepo
                .Setup(r => r.IsQuantityAvailableAsync(partId, "Warehouse-Main", 100))
                .ReturnsAsync(false);

            // ACT
            var result = await _service.IsQuantityAvailableAsync(partId, "Warehouse-Main", 100);

            // ASSERT
            result.Should().BeFalse();
        }
    }
}
