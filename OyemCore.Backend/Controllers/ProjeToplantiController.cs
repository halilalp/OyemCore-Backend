using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Proje / Toplantı yönetimi. Faz 1 — liste + detay.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProjeToplantiController : ControllerBase
    {
        private readonly IProjeToplantiService _service;

        public ProjeToplantiController(IProjeToplantiService service)
        {
            _service = service;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        [HttpGet]
        public IActionResult GetList([FromQuery] string konu, [FromQuery] string durum, [FromQuery] string tur)
        {
            try { return Ok(new { data = _service.GetList(GetUserId(), konu, durum, tur) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("{id}")]
        public IActionResult GetDetail(int id)
        {
            try
            {
                var detay = _service.GetDetail(GetUserId(), id);
                if (detay == null) return NotFound(new { message = "Kayıt bulunamadı." });
                return Ok(detay);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
    }
}
