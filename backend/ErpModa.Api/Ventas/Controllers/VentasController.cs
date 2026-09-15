using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Interfaces;

namespace ErpModa.Api.Ventas.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly IVentasService _service;

        public VentasController(IVentasService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VentaResponseDto>>> GetAll(
            [FromQuery] string? estado,
            [FromQuery] DateTime? fecha)
        {
            try
            {
                var result = await _service.GetAllAsync(estado, fecha);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("resumen")]
        public async Task<ActionResult<VentasResumenDto>> GetResumen()
        {
            var result = await _service.GetResumenAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VentaResponseDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<VentaResponseDto>> Create([FromBody] CrearVentaDto crearDto)
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

        [HttpPost("{id}/confirmar")]
        public async Task<ActionResult<VentaResponseDto>> Confirmar(int id)
        {
            try
            {
                var result = await _service.ConfirmarAsync(id);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/anular")]
        public async Task<ActionResult<VentaResponseDto>> Anular(int id, [FromBody] AnularVentaDto anularDto)
        {
            try
            {
                var result = await _service.AnularAsync(id, anularDto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}