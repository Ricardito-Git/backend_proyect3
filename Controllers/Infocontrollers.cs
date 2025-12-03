using Microsoft.AspNetCore.Mvc;

namespace backend_proyect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                nombre = "Backend de Ricardito",
                mensaje = "El backend está funcionando correctamente en Render.",
                estado = "OK",
                fecha = DateTime.UtcNow
            });
        }
    }
}
