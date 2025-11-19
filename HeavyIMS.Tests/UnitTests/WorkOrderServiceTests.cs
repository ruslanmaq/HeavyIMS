using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using HeavyIMS.Application.Services;
using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Interfaces;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;

namespace HeavyIMS.Tests.UnitTests
{
    /// <summary>
    /// Unit Tests for WorkOrderService
    /// DEMONSTRATES:
    /// - Unit testing with xUnit
    /// - Mocking dependencies with Moq
    /// - AAA pattern (Arrange, Act, Assert)
    /// - FluentAssertions for readable assertions
    /// - Testing business logic in isolation
    ///
    /// TESTING PRINCIPLES:
    /// - Fast: No database, no external dependencies
    /// - Isolated: Each test is independent
    /// - Repeatable: Same results every time
    /// - Self-checking: Automated assertions
    /// - Timely: Written alongside code
    ///
    /// KEY CONCEPTS:
    /// - Automated testing for quality assurance
    /// - Demonstrates testable code design
    /// - Shows dependency injection benefits
    /// </summary>
    public class WorkOrderServiceTests
    {
        // Mock dependencies
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IDistributedCache> _mockCache;
        private readonly Mock<ILogger<WorkOrderService>> _mockLogger;

        // Mock repositories and services
        private readonly Mock<IWorkOrderRepository> _mockWorkOrderRepo;
        private readonly Mock<ITechnicianRepository> _mockTechnicianRepo;
        private readonly Mock<IInventoryService> _mockInventoryService;
        private readonly Mock<IRepository<Customer>> _mockCustomerRepo;

        // System Under Test (SUT)
        private readonly WorkOrderService _service;

        /// <summary>
        /// Constructor - Setup mocks for each test
        /// PATTERN: Test fixture setup
        /// RUNS: Before each test method
        /// </summary>
        public WorkOrderServiceTests()
        {
            // ARRANGE: Create mocks
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCache = new Mock<IDistributedCache>();
            _mockLogger = new Mock<ILogger<WorkOrderService>>();

            // Create repository mocks
            _mockWorkOrderRepo = new Mock<IWorkOrderRepository>();
            _mockTechnicianRepo = new Mock<ITechnicianRepository>();
            _mockInventoryService = new Mock<IInventoryService>();
            _mockCustomerRepo = new Mock<IRepository<Customer>>();

            // Setup Unit of Work to return mock repositories
            _mockUnitOfWork.Setup(u => u.WorkOrders).Returns(_mockWorkOrderRepo.Object);
            _mockUnitOfWork.Setup(u => u.Technicians).Returns(_mockTechnicianRepo.Object);
            _mockUnitOfWork.Setup(u => u.Customers).Returns(_mockCustomerRepo.Object);

            // Create service with mock dependencies
            _service = new WorkOrderService(
                _mockUnitOfWork.Object,
                _mockInventoryService.Object,
                _mockNotificationService.Object,
                _mockCache.Object,
                _mockLogger.Object);
        }

        /// <summary>
        /// TEST: CreateWorkOrderAsync with valid data should create work order
        /// PATTERN: AAA (Arrange, Act, Assert)
        /// DEMONSTRATES: Happy path testing
        /// </summary>
        [Fact]
        public async Task CreateWorkOrderAsync_WithValidData_ShouldCreateWorkOrder()
        {
            // ARRANGE: Prepare test data and mock behaviors
            var customerId = Guid.NewGuid();
            var customer = Customer.Create(
                "Heavy Equipment Co",
                "John Doe",
                "john@example.com",
                "555-1234",
                "123 Main St");

            var dto = new CreateWorkOrderDto
            {
                EquipmentVIN = "1HGBH41JXMN109186",
                EquipmentType = "Excavator",
                EquipmentModel = "CAT 320",
                CustomerId = customerId,
                Description = "Hydraulic system repair",
                Priority = WorkOrderPriority.High,
                EstimatedLaborHours = 8,
                EstimatedCost = 1200,
                CreatedBy = "test@example.com"
            };

            // Mock: Customer exists
            _mockCustomerRepo
                .Setup(r => r.GetByIdAsync(customerId))
                .ReturnsAsync(customer);

            // Mock: SaveChanges succeeds
            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Mock: Notification sent successfully
            _mockNotificationService
                .Setup(n => n.SendWorkOrderCreatedNotificationAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // ACT: Execute the method under test
            var result = await _service.CreateWorkOrderAsync(dto);

            // ASSERT: Verify results
            // FluentAssertions provides readable assertions
            result.Should().NotBeNull();
            result.EquipmentVIN.Should().Be(dto.EquipmentVIN);
            result.EquipmentType.Should().Be(dto.EquipmentType);
            result.Status.Should().Be(WorkOrderStatus.Pending);
            result.EstimatedLaborHours.Should().Be(dto.EstimatedLaborHours);

            // Verify mock interactions
            _mockWorkOrderRepo.Verify(
                r => r.AddAsync(It.IsAny<WorkOrder>()),
                Times.Once,
                "Work order should be added to repository");

            _mockUnitOfWork.Verify(
                u => u.SaveChangesAsync(),
                Times.Once,
                "Changes should be saved");

            _mockNotificationService.Verify(
                n => n.SendWorkOrderCreatedNotificationAsync(
                    It.IsAny<Guid>(),
                    customer.Email,
                    It.IsAny<string>()),
                Times.Once,
                "Customer should be notified");
        }

        /// <summary>
        /// TEST: CreateWorkOrderAsync with invalid customer should throw exception
        /// DEMONSTRATES: Negative testing / error cases
        /// </summary>
        [Fact]
        public async Task CreateWorkOrderAsync_WithInvalidCustomer_ShouldThrowException()
        {
            // ARRANGE
            var dto = new CreateWorkOrderDto
            {
                EquipmentVIN = "1HGBH41JXMN109186",
                EquipmentType = "Excavator",
                CustomerId = Guid.NewGuid(),
                Description = "Test repair",
                CreatedBy = "test@example.com"
            };

            // Mock: Customer not found
            _mockCustomerRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Customer)null);

            // ACT & ASSERT: Verify exception is thrown
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateWorkOrderAsync(dto));

            // Verify SaveChanges was not called
            _mockUnitOfWork.Verify(
                u => u.SaveChangesAsync(),
                Times.Never,
                "Should not save if customer doesn't exist");
        }

        /// <summary>
        /// TEST: AssignWorkOrderAsync should assign technician and send notifications
        /// DEMONSTRATES: Testing complex multi-step operations
        /// </summary>
        [Fact]
        public async Task AssignWorkOrderAsync_WithValidData_ShouldAssignTechnicianAndNotify()
        {
            // ARRANGE
            var workOrderId = Guid.NewGuid();
            var technicianId = Guid.NewGuid();

            // Create domain entities
            var customer = Customer.Create("Test Co", "John", "john@test.com", "555-1234", "123 St");
            var workOrder = WorkOrder.Create(
                "1HGBH41JXMN109186",
                "Excavator",
                "CAT 320",
                customer.Id,
                "Hydraulic repair",
                WorkOrderPriority.Normal,
                "admin@test.com");

            // DDD: WorkOrder no longer has Customer navigation property - only CustomerId

            var technician = Technician.Create(
                "Mike",
                "Smith",
                "mike@test.com",
                "555-5678",
                TechnicianSkillLevel.Senior);

            // Set the technician ID to match our test ID
            typeof(Technician).GetProperty("Id").SetValue(technician, technicianId);

            // Mock: Get work order with details
            _mockWorkOrderRepo
                .Setup(r => r.GetByIdAsync(workOrderId))
                .ReturnsAsync(workOrder);

            // Mock: Get technician (DDD: no navigation properties)
            _mockTechnicianRepo
                .Setup(r => r.GetByIdAsync(technicianId))
                .ReturnsAsync(technician);

            // Mock: Count active work orders for capacity check (DDD: cross-aggregate query)
            _mockWorkOrderRepo
                .Setup(r => r.CountActiveWorkOrdersByTechnicianAsync(technicianId))
                .ReturnsAsync(0);

            // Mock: Get customer (DDD: WorkOrderService loads customer separately for notifications)
            _mockCustomerRepo
                .Setup(r => r.GetByIdAsync(customer.Id))
                .ReturnsAsync(customer);

            // Mock: Transaction methods
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _mockUnitOfWork.Setup(u => u.CommitTransactionAsync()).Returns(Task.CompletedTask);

            // Mock: Notification
            _mockNotificationService
                .Setup(n => n.SendWorkOrderAssignedNotificationAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // ACT
            var result = await _service.AssignWorkOrderAsync(workOrderId, technicianId, "admin@test.com");

            // ASSERT
            result.Should().NotBeNull();
            result.AssignedTechnicianId.Should().Be(technicianId);
            result.Status.Should().Be(WorkOrderStatus.Assigned);

            // Verify transaction was used
            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);

            // Verify notification sent
            _mockNotificationService.Verify(
                n => n.SendWorkOrderAssignedNotificationAsync(
                    workOrderId,
                    technician.Email,
                    It.IsAny<string>()),
                Times.Once);
        }

        /// <summary>
        /// TEST: AssignWorkOrderAsync with technician at capacity should fail
        /// DEMONSTRATES: Business rule validation testing
        /// </summary>
        [Fact]
        public async Task AssignWorkOrderAsync_WithTechnicianAtCapacity_ShouldThrowException()
        {
            // ARRANGE
            var workOrderId = Guid.NewGuid();
            var technicianId = Guid.NewGuid();

            var customer = Customer.Create("Test Co", "John", "john@test.com", "555-1234", "123 St");
            var workOrder = WorkOrder.Create(
                "1HGBH41JXMN109186",
                "Excavator",
                "CAT 320",
                customer.Id,
                "Test repair",
                WorkOrderPriority.Normal,
                "admin@test.com");

            // DDD: WorkOrder no longer has Customer navigation property - only CustomerId

            var technician = Technician.Create(
                "Mike",
                "Smith",
                "mike@test.com",
                "555-5678",
                TechnicianSkillLevel.Junior);  // Junior = max 2 jobs

            // Set the technician ID to match our test ID
            typeof(Technician).GetProperty("Id").SetValue(technician, technicianId);

            // DDD: Mock that technician has 2 active work orders (at capacity)
            // Junior technician max capacity = 2
            _mockWorkOrderRepo
                .Setup(r => r.CountActiveWorkOrdersByTechnicianAsync(technicianId))
                .ReturnsAsync(2);  // At capacity

            _mockWorkOrderRepo
                .Setup(r => r.GetByIdAsync(workOrderId))
                .ReturnsAsync(workOrder);

            _mockTechnicianRepo
                .Setup(r => r.GetByIdAsync(technicianId))
                .ReturnsAsync(technician);

            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.RollbackTransactionAsync()).Returns(Task.CompletedTask);

            // ACT & ASSERT
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.AssignWorkOrderAsync(workOrderId, technicianId, "admin"));

            // Verify rollback was called (via exception in real implementation)
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        /// <summary>
        /// TEST: GetPendingWorkOrdersAsync should use cache when available
        /// DEMONSTRATES: Caching behavior testing
        /// NOTE: Skipped because Moq cannot mock extension methods (GetStringAsync)
        /// </summary>
        [Fact(Skip = "Cannot mock IDistributedCache extension methods with Moq")]
        public async Task GetPendingWorkOrdersAsync_WhenCacheHit_ShouldReturnCachedData()
        {
            // ARRANGE
            var cachedJson = "[{\"Id\":\"" + Guid.NewGuid() + "\",\"WorkOrderNumber\":\"WO-2025-001\"}]";

            _mockCache
                .Setup(c => c.GetStringAsync(It.IsAny<string>(), default))
                .ReturnsAsync(cachedJson);

            // ACT
            var result = await _service.GetPendingWorkOrdersAsync();

            // ASSERT
            result.Should().NotBeNull();

            // Verify repository was NOT called (used cache)
            _mockWorkOrderRepo.Verify(
                r => r.GetPendingWorkOrdersAsync(),
                Times.Never,
                "Should use cache, not database");

            // Verify cache was checked
            _mockCache.Verify(
                c => c.GetStringAsync(It.IsAny<string>(), default),
                Times.Once);
        }
    }
}

/*
 * TESTING NOTES:
 *
 * 1. UNIT TESTING BENEFITS:
 *    - Fast feedback during development
 *    - Prevents regressions
 *    - Documents expected behavior
 *    - Improves code design (forces testable code)
 *
 * 2. MOCKING:
 *    - Isolates unit under test
 *    - Controls dependencies' behavior
 *    - Verifies interactions
 *    - No need for real database/services
 *
 * 3. AAA PATTERN:
 *    - Arrange: Set up test data and mocks
 *    - Act: Execute method under test
 *    - Assert: Verify results and interactions
 *
 * 4. TEST COVERAGE:
 *    - Happy path (valid inputs, expected output)
 *    - Error cases (invalid inputs, exceptions)
 *    - Edge cases (boundaries, null values)
 *    - Business rules (capacity limits, state transitions)
 *
 * 5. TOOLS:
 *    - xUnit: Test framework (.NET standard)
 *    - Moq: Mocking library
 *    - FluentAssertions: Readable assertions
 *    - Coverlet: Code coverage
 */
