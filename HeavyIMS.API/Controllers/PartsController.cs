using HeavyIMS.Application.DTOs;
using HeavyIMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavyIMS.API.Controllers
{
    /// <summary>
    /// API Controller: Part Catalog Management
    /// DEMONSTRATES: RESTful API design for catalog operations
    /// ENDPOINTS: CRUD operations for parts
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PartsController : ControllerBase
    {
        private readonly IPartService _partService;

        public PartsController(IPartService partService)
        {
            _partService = partService ?? throw new ArgumentNullException(nameof(partService));
        }

        /// <summary>
        /// Get all parts
        /// </summary>
        /// <param name="search">Optional search term</param>
        /// <param name="category">Optional category filter</param>
        /// <param name="activeOnly">Show only active parts</param>
        /// <returns>List of parts</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PartDto>>> GetParts(
            [FromQuery] string search = null,
            [FromQuery] string category = null,
            [FromQuery] bool activeOnly = false)
        {
            try
            {
                IEnumerable<PartDto> parts;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    parts = await _partService.SearchPartsAsync(search);
                }
                else if (!string.IsNullOrWhiteSpace(category))
                {
                    parts = await _partService.GetPartsByCategoryAsync(category);
                }
                else if (activeOnly)
                {
                    parts = await _partService.GetActivePartsAsync();
                }
                else
                {
                    parts = await _partService.GetAllPartsAsync();
                }

                return Ok(parts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving parts", error = ex.Message });
            }
        }

        /// <summary>
        /// Get part by ID with inventory details
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <param name="includeInventory">Include inventory locations</param>
        /// <returns>Part details</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<PartWithInventoryDto>> GetPart(Guid id, [FromQuery] bool includeInventory = true)
        {
            try
            {
                if (includeInventory)
                {
                    var partWithInventory = await _partService.GetPartWithInventoryAsync(id);
                    if (partWithInventory == null)
                        return NotFound(new { message = $"Part {id} not found" });

                    return Ok(partWithInventory);
                }
                else
                {
                    var part = await _partService.GetPartByIdAsync(id);
                    if (part == null)
                        return NotFound(new { message = $"Part {id} not found" });

                    return Ok(part);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving part", error = ex.Message });
            }
        }

        /// <summary>
        /// Get part by part number
        /// </summary>
        /// <param name="partNumber">Part number</param>
        /// <returns>Part details</returns>
        [HttpGet("by-part-number/{partNumber}")]
        public async Task<ActionResult<PartDto>> GetPartByPartNumber(string partNumber)
        {
            try
            {
                var part = await _partService.GetPartByPartNumberAsync(partNumber);
                if (part == null)
                    return NotFound(new { message = $"Part '{partNumber}' not found" });

                return Ok(part);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving part", error = ex.Message });
            }
        }

        /// <summary>
        /// Get discontinued parts
        /// </summary>
        /// <returns>List of discontinued parts</returns>
        [HttpGet("discontinued")]
        public async Task<ActionResult<IEnumerable<PartDto>>> GetDiscontinuedParts()
        {
            try
            {
                var parts = await _partService.GetDiscontinuedPartsAsync();
                return Ok(parts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving parts", error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new part
        /// </summary>
        /// <param name="dto">Part creation data</param>
        /// <returns>Created part</returns>
        [HttpPost]
        public async Task<ActionResult<PartDto>> CreatePart([FromBody] CreatePartDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var part = await _partService.CreatePartAsync(dto);
                return CreatedAtAction(nameof(GetPart), new { id = part.PartId }, part);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating part", error = ex.Message });
            }
        }

        /// <summary>
        /// Update part information
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <param name="dto">Update data</param>
        /// <returns>Updated part</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<PartDto>> UpdatePart(Guid id, [FromBody] UpdatePartDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var part = await _partService.UpdatePartAsync(id, dto);
                return Ok(part);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating part", error = ex.Message });
            }
        }

        /// <summary>
        /// Update part pricing
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <param name="dto">Pricing data</param>
        /// <returns>Updated part</returns>
        [HttpPut("{id}/pricing")]
        public async Task<ActionResult<PartDto>> UpdatePricing(Guid id, [FromBody] UpdatePartPricingDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var part = await _partService.UpdatePricingAsync(id, dto);
                return Ok(part);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating pricing", error = ex.Message });
            }
        }

        /// <summary>
        /// Update part supplier information
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <param name="dto">Supplier data</param>
        /// <returns>Updated part</returns>
        [HttpPut("{id}/supplier")]
        public async Task<ActionResult<PartDto>> UpdateSupplier(Guid id, [FromBody] UpdatePartSupplierDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var part = await _partService.UpdateSupplierAsync(id, dto);
                return Ok(part);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating supplier", error = ex.Message });
            }
        }

        /// <summary>
        /// Update default stock levels
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <param name="dto">Stock level data</param>
        /// <returns>Updated part</returns>
        [HttpPut("{id}/stock-levels")]
        public async Task<ActionResult<PartDto>> UpdateStockLevels(Guid id, [FromBody] UpdatePartStockLevelsDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var part = await _partService.UpdateStockLevelsAsync(id, dto);
                return Ok(part);
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
        /// Discontinue a part
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <returns>Updated part</returns>
        [HttpPost("{id}/discontinue")]
        public async Task<ActionResult<PartDto>> DiscontinuePart(Guid id)
        {
            try
            {
                var part = await _partService.DiscontinuePartAsync(id);
                return Ok(part);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error discontinuing part", error = ex.Message });
            }
        }

        /// <summary>
        /// Reactivate a discontinued part
        /// </summary>
        /// <param name="id">Part ID</param>
        /// <returns>Updated part</returns>
        [HttpPost("{id}/reactivate")]
        public async Task<ActionResult<PartDto>> ReactivatePart(Guid id)
        {
            try
            {
                var part = await _partService.ReactivatePartAsync(id);
                return Ok(part);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error reactivating part", error = ex.Message });
            }
        }
    }
}
