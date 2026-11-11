using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;

namespace ErpModa.Api.Proveedores.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class ProveedoresController : ControllerBase
    {
        private readonly IProveedoresService _service;

        public ProveedoresController(IProveedoresService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProveedorResponseDto>>> GetAll([FromQuery] bool? activo)
        {
            try
            {
                var result = await _service.GetAllAsync(activo);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al obtener los proveedores.");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProveedorResponseDto>> GetById(int id)
        {
            try
            {
                var result = await _service.GetByIdAsync(id);
                if (result == null)
                    return NotFound();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al obtener el proveedor.");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ProveedorResponseDto>> Create([FromBody] CrearProveedorDto crearDto)
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
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al crear el proveedor.");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ProveedorResponseDto>> Update(int id, [FromBody] ActualizarProveedorDto actualizarDto)
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
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al actualizar el proveedor.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);
                if (!deleted)
                    return NotFound();
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al eliminar el proveedor.");
            }
        }
    }
}
