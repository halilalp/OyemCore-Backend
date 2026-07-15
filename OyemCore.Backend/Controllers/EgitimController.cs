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
    public class EgitimController : ControllerBase
    {
        private readonly IEgitimService _egitimService;
        private readonly ITenantService _tenantService;
        private readonly IWebHostEnvironment _env;

        public EgitimController(IEgitimService egitimService, ITenantService tenantService, IWebHostEnvironment env)
        {
            _egitimService = egitimService;
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
        public IActionResult GetList([FromQuery] string search)
        {
            try
            {
                int userId = GetCurrentUserId();
                var list = _egitimService.GetEgitimList(userId, search);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try
            {
                var list = _egitimService.GetEgitimCategories();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveEgitim([FromBody] EgitimRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _egitimService.SaveEgitim(userId, request.Konu, request.Aciklama, request.KategoriID, request.DosyaUrl);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateEgitim(int id, [FromBody] EgitimRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _egitimService.UpdateEgitim(userId, id, request.Konu, request.Aciklama, request.KategoriID, request.DosyaUrl);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteEgitim(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _egitimService.DeleteEgitim(userId, id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile([FromBody] FileUploadDto dto)
        {
            string storageFolder = "";
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.FileBase64) || string.IsNullOrEmpty(dto.FileName))
                    return BadRequest(new { message = "Dosya verisi gecersiz." });

                string modulePath = _tenantService.GetModulPath("HABERDOCS");
                string ext = Path.GetExtension(dto.FileName);
                string uniqueName = $"{DateTime.Now:yyMMddHHmmssfff}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{ext}";

                if (_tenantService.IsStorageRemote())
                {
                    string remoteRelativePath = $"{modulePath}/{uniqueName}".Replace("\\", "/").Replace("//", "/");
                    var uploadResult = await _tenantService.UploadToRemoteStorageAsync(remoteRelativePath, dto.FileBase64);
                    if (!uploadResult.Success)
                        return BadRequest(new { message = $"Webportal'a yükleme başarısız: {uploadResult.Error}" });
                    return Ok(new { success = true, filePath = uniqueName, fileName = dto.FileName });
                }

                storageFolder = _tenantService.ResolveLocalStorageFolder(_env.ContentRootPath);

                string uploadDir = Path.Combine(storageFolder, modulePath);
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

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

    public class EgitimRequest
    {
        public string Konu { get; set; }
        public string? Aciklama { get; set; }
        public int KategoriID { get; set; }
        public string? DosyaUrl { get; set; }
    }
}
