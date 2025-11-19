using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavyIMS.API.Controllers
{
    /// <summary>
    /// API Controller: Inventory Operations Management
    /// DEMONSTRATES: RESTful API for warehouse inventory operations
    /// ADDRESSES CHALLENGE 2: Real-time inventory tracking and alerts
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        #region Query Endpoints

        /// <summary>
        /// Get inventory by ID
        /// </summary>
        /// <param name="id">Inventory ID</param>
        /// <param name="includeTransactions">Include transaction history</param>
        /// <returns>Inventory details</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryDto>> GetInventory(Guid id, [FromQuery] bool includeTransactions = false)
        {
            try
            {
                if (includeTransactions)
                {
                    var inventoryWithTransactions = await _inventoryService.GetInventoryWithTransactionsAsync(id);
                    if (inventoryWithTransactions == null)
                        return NotFound(new { message = $"Inventory {id} not found" });

                    return Ok(inventoryWithTransactions);
                }
                else
                {
                    var inventory = await _inventoryService.GetInventoryByIdAsync(id);
                    if (inventory == null)
                        return NotFound(new { message = $"Inventory {id} not found" });

                    return Ok(inventory);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving inventory", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all inventory locations for a part
        /// </summary>
        /// <param name="partId">Part ID</param>
        /// <returns>List of inventory locations</returns>
        [HttpGet("part/{partId}")]
        public async Task<ActionResult<IEnumerable<InventoryDto>>> GetInventoryByPart(Guid partId)
        {
            try
            {
                var inventories = await _inventoryService.GetInventoryByPartIdAsync(partId);
                return Ok(inventories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving inventory", error = ex.Message });
            }
        }

        /// <summary>
        /// Get inventory for part at specific warehouse
        /// </summary>
        /// <param name="partId">Part ID</param>
        /// <param name="warehouse">Warehouse name</param>
        /// <returns>Inventory details</returns>
        [HttpGet("part/{partId}/warehouse/{warehouse}")]
        public async Task<ActionResult<InventoryDto>> GetInventoryByPartAndWarehouse(Guid partId, string warehouse)
        {
            try
            {
                var inventory = await _inventoryService.GetInventoryByPartAndWarehouseAsync(partId, warehouse);
                if (inventory == null)
                    return NotFound(new { message = $"Inventory for part {partId} at warehouse '{warehouse}' not found" });

                return Ok(inventory);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving inventory", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all inventory at a warehouse
        /// </summary>
        /// <param name="warehouse">Warehouse name</param>
        /// <returns>List of inventory</returns>
        [HttpGet("warehouse/{warehouse}")]
        public async Task<ActionResult<IEnumerable<InventoryDto>>> GetInventoryByWarehouse(string warehouse)
        {
            try
            {
                var inventories = await _inventoryService.GetInventoryByWarehouseAsync(warehouse);
                return Ok(inventories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving inventory", error = ex.Message });
            }
        }

        #endregion

        #region Alert Endpoints

        /// <summary>
        /// Get low stock alerts
        /// CRITICAL FOR: Challenge 2 - Automated low stock alerts
        /// </summary>
        /// <returns>List of low stock alerts</returns>
        [HttpGet("lowstock")]
        public async Task<ActionResult<IEnumerable<LowStockAlertDto>>> GetLowStockAlerts()
        {
            try
            {
                var alerts = await _inventoryService.GetLowStockAlertsAsync();
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving low stock alerts", error = ex.Message });
            }
        }

        /// <summary>
        /// Get out of stock inventory
        /// </summary>
        /// <returns>List of out of stock inventory</returns>
        [HttpGet("outofstock")]
        public async Task<ActionResult<IEnumerable<InventoryDto>>> GetOutOfStockInventory()
        {
            try
            {
                var inventories = await _inventoryService.GetOutOfStockInventoryAsync();
                return Ok(inventories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving out of stock inventory", error = ex.Message });
            }
        }

        #endregion

        #region Reporting Endpoints

        /// <summary>
        /// Get warehouse summaries
        /// </summary>
        /// <returns>List of warehouse summaries</returns>
        [HttpGet("warehouses/summary")]
        public async Task<ActionResult<IEnumerable<WarehouseInventorySummaryDto>>> GetWarehouseSummaries()
        {
            try
            {
                var summaries = await _inventoryService.GetWarehouseSummariesAsync();
                return Ok(summaries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving warehouse summaries", error = ex.Message });
            }
        }

        /// <summary>
        /// Get summary for specific warehouse
        /// </summary>
        /// <param name="warehouse">Warehouse name</param>
        /// <returns>Warehouse summary</returns>
        [HttpGet("warehouses/{warehouse}/summary")]
        public async Task<ActionResult<WarehouseInventorySummaryDto>> GetWarehouseSummary(string warehouse)
        {
            try
            {
                var summary = await _inventoryService.GetWarehouseSummaryAsync(warehouse);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving warehouse summary", error = ex.Message });
            }
        }

        #endregion

        #region Command Endpoints - Create

        /// <summary>
        /// Create new inventory location
        /// </summary>
        /// <param name="dto">Inventory creation data</param>
        /// <returns>Created inventory</returns>
        [HttpPost]
        public async Task<ActionResult<InventoryDto>> CreateInventoryLocation([FromBody] CreateInventoryDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.CreateInventoryLocationAsync(dto);
                return CreatedAtAction(nameof(GetInventory), new { id = inventory.InventoryId }, inventory);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating inventory location", error = ex.Message });
            }
        }

        #endregion

        #region Command Endpoints - Stock Movements

        /// <summary>
        /// Reserve parts for a work order
        /// </summary>
        /// <param name="dto">Reservation data</param>
        /// <returns>Updated inventory</returns>
        [HttpPost("reserve")]
        public async Task<ActionResult<InventoryDto>> ReserveParts([FromBody] ReserveInventoryDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.ReservePartsAsync(dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error reserving parts", error = ex.Message });
            }
        }

        /// <summary>
        /// Release reserved parts
        /// </summary>
        /// <param name="dto">Release data</param>
        /// <returns>Updated inventory</returns>
        [HttpPost("release")]
        public async Task<ActionResult<InventoryDto>> ReleaseReservation([FromBody] ReleaseReservationDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.ReleaseReservationAsync(dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error releasing reservation", error = ex.Message });
            }
        }

        /// <summary>
        /// Issue parts to work order (physical consumption)
        /// </summary>
        /// <param name="dto">Issue data</param>
        /// <returns>Updated inventory</returns>
        [HttpPost("issue")]
        public async Task<ActionResult<InventoryDto>> IssueParts([FromBody] IssuePartsDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.IssuePartsAsync(dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error issuing parts", error = ex.Message });
            }
        }

        /// <summary>
        /// Receive parts from supplier
        /// </summary>
        /// <param name="dto">Receipt data</param>
        /// <returns>Updated inventory</returns>
        [HttpPost("receive")]
        public async Task<ActionResult<InventoryDto>> ReceiveParts([FromBody] ReceivePartsDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.ReceivePartsAsync(dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error receiving parts", error = ex.Message });
            }
        }

        /// <summary>
        /// Adjust inventory quantity (cycle count)
        /// </summary>
        /// <param name="dto">Adjustment data</param>
        /// <returns>Updated inventory</returns>
        [HttpPost("adjust")]
        public async Task<ActionResult<InventoryDto>> AdjustInventory([FromBody] AdjustInventoryDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.AdjustInventoryAsync(dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error adjusting inventory", error = ex.Message });
            }
        }

        #endregion

        #region Command Endpoints - Location Management

        /// <summary>
        /// Update stock levels for inventory location
        /// </summary>
        /// <param name="id">Inventory ID</param>
        /// <param name="dto">Stock level data</param>
        /// <returns>Updated inventory</returns>
        [HttpPut("{id}/stock-levels")]
        public async Task<ActionResult<InventoryDto>> UpdateStockLevels(Guid id, [FromBody] UpdateInventoryStockLevelsDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.UpdateStockLevelsAsync(id, dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating stock levels", error = ex.Message });
            }
        }

        /// <summary>
        /// Move inventory to different bin location
        /// </summary>
        /// <param name="id">Inventory ID</param>
        /// <param name="dto">Move data</param>
        /// <returns>Updated inventory</returns>
        [HttpPut("{id}/bin-location")]
        public async Task<ActionResult<InventoryDto>> MoveInventory(Guid id, [FromBody] MoveInventoryDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var inventory = await _inventoryService.MoveInventoryAsync(id, dto);
                return Ok(inventory);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error moving inventory", error = ex.Message });
            }
        }

        /// <summary>
        /// Deactivate inventory location
        /// </summary>
        /// <param name="id">Inventory ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeactivateInventory(Guid id)
        {
            try
            {
                await _inventoryService.DeactivateInventoryAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deactivating inventory", error = ex.Message });
            }
        }

        #endregion
    }
}
