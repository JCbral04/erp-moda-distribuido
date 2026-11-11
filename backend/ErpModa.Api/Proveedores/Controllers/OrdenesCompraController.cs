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
    public class OrdenesCompraController : ControllerBase
    {
        private readonly IOrdenesCompraService _service;

        public OrdenesCompraController(IOrdenesCompraService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenCompraResponseDto>>> GetAll(
            [FromQuery] int? proveedorId,
            [FromQuery] string? estado)
        {
            try
            {
                var result = await _service.GetAllAsync(proveedorId, estado);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al obtener las órdenes de compra.");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrdenCompraResponseDto>> GetById(int id)
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
                return StatusCode(500, "Ocurrió un error al obtener la orden de compra.");
            }
        }

        [HttpPost]
        public async Task<ActionResult<OrdenCompraResponseDto>> Create([FromBody] CrearOrdenCompraDto crearDto)
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al crear la orden de compra.");
            }
        }
    }
}
