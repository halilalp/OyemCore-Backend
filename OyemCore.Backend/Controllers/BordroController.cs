using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    [Route("api/bordro")]
    [ApiController]
    [Authorize]
    public class BordroController : ControllerBase
    {
        private readonly IBordroService _bordroService;

        public BordroController(IBordroService bordroService)
        {
            _bordroService = bordroService;
        }

        [HttpGet("list")]
        public IActionResult GetList([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int kullaniciID))
                {
                    return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı." });
                }

                var (total, data) = _bordroService.GetBordroListesiUser(kullaniciID, pageIndex, pageSize);
                return Ok(new { success = true, data = data, total = total });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("action")]
        public IActionResult SaveAction([FromQuery] int bordroID, [FromQuery] string aksiyon)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int kullaniciID))
                {
                    return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı." });
                }

                bool result = _bordroService.BordroAksiyonKaydet(kullaniciID, bordroID, aksiyon);
                if (result)
                {
                    return Ok(new { success = true, message = "Aksiyon başarıyla kaydedildi." });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Aksiyon kaydedilirken hata oluştu." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
