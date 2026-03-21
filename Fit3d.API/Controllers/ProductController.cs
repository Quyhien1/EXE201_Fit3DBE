using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Fit3d.BLL.DTOs;
using Fit3d.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fit3d.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _service;

        public ProductController(IProductService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaging([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null)
        {
            var result = await _service.GetPagingAsync(page, size, search, categoryId);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromForm] CreateProductDTO createDto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _service.CreateAsync(createDto, userId.Value);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateProductDTO updateDto)
        {
            var result = await _service.UpdateAsync(id, updateDto);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpPost("{id}/colors")]
        public async Task<IActionResult> AddColor(Guid id, [FromBody] CreateProductColorDTO colorDto)
        {
            var result = await _service.AddColorAsync(id, colorDto);
            if (result == null) return BadRequest("Could not add color. Product might not exist.");
            return Ok(result);
        }

        [HttpDelete("colors/{colorId}")]
        public async Task<IActionResult> DeleteColor(Guid colorId)
        {
            var result = await _service.DeleteColorAsync(colorId);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpPost("{id}/sizes")]
        public async Task<IActionResult> AddSize(Guid id, [FromBody] CreateProductSizeDTO sizeDto)
        {
            var result = await _service.AddSizeAsync(id, sizeDto);
            if (result == null) return BadRequest("Could not add size. Product might not exist.");
            return Ok(result);
        }

        [HttpDelete("sizes/{sizeId}")]
        public async Task<IActionResult> DeleteSize(Guid sizeId)
        {
            var result = await _service.DeleteSizeAsync(sizeId);
            if (!result) return NotFound();
            return NoContent();
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            return userId;
        }
    }
}