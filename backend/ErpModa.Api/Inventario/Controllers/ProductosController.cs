using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpModa.Api.Inventario.DTOs;
using ErpModa.Api.Inventario.Interfaces;

namespace ErpModa.Api.Inventario.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly IProductosService _service;

        public ProductosController(IProductosService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        [Authorize(Policy = "InventarioConsulta")]
        public async Task<ActionResult<IEnumerable<ProductoResponseDto>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "InventarioConsulta")]
        public async Task<ActionResult<ProductoResponseDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "InventarioEscritura")]
        public async Task<ActionResult<ProductoResponseDto>> Create([FromBody] CrearProductoDto crearDto)
        {
            try
            {
                var created = await _service.CreateAsync(crearDto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "InventarioEscritura")]
        public async Task<ActionResult<ProductoResponseDto>> Update(int id, [FromBody] ActualizarProductoDto actualizarDto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, actualizarDto);
                if (updated == null)
                    return NotFound();
                return Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "InventarioEscritura")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}
