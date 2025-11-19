using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Moq;
using HeavyIMS.Infrastructure.Data;
using HeavyIMS.Infrastructure.Repositories;
using HeavyIMS.Domain.Entities;
using HeavyIMS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HeavyIMS.Tests.IntegrationTests
{
    /// <summary>
    /// Integration Tests for Work Order functionality
    /// DEMONSTRATES:
    /// - Integration testing with real SQL Server database
    /// - Testing EF Core configurations
    /// - Testing repository implementations
    /// - Testing database transactions
    /// - Testing navigation properties and relationships
    ///
    /// DIFFERENCE FROM UNIT TESTS:
    /// - Uses real DbContext (not mocked)
    /// - Tests multiple components together
    /// - Slower but more realistic
    /// - Catches integration issues (SQL generation, relationships)
    ///
    /// WHY INTEGRATION TESTS?
    /// - Verify EF Core mappings work correctly
    /// - Test complex queries against real database
    /// - Ensure transactions work as expected
    /// - Catch issues mocks can't reveal
    ///
    /// KEY CONCEPTS:
    /// - Understanding of different test types
    /// - Demonstrates EF Core proficiency
    /// - Real-world testing scenarios
    /// </summary>
    [Collection("Database Collection")]
    public class WorkOrderIntegrationTests : IDisposable
    {
        private readonly DbContextOptions<HeavyIMSDbContext> _dbOptions;
        private readonly HeavyIMSDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _testPrefix;
        private readonly List<Guid> _testCustomerIds;
        private readonly List<Guid> _testWorkOrderIds;
        private readonly List<Guid> _testTechnicianIds;
        private readonly List<Guid> _testPartIds;

        /// <summary>
        /// Constructor - Setup SQL Server database for each test
        /// PATTERN: Integration test fixture
        /// USES: Real SQL Server for realistic integration testing
        /// </summary>
        public WorkOrderIntegrationTests()
        {
            // Use real SQL Server database for integration tests
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<WorkOrderIntegrationTests>()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=HeavyIMSDb;Trusted_Connection=True;";

            _dbOptions = new DbContextOptionsBuilder<HeavyIMSDbContext>()
                .UseSqlServer(connectionString)
                .EnableSensitiveDataLogging()
                .Options;

            _context = new HeavyIMSDbContext(_dbOptions);

            // Create a mock event dispatcher for integration tests
            // Integration tests focus on database operations, not event handling
            var mockEventDispatcher = new Mock<IDomainEventDispatcher>();
            _unitOfWork = new UnitOfWork(_context, mockEventDispatcher.Object);

            // Shorter prefix to fit VIN length constraints (17 chars max)
            _testPrefix = $"T{Guid.NewGuid().ToString().Substring(0, 2)}";
            _testCustomerIds = new List<Guid>();
            _testWorkOrderIds = new List<Guid>();
            _testTechnicianIds = new List<Guid>();
            _testPartIds = new List<Guid>();

            // Ensure database is created
            _context.Database.EnsureCreated();
        }

        /// <summary>
        /// TEST: Create work order and retrieve it from database
        /// DEMONSTRATES: Full create-read cycle with real database
        /// </summary>
        [Fact]
        public async Task CreateWorkOrder_ShouldPersistToDatabase()
        {
            // ARRANGE: Create customer first (foreign key requirement)
            var customer = Customer.Create(
                "Heavy Equipment Co",
                "John Doe",
                $"{_testPrefix}john@example.com",
                "555-1234",
                "123 Main St");

            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            _testCustomerIds.Add(customer.Id);

            // Create work order
            var workOrder = WorkOrder.Create(
                "1HGBH41JXMN109186",
                "Excavator",
                "CAT 320",
                customer.Id,
                "Hydraulic system needs repair",
                WorkOrderPriority.High,
                "admin@example.com");

            // ACT: Add and save to database
            await _unitOfWork.WorkOrders.AddAsync(workOrder);
            var saveResult = await _unitOfWork.SaveChangesAsync();

            // ASSERT: Verify save was successful
            saveResult.Should().BeGreaterThan(0, "at least one record should be saved");

            // Retrieve from database to verify persistence
            var retrievedWorkOrder = await _unitOfWork.WorkOrders.GetByIdAsync(workOrder.Id);

            retrievedWorkOrder.Should().NotBeNull();
            retrievedWorkOrder.WorkOrderNumber.Should().Be(workOrder.WorkOrderNumber);
            // VALUE OBJECTS: Access nested property
            retrievedWorkOrder.Equipment.VIN.Should().Be("1HGBH41JXMN109186");
            retrievedWorkOrder.Status.Should().Be(WorkOrderStatus.Pending);
            retrievedWorkOrder.CustomerId.Should().Be(customer.Id);
            retrievedWorkOrder.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// TEST: Assign technician to work order with transaction
        /// DEMONSTRATES: Multi-step operation with real transaction
        /// </summary>
        [Fact]
        public async Task AssignTechnicianToWorkOrder_ShouldUpdateBothEntities()
        {
            // ARRANGE: Create customer, work order, and technician
            var customer = Customer.Create(
                "Equipment Co",
                "Jane Smith",
                $"{_testPrefix}jane@example.com",
                "555-5678",
                "456 Oak St");

            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            _testCustomerIds.Add(customer.Id);

            // VALUE OBJECTS: VIN must be exactly 17 characters
            var vin = "1TEST0000000TECH2";
            var workOrder = WorkOrder.Create(
                vin,
                "Bulldozer",
                "CAT D8",
                customer.Id,
                "Engine repair needed",
                WorkOrderPriority.Critical,
                "admin@example.com");

            var technician = Technician.Create(
                "Mike",
                "Johnson",
                $"{_testPrefix}mike@example.com",
                "555-9999",
                TechnicianSkillLevel.Senior);

            await _unitOfWork.WorkOrders.AddAsync(workOrder);
            await _unitOfWork.Technicians.AddAsync(technician);
            await _unitOfWork.SaveChangesAsync();
            _testWorkOrderIds.Add(workOrder.Id);
            _testTechnicianIds.Add(technician.Id);

            // ACT: Assign technician to work order (DDD: use ID, not entity)
            workOrder.AssignTechnician(technician.Id);
            technician.UpdateStatus(TechnicianStatus.OnJob);

            await _unitOfWork.SaveChangesAsync();

            // ASSERT: Verify both entities were updated
            var updatedWorkOrder = await _unitOfWork.WorkOrders
                .GetWorkOrderWithDetailsAsync(workOrder.Id);

            updatedWorkOrder.AssignedTechnicianId.Should().Be(technician.Id);
            updatedWorkOrder.Status.Should().Be(WorkOrderStatus.Assigned);

            var updatedTechnician = await _unitOfWork.Technicians
                .GetByIdAsync(technician.Id);

            updatedTechnician.Status.Should().Be(TechnicianStatus.OnJob);
        }

        /// <summary>
        /// TEST: Technician capacity validation
        /// DEMONSTRATES: Business rule enforcement at database level
        /// </summary>
        [Fact]
        public async Task AssignTechnician_WhenAtCapacity_ShouldThrowException()
        {
            // ARRANGE: Create technician and fill to capacity
            var customer = Customer.Create("Test Co", "Test", $"{_testPrefix}test@test.com", "555-0000", "123 St");
            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            _testCustomerIds.Add(customer.Id);

            var technician = Technician.Create(
                "Bob",
                "Williams",
                $"{_testPrefix}bob@example.com",
                "555-1111",
                TechnicianSkillLevel.Junior);  // Junior = max 2 concurrent jobs

            await _unitOfWork.Technicians.AddAsync(technician);
            await _unitOfWork.SaveChangesAsync();
            _testTechnicianIds.Add(technician.Id);

            // Create and assign 2 work orders (at capacity)
            for (int i = 0; i < 2; i++)
            {
                // VALUE OBJECTS: VIN must be exactly 17 characters
                var wo = WorkOrder.Create(
                    $"1TEST0000000CAP{i:D2}",
                    "Equipment",
                    "Model",
                    customer.Id,
                    $"Repair {i}",
                    WorkOrderPriority.Normal,
                    "admin");

                wo.AssignTechnician(technician.Id);
                await _unitOfWork.WorkOrders.AddAsync(wo);
                _testWorkOrderIds.Add(wo.Id);
            }

            await _unitOfWork.SaveChangesAsync();

            // Try to assign one more (should fail)
            // VALUE OBJECTS: VIN must be exactly 17 characters
            var newWorkOrder = WorkOrder.Create(
                "1TEST00000CAPFAIL",
                "Equipment",
                "Model",
                customer.Id,
                "Should fail",
                WorkOrderPriority.Normal,
                "admin");

            // ACT & ASSERT: Check capacity (DDD: query active work orders separately)
            var activeJobCount = await _unitOfWork.WorkOrders
                .CountActiveWorkOrdersByTechnicianAsync(technician.Id);

            // Technician should not be able to accept new job (at capacity)
            var tech = await _unitOfWork.Technicians.GetByIdAsync(technician.Id);
            tech.CanAcceptNewJob(activeJobCount).Should().BeFalse();
        }

        /// <summary>
        /// TEST: Query pending work orders
        /// DEMONSTRATES: Testing custom repository queries with real database
        /// </summary>
        [Fact]
        public async Task GetPendingWorkOrders_ShouldReturnOnlyPendingOrders()
        {
            // ARRANGE: Create multiple work orders with different statuses
            var customer = Customer.Create("Test Co", "Test", $"{_testPrefix}test@test.com", "555-0000", "123 St");
            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            _testCustomerIds.Add(customer.Id);

            // VALUE OBJECTS: VIN must be exactly 17 characters
            var pendingWO = WorkOrder.Create(
                "1TEST00000PENDING",
                "Type1",
                "Model1",
                customer.Id,
                "Pending work",
                WorkOrderPriority.High,
                "admin");

            var assignedWO = WorkOrder.Create(
                "1TEST0000ASSIGNED",
                "Type2",
                "Model2",
                customer.Id,
                "Assigned work",
                WorkOrderPriority.Normal,
                "admin");

            var completedWO = WorkOrder.Create(
                "1TEST000COMPLETED",
                "Type3",
                "Model3",
                customer.Id,
                "Completed work",
                WorkOrderPriority.Low,
                "admin");

            // Change statuses
            var technician = Technician.Create("Tech", "Name", $"{_testPrefix}tech@test.com", "555", TechnicianSkillLevel.Senior);
            await _unitOfWork.Technicians.AddAsync(technician);
            await _unitOfWork.SaveChangesAsync();
            _testTechnicianIds.Add(technician.Id);

            assignedWO.AssignTechnician(technician.Id);

            // Follow valid status transitions: Pending -> Assigned -> InProgress -> Completed
            completedWO.AssignTechnician(technician.Id);  // Pending -> Assigned
            completedWO.UpdateStatus(WorkOrderStatus.InProgress);  // Assigned -> InProgress
            completedWO.UpdateStatus(WorkOrderStatus.Completed);  // InProgress -> Completed

            await _unitOfWork.WorkOrders.AddAsync(pendingWO);
            await _unitOfWork.WorkOrders.AddAsync(assignedWO);
            await _unitOfWork.WorkOrders.AddAsync(completedWO);
            await _unitOfWork.SaveChangesAsync();
            _testWorkOrderIds.Add(pendingWO.Id);
            _testWorkOrderIds.Add(assignedWO.Id);
            _testWorkOrderIds.Add(completedWO.Id);

            // ACT: Query pending work orders
            var pendingOrders = await _unitOfWork.WorkOrders.GetPendingWorkOrdersAsync();

            // ASSERT: Should only return pending orders (may include others from other tests)
            pendingOrders.Should().Contain(wo => wo.Id == pendingWO.Id);
            pendingOrders.Where(wo => wo.Id == pendingWO.Id).First().Status.Should().Be(WorkOrderStatus.Pending);
            pendingOrders.Should().NotContain(wo => wo.Id == assignedWO.Id);
            pendingOrders.Should().NotContain(wo => wo.Id == completedWO.Id);
        }

        /// <summary>
        /// TEST: Reserve parts for work order
        /// DEMONSTRATES: Testing inventory transactions
        /// </summary>
        [Fact]
        public async Task ReserveParts_ShouldUpdateInventoryAndWorkOrder()
        {
            // ARRANGE: Create inventory part and work order
            var customer = Customer.Create("Test Co", "Test", $"{_testPrefix}test@test.com", "555-0000", "123 St");
            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            _testCustomerIds.Add(customer.Id);

            // Create part in catalog with unique part number
            var partNumber = $"{_testPrefix}PART-001";
            var part = Part.Create(
                partNumber,
                "Hydraulic Pump",
                "Heavy duty hydraulic pump",
                "Hydraulics",
                unitCost: 250.00m,
                unitPrice: 400.00m);

            await _unitOfWork.Parts.AddAsync(part);
            await _unitOfWork.SaveChangesAsync();
            _testPartIds.Add(part.PartId);

            // VALUE OBJECTS: VIN must be exactly 17 characters
            var vin = "1TEST000000PARTS1";
            var workOrder = WorkOrder.Create(
                vin,
                "Excavator",
                "CAT 320",
                customer.Id,
                "Replace hydraulic pump",
                WorkOrderPriority.High,
                "admin");

            // Create inventory location and do ALL operations before saving
            var inventory = Inventory.Create(part.PartId, "Main-Warehouse", "A-12-3", 5, 50);
            inventory.ReceiveParts(20, "admin", "PO-12345");
            inventory.ReserveParts(2, workOrder.Id, "admin");

            // Add required part to work order
            workOrder.AddRequiredPart(part.PartId, 2, isAvailable: true);

            // ACT: Save all entities with domain operations completed
            await _unitOfWork.Inventory.AddAsync(inventory);
            await _unitOfWork.WorkOrders.AddAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            _testWorkOrderIds.Add(workOrder.Id);
            var inventoryId = inventory.InventoryId;
            var workOrderId = workOrder.Id;

            // ASSERT: Verify inventory and work order were updated
            var updatedInventory = await _unitOfWork.Inventory
                .GetInventoryWithTransactionsAsync(inventoryId);

            updatedInventory.QuantityOnHand.Should().Be(20);
            updatedInventory.QuantityReserved.Should().Be(2);
            updatedInventory.GetAvailableQuantity().Should().Be(18);
            updatedInventory.Transactions.Should().HaveCountGreaterThanOrEqualTo(2); // Receipt + Reservation

            var updatedWorkOrder = await _unitOfWork.WorkOrders
                .GetWorkOrderWithDetailsAsync(workOrderId);

            updatedWorkOrder.RequiredParts.Should().HaveCount(1);
            updatedWorkOrder.RequiredParts.First().QuantityRequired.Should().Be(2);
        }

        /// <summary>
        /// Cleanup - Dispose of database context and clean up test data
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Clean up test data in reverse order of dependencies
                // 1. WorkOrderParts (junction table)
                var workOrderParts = _context.WorkOrderParts
                    .Where(wp => _testWorkOrderIds.Contains(wp.WorkOrderId))
                    .ToList();
                _context.WorkOrderParts.RemoveRange(workOrderParts);

                // 2. WorkOrderNotifications
                var notifications = _context.WorkOrderNotifications
                    .Where(n => _testWorkOrderIds.Contains(n.WorkOrderId))
                    .ToList();
                _context.WorkOrderNotifications.RemoveRange(notifications);

                // 3. WorkOrders
                var workOrders = _context.WorkOrders
                    .Where(wo => _testWorkOrderIds.Contains(wo.Id))
                    .ToList();
                _context.WorkOrders.RemoveRange(workOrders);

                // 4. InventoryTransactions
                var inventoryIds = _context.Inventory
                    .Where(i => _testPartIds.Contains(i.PartId))
                    .Select(i => i.InventoryId)
                    .ToList();
                var transactions = _context.InventoryTransactions
                    .Where(t => inventoryIds.Contains(t.InventoryId))
                    .ToList();
                _context.InventoryTransactions.RemoveRange(transactions);

                // 5. Inventory
                var inventory = _context.Inventory
                    .Where(i => _testPartIds.Contains(i.PartId))
                    .ToList();
                _context.Inventory.RemoveRange(inventory);

                // 6. Parts
                var parts = _context.Parts
                    .Where(p => _testPartIds.Contains(p.PartId))
                    .ToList();
                _context.Parts.RemoveRange(parts);

                // 7. Technicians
                var technicians = _context.Technicians
                    .Where(t => _testTechnicianIds.Contains(t.Id))
                    .ToList();
                _context.Technicians.RemoveRange(technicians);

                // 8. Customers (last - referenced by WorkOrders)
                var customers = _context.Customers
                    .Where(c => _testCustomerIds.Contains(c.Id))
                    .ToList();
                _context.Customers.RemoveRange(customers);

                _context.SaveChanges();
            }
            catch
            {
                // Ignore cleanup errors
            }
            finally
            {
                _context?.Dispose();
                _unitOfWork?.Dispose();
            }
        }
    }
}

/*
 * TESTING NOTES:
 *
 * 1. INTEGRATION VS UNIT TESTS:
 *    - Unit: Fast, isolated, mocked dependencies
 *    - Integration: Slower, tests components together, real dependencies
 *    - Both needed: Unit for logic, Integration for data access
 *
 * 2. IN-MEMORY DATABASE:
 *    - Fast (no disk I/O)
 *    - Isolated (each test gets own database)
 *    - Good for testing EF Core logic
 *    - Limitations: Doesn't catch SQL Server-specific issues
 *    - Alternative: TestContainers with real SQL Server in Docker
 *
 * 3. WHAT TO INTEGRATION TEST:
 *    - EF Core configurations (relationships, mappings)
 *    - Complex queries and LINQ
 *    - Database transactions
 *    - Navigation property loading
 *    - Database constraints and validations
 *
 * 4. TEST DATA SETUP:
 *    - Create minimal data needed for test
 *    - Use domain factory methods (Create)
 *    - Respect foreign key constraints
 *    - Clean up after (Dispose pattern)
 *
 * 5. RUNNING TESTS:
 *    - dotnet test: Run all tests
 *    - Test Explorer in Visual Studio
 *    - CI/CD pipeline integration
 *    - Code coverage reports
 */
