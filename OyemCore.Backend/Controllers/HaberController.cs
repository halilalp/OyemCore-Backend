using System;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class HaberController : ControllerBase
    {
        private readonly IHaberService _haberService;
        private readonly ITenantService _tenantService;
        private readonly IWebHostEnvironment _env;

        public HaberController(IHaberService haberService, ITenantService tenantService, IWebHostEnvironment env)
        {
            _haberService = haberService;
            _tenantService = tenantService;
            _env = env;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        [HttpGet]
        public IActionResult GetList([FromQuery] string search, [FromQuery] string startDate, [FromQuery] string endDate)
        {
            try
            {
                int userId = GetCurrentUserId();
                var list = _haberService.GetHaberList(userId, search, startDate, endDate);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetDetail(int id)
        {
            try
            {
                var detail = _haberService.GetHaberDetail(id);
                if (detail == null) return NotFound(new { message = "Haber bulunamadi." });
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveHaber([FromBody] HaberRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _haberService.SaveHaber(userId, request.Konu, request.Aciklama, request.ProfilUrl);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateHaber(int id, [FromBody] HaberRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _haberService.UpdateHaber(userId, id, request.Konu, request.Aciklama, request.ProfilUrl);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteHaber(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _haberService.DeleteHaber(userId, id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("upload-file")]
        public IActionResult UploadFile([FromBody] FileUploadDto dto)
        {
            string storageFolder = "";
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.FileBase64) || string.IsNullOrEmpty(dto.FileName))
                    return BadRequest(new { message = "Dosya verisi gecersiz." });

                storageFolder = _tenantService.GetCurrentStorageFolder() ?? "";
                if (string.IsNullOrEmpty(storageFolder))
                    storageFolder = Path.Combine(_env.ContentRootPath, "wwwroot");
                else if (!Path.IsPathRooted(storageFolder))
                    storageFolder = Path.GetFullPath(Path.Combine(_env.ContentRootPath, storageFolder));

                string modulePath = _tenantService.GetModulPath("HABERIMG");
                string uploadDir = Path.Combine(storageFolder, modulePath);
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                string ext = Path.GetExtension(dto.FileName);
                string uniqueName = $"{DateTime.Now:yyMMddHHmmssfff}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{ext}";
                string fullPath = Path.Combine(uploadDir, uniqueName);

                byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);
                System.IO.File.WriteAllBytes(fullPath, fileBytes);

                return Ok(new { success = true, filePath = uniqueName, fileName = dto.FileName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hata: {ex.Message}. Klasor: {storageFolder}" });
            }
        }
    }

    public class HaberRequest
    {
        public string Konu { get; set; }
        public string? Aciklama { get; set; }
        public string? ProfilUrl { get; set; }
    }
}
