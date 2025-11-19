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
    /// Unit Tests for PartService
    /// DEMONSTRATES: Testing application services with mocked dependencies
    /// TESTS: Part catalog management (DDD Catalog Aggregate)
    /// </summary>
    public class PartServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IPartRepository> _mockPartRepo;
        private readonly Mock<IInventoryRepository> _mockInventoryRepo;
        private readonly PartService _service;

        public PartServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockPartRepo = new Mock<IPartRepository>();
            _mockInventoryRepo = new Mock<IInventoryRepository>();

            _mockUnitOfWork.Setup(u => u.Parts).Returns(_mockPartRepo.Object);
            _mockUnitOfWork.Setup(u => u.Inventory).Returns(_mockInventoryRepo.Object);

            _service = new PartService(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task CreatePartAsync_WithValidData_ShouldCreatePart()
        {
            // ARRANGE
            var dto = new CreatePartDto
            {
                PartNumber = "HYD-001",
                PartName = "Hydraulic Pump",
                Description = "Main hydraulic pump",
                Category = "Hydraulics",
                UnitCost = 1200.00m,
                UnitPrice = 1800.00m
            };

            _mockPartRepo
                .Setup(r => r.GetByPartNumberAsync(dto.PartNumber))
                .ReturnsAsync((Part)null);

            _mockPartRepo
                .Setup(r => r.AddAsync(It.IsAny<Part>()))
                .ReturnsAsync((Part p) => p);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.CreatePartAsync(dto);

            // ASSERT
            result.Should().NotBeNull();
            result.PartNumber.Should().Be(dto.PartNumber);
            result.PartName.Should().Be(dto.PartName);
            result.UnitCost.Should().Be(dto.UnitCost);
            result.UnitPrice.Should().Be(dto.UnitPrice);
            result.IsActive.Should().BeTrue();
            result.IsDiscontinued.Should().BeFalse();

            _mockPartRepo.Verify(r => r.AddAsync(It.IsAny<Part>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreatePartAsync_WithDuplicatePartNumber_ShouldThrowException()
        {
            // ARRANGE
            var dto = new CreatePartDto
            {
                PartNumber = "HYD-001",
                PartName = "Hydraulic Pump",
                Description = "Main hydraulic pump",
                Category = "Hydraulics",
                UnitCost = 1200.00m,
                UnitPrice = 1800.00m
            };

            var existingPart = Part.Create("HYD-001", "Existing Part", "Desc", "Cat", 100m, 150m);

            _mockPartRepo
                .Setup(r => r.GetByPartNumberAsync(dto.PartNumber))
                .ReturnsAsync(existingPart);

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.CreatePartAsync(dto));

            _mockPartRepo.Verify(r => r.AddAsync(It.IsAny<Part>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdatePartPricingAsync_WithValidData_ShouldUpdatePricing()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var part = Part.Create("HYD-001", "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);
            typeof(Part).GetProperty("PartId").SetValue(part, partId);

            var dto = new UpdatePartPricingDto
            {
                UnitCost = 1300.00m,
                UnitPrice = 1950.00m
            };

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId))
                .ReturnsAsync(part);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            var result = await _service.UpdatePricingAsync(partId, dto);

            // ASSERT
            result.Should().NotBeNull();
            result.UnitCost.Should().Be(dto.UnitCost);
            result.UnitPrice.Should().Be(dto.UnitPrice);

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DiscontinuePartAsync_WithValidPart_ShouldDiscontinuePart()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var part = Part.Create("HYD-001", "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);
            typeof(Part).GetProperty("PartId").SetValue(part, partId);

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId))
                .ReturnsAsync(part);

            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // ACT
            await _service.DiscontinuePartAsync(partId);

            // ASSERT
            part.IsDiscontinued.Should().BeTrue();
            part.IsActive.Should().BeFalse();

            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetPartWithInventoryAsync_ShouldCombinePartAndInventoryData()
        {
            // ARRANGE
            var partId = Guid.NewGuid();
            var part = Part.Create("HYD-001", "Hydraulic Pump", "Desc", "Hydraulics", 1200m, 1800m);
            typeof(Part).GetProperty("PartId").SetValue(part, partId);

            var inventory1 = Inventory.Create(partId, "Warehouse-Main", "A-12-3", 10, 50);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory1, Guid.NewGuid());
            inventory1.ReceiveParts(30, "admin", "PO-001");

            var inventory2 = Inventory.Create(partId, "Warehouse-East", "B-05-1", 5, 30);
            typeof(Inventory).GetProperty("InventoryId").SetValue(inventory2, Guid.NewGuid());
            inventory2.ReceiveParts(15, "admin", "PO-002");

            _mockPartRepo
                .Setup(r => r.GetByIdAsync(partId))
                .ReturnsAsync(part);

            _mockInventoryRepo
                .Setup(r => r.GetByPartIdAsync(partId))
                .ReturnsAsync(new List<Inventory> { inventory1, inventory2 });

            // ACT
            var result = await _service.GetPartWithInventoryAsync(partId);

            // ASSERT
            result.Should().NotBeNull();
            result.Part.Should().NotBeNull();
            result.Part.PartNumber.Should().Be("HYD-001");
            result.TotalQuantityOnHand.Should().Be(45);
            result.TotalAvailable.Should().Be(45);
            result.Locations.Should().HaveCount(2);
        }

        [Fact]
        public async Task SearchPartsAsync_ShouldReturnMatchingParts()
        {
            // ARRANGE
            var searchTerm = "hydraulic";
            var parts = new List<Part>
            {
                Part.Create("HYD-001", "Hydraulic Pump", "Main pump", "Hydraulics", 1200m, 1800m),
                Part.Create("HYD-002", "Hydraulic Cylinder", "Heavy duty", "Hydraulics", 800m, 1200m)
            };

            _mockPartRepo
                .Setup(r => r.SearchPartsAsync(searchTerm))
                .ReturnsAsync(parts);

            // ACT
            var result = await _service.SearchPartsAsync(searchTerm);

            // ASSERT
            result.Should().HaveCount(2);
            result.All(p => p.Category == "Hydraulics").Should().BeTrue();
        }

        [Fact]
        public async Task GetPartsByCategoryAsync_ShouldReturnPartsInCategory()
        {
            // ARRANGE
            var category = "Hydraulics";
            var parts = new List<Part>
            {
                Part.Create("HYD-001", "Hydraulic Pump", "Main pump", "Hydraulics", 1200m, 1800m),
                Part.Create("HYD-002", "Hydraulic Cylinder", "Heavy duty", "Hydraulics", 800m, 1200m)
            };

            _mockPartRepo
                .Setup(r => r.GetPartsByCategoryAsync(category))
                .ReturnsAsync(parts);

            // ACT
            var result = await _service.GetPartsByCategoryAsync(category);

            // ASSERT
            result.Should().HaveCount(2);
            result.All(p => p.Category == category).Should().BeTrue();
        }

        [Fact]
        public async Task GetActivePartsAsync_ShouldReturnOnlyActiveParts()
        {
            // ARRANGE
            var parts = new List<Part>
            {
                Part.Create("HYD-001", "Hydraulic Pump", "Main pump", "Hydraulics", 1200m, 1800m),
                Part.Create("ENG-001", "Engine Oil", "Premium oil", "Engine", 50m, 75m)
            };

            _mockPartRepo
                .Setup(r => r.GetActivePartsAsync())
                .ReturnsAsync(parts);

            // ACT
            var result = await _service.GetActivePartsAsync();

            // ASSERT
            result.Should().HaveCount(2);
            result.All(p => p.IsActive).Should().BeTrue();
        }
    }
}
