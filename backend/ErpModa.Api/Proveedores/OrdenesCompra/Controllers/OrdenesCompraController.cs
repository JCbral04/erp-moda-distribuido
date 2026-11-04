using Microsoft.AspNetCore.Mvc;
using ErpModa.Api.Proveedores.OrdenesCompra.DTOs;
using ErpModa.Api.Proveedores.OrdenesCompra.Interfaces;

namespace ErpModa.Api.Proveedores.OrdenesCompra.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenesCompraController : ControllerBase
    {
        private readonly IOrdenesCompraService _service;

        public OrdenesCompraController(IOrdenesCompraService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenCompraResponseDto>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrdenCompraResponseDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound();
            return Ok(result);
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
        }
    }
}