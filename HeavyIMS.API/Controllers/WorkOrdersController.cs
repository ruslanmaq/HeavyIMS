using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HeavyIMS.API.Controllers
{
    /// <summary>
    /// Work Orders API Controller
    /// DEMONSTRATES:
    /// - RESTful API design
    /// - Dependency Injection in controllers
    /// - HTTP status codes and responses
    /// - Error handling and validation
    /// - API routing and versioning
    ///
    /// ADDRESSES: All 5 challenges via API endpoints
    ///
    /// REST PRINCIPLES:
    /// - Resources identified by URIs (/api/workorders/{id})
    /// - HTTP verbs for operations (GET, POST, PUT, DELETE)
    /// - Stateless communication
    /// - Standard HTTP status codes
    /// </summary>
    [ApiController]  // Enables automatic model validation and better error responses
    [Route("api/[controller]")]  // Route: /api/workorders
    [Produces("application/json")]  // Default response type
    public class WorkOrdersController : ControllerBase
    {
        private readonly IWorkOrderService _workOrderService;
        private readonly ILogger<WorkOrdersController> _logger;

        /// <summary>
        /// Constructor - Dependency Injection
        /// DEMONSTRATES: Constructor injection in ASP.NET Core
        /// SERVICES INJECTED:
        /// - IWorkOrderService: Business logic
        /// - ILogger: Logging/diagnostics
        /// </summary>
        public WorkOrdersController(
            IWorkOrderService workOrderService,
            ILogger<WorkOrdersController> logger)
        {
            _workOrderService = workOrderService ?? throw new ArgumentNullException(nameof(workOrderService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// GET: api/workorders/pending
        /// Get all pending work orders (not yet assigned)
        /// ADDRESSES CHALLENGE 1: Scheduling dashboard
        /// </summary>
        /// <returns>List of pending work orders</returns>
        [HttpGet("pending")]
        [ProducesResponseType(typeof(IEnumerable<WorkOrderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPendingWorkOrders()
        {
            try
            {
                _logger.LogInformation("Getting pending work orders");

                var workOrders = await _workOrderService.GetPendingWorkOrdersAsync();

                // HTTP 200 OK with data
                return Ok(workOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending work orders");

                // HTTP 500 Internal Server Error
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving work orders" });
            }
        }

        /// <summary>
        /// GET: api/workorders/{id}
        /// Get a specific work order by ID
        /// </summary>
        /// <param name="id">Work order ID</param>
        /// <returns>Work order details</returns>
        [HttpGet("{id:guid}")]  // Route constraint: id must be a GUID
        [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetWorkOrderById(Guid id)
        {
            try
            {
                _logger.LogInformation("Getting work order {WorkOrderId}", id);

                var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);

                // HTTP 200 OK with data
                return Ok(workOrder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Work order not found: {WorkOrderId}", id);

                // HTTP 404 Not Found
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving work order {WorkOrderId}", id);

                // HTTP 500 Internal Server Error
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving the work order" });
            }
        }

        /// <summary>
        /// POST: api/workorders
        /// Create a new work order
        /// DEMONSTRATES:
        /// - Model validation via [ApiController]
        /// - HTTP 201 Created response
        /// - Location header for new resource
        /// </summary>
        /// <param name="dto">Work order creation data</param>
        /// <returns>Created work order</returns>
        [HttpPost]
        [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderDto dto)
        {
            try
            {
                // MODEL VALIDATION
                // [ApiController] attribute automatically validates the model
                // and returns 400 Bad Request if validation fails
                // No need to check ModelState.IsValid manually

                _logger.LogInformation("Creating work order for VIN {VIN}", dto.EquipmentVIN);

                var workOrder = await _workOrderService.CreateWorkOrderAsync(dto);

                // HTTP 201 Created with Location header
                // Location: /api/workorders/{id}
                return CreatedAtAction(
                    nameof(GetWorkOrderById),  // Action name
                    new { id = workOrder.Id }, // Route values
                    workOrder);                // Response body
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid work order creation request");

                // HTTP 400 Bad Request
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating work order");

                // HTTP 500 Internal Server Error
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while creating the work order" });
            }
        }

        /// <summary>
        /// POST: api/workorders/{id}/assign
        /// Assign work order to a technician
        /// ADDRESSES CHALLENGE 1: Drag-and-drop scheduling
        /// DEMONSTRATES: Complex action endpoint
        /// </summary>
        /// <param name="id">Work order ID</param>
        /// <param name="dto">Assignment data</param>
        /// <returns>Updated work order</returns>
        [HttpPost("{id:guid}/assign")]
        [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssignWorkOrder(
            Guid id,
            [FromBody] AssignWorkOrderDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "Assigning work order {WorkOrderId} to technician {TechnicianId}",
                    id, dto.TechnicianId);

                var workOrder = await _workOrderService.AssignWorkOrderAsync(
                    id,
                    dto.TechnicianId,
                    dto.AssignedBy);

                // HTTP 200 OK with updated resource
                return Ok(workOrder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid assignment request");

                // HTTP 400 Bad Request
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot assign work order");

                // HTTP 400 Bad Request (business rule violation)
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning work order {WorkOrderId}", id);

                // HTTP 500 Internal Server Error
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while assigning the work order" });
            }
        }

        /// <summary>
        /// PUT: api/workorders/{id}/status
        /// Update work order status
        /// TRIGGERS: Automated notifications (CHALLENGE 3)
        /// </summary>
        /// <param name="id">Work order ID</param>
        /// <param name="dto">New status</param>
        /// <returns>Updated work order</returns>
        [HttpPut("{id:guid}/status")]
        [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateWorkOrderStatus(
            Guid id,
            [FromBody] UpdateWorkOrderStatusDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "Updating work order {WorkOrderId} status to {NewStatus}",
                    id, dto.NewStatus);

                var workOrder = await _workOrderService.UpdateStatusAsync(id, dto.NewStatus);

                // HTTP 200 OK
                return Ok(workOrder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Work order not found or invalid status");

                // HTTP 404 Not Found or 400 Bad Request
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid status transition");

                // HTTP 400 Bad Request (invalid state transition)
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating work order status");

                // HTTP 500 Internal Server Error
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while updating the work order status" });
            }
        }

        /// <summary>
        /// POST: api/workorders/{id}/reserve-parts
        /// Reserve parts for a work order
        /// ADDRESSES CHALLENGE 2: Parts management
        /// </summary>
        /// <param name="id">Work order ID</param>
        /// <param name="dto">Parts to reserve</param>
        /// <returns>Success response</returns>
        [HttpPost("{id:guid}/reserve-parts")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReserveParts(
            Guid id,
            [FromBody] ReservePartsDto dto)
        {
            try
            {
                _logger.LogInformation(
                    "Reserving {Count} parts for work order {WorkOrderId}",
                    dto.Parts.Count, id);

                await _workOrderService.ReservePartsForWorkOrderAsync(
                    id,
                    dto.Parts,
                    dto.ReservedBy);

                // HTTP 204 No Content (success with no response body)
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid parts reservation request");

                // HTTP 400 Bad Request or 404 Not Found
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot reserve parts");

                // HTTP 400 Bad Request (insufficient quantity, etc.)
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reserving parts for work order {WorkOrderId}", id);

                // HTTP 500 Internal Server Error
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while reserving parts" });
            }
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. HTTP STATUS CODES:
 *    - 200 OK: Successful GET, PUT, PATCH
 *    - 201 Created: Successful POST (resource created)
 *    - 204 No Content: Successful action with no response body
 *    - 400 Bad Request: Invalid input, business rule violation
 *    - 404 Not Found: Resource doesn't exist
 *    - 500 Internal Server Error: Unexpected error
 *
 * 2. RESTFUL API DESIGN:
 *    - Resources: /api/workorders
 *    - Actions on resources: /api/workorders/{id}/assign
 *    - HTTP verbs: GET (read), POST (create), PUT (update), DELETE (delete)
 *    - Stateless: Each request contains all needed information
 *
 * 3. CONTROLLER RESPONSIBILITIES:
 *    - Routing: Map HTTP requests to actions
 *    - Validation: Ensure input is valid (via [ApiController])
 *    - Orchestration: Call appropriate services
 *    - Response formatting: Return appropriate HTTP status codes
 *    - Logging: Record requests and errors
 *
 * 4. BEST PRACTICES:
 *    - Thin controllers: Business logic in services
 *    - Async/await: Non-blocking I/O operations
 *    - Dependency injection: Services injected via constructor
 *    - Error handling: Try-catch with appropriate responses
 *    - Logging: Record important events and errors
 *
 * 5. API VERSIONING (Future):
 *    - URI versioning: /api/v1/workorders, /api/v2/workorders
 *    - Header versioning: api-version: 1.0
 *    - Query string: /api/workorders?api-version=1.0
 */
