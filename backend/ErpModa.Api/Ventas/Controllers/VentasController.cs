using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Exceptions;
using ErpModa.Api.Ventas.Interfaces;

namespace ErpModa.Api.Ventas.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly IVentasService _service;

        public VentasController(IVentasService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        // Issue #83: la identidad estable del JWT (claim NameIdentifier) se pasa al
        // servicio para que aplique ownership. El controlador NO decide quién puede
        // ver cada venta: solo traduce el token a los argumentos del servicio.
        private string? UsuarioActual => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private bool EsAdministrador => User.IsInRole("Administrador");

        [HttpGet]
        [Authorize(Policy = "VentasConsulta")]
        public async Task<ActionResult<IEnumerable<VentaResponseDto>>> GetAll(
            [FromQuery] string? estado,
            [FromQuery] DateTime? fecha)
        {
            try
            {
                var result = await _service.GetAllAsync(estado, fecha, UsuarioActual, EsAdministrador);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("resumen")]
        [Authorize(Policy = "Administrador")]
        public async Task<ActionResult<VentasResumenDto>> GetResumen()
        {
            var result = await _service.GetResumenAsync(UsuarioActual, EsAdministrador);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "VentasConsulta")]
        public async Task<ActionResult<VentaResponseDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id, UsuarioActual, EsAdministrador);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "VentasEscritura")]
        public async Task<ActionResult<VentaResponseDto>> Create([FromBody] CrearVentaDto crearDto)
        {
            try
            {
                var created = await _service.CreateAsync(crearDto, UsuarioActual);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/confirmar")]
        [Authorize(Policy = "VentasEscritura")]
        public async Task<ActionResult<VentaResponseDto>> Confirmar(int id)
        {
            try
            {
                var result = await _service.ConfirmarAsync(id, UsuarioActual, EsAdministrador);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (StockInsuficienteException ex)
            {
                return Conflict(new
                {
                    codigo = "stock_insuficiente",
                    varianteId = ex.VarianteId,
                    disponible = ex.Disponible,
                    solicitado = ex.Solicitado,
                    mensaje = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/anular")]
        [Authorize(Policy = "VentasEscritura")]
        public async Task<ActionResult<VentaResponseDto>> Anular(int id, [FromBody] AnularVentaDto anularDto)
        {
            try
            {
                var result = await _service.AnularAsync(id, anularDto, UsuarioActual, EsAdministrador);
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