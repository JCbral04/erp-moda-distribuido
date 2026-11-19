using Microsoft.AspNetCore.Mvc;
using ErpModa.Api.Inventario.DTOs;
using ErpModa.Api.Inventario.Filters;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Services;

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
        public async Task<ActionResult<IEnumerable<ProductoResponseDto>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductoResponseDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpPost]
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

        // Lo llama el lector de QR (ai/deteccion.py): suma una unidad al stock de la variante.
        // Idempotente por idEscaneo: un reintento con el mismo id devuelve 200 con
        // duplicado = true sin sumar otra unidad. Restringido por red hasta #84.
        [HttpPost("escaneo-qr")]
        [RedPermitidaEscaneoQr]
        public async Task<ActionResult<EscaneoQrResponseDto>> RegistrarEscaneoQr([FromBody] EscaneoQrDto escaneoDto)
        {
            try
            {
                var result = await _service.RegistrarEscaneoQrAsync(escaneoDto.Sku, escaneoDto.IdEscaneo);
                if (result == null)
                    return NotFound($"No existe una variante con SKU '{escaneoDto.Sku.Trim()}'.");
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (EscaneoQrConflictoException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}
