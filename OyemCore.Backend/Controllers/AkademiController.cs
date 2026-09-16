using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Akademi — Faz 1: içerik yönetimi, atama, gerçek izleme takibi.
    // Mevcut "Eğitimler" (EgitimController / tb_Egitim) modülüne dokunmuyor, tamamen ayrı.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AkademiController : ControllerBase
    {
        private readonly IAkademiService _akademiService;
        private readonly ITenantService _tenantService;
        private readonly IWebHostEnvironment _env;

        public AkademiController(IAkademiService akademiService, ITenantService tenantService, IWebHostEnvironment env)
        {
            _akademiService = akademiService;
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

        private string GetCurrentSicilNo()
        {
            var claim = User.FindFirst("SicilNo");
            if (claim != null) return claim.Value;
            throw new UnauthorizedAccessException("Giris yapan kullanicinin Sicil Numarasi bulunamadi.");
        }

        // ── İçerik (İK yönetimi) ──

        [HttpGet("content")]
        public IActionResult GetContentList()
        {
            try { return Ok(_akademiService.GetContentList(GetCurrentUserId())); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("content")]
        public IActionResult SaveContent([FromBody] AkademiContentRequest req)
        {
            try
            {
                bool success = _akademiService.SaveContent(GetCurrentUserId(), req.Baslik, req.Aciklama, req.KategoriKodu, req.IcerikTipi, req.DosyaUrl, req.SureSaniye);
                return Ok(new { success });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("content/{id}")]
        public IActionResult UpdateContent(int id, [FromBody] AkademiContentRequest req)
        {
            try
            {
                bool success = _akademiService.UpdateContent(GetCurrentUserId(), id, req.Baslik, req.Aciklama, req.KategoriKodu, req.DosyaUrl, req.SureSaniye);
                return Ok(new { success });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("content/{id}/active")]
        public IActionResult SetContentActive(int id, [FromBody] AkademiActiveRequest req)
        {
            try
            {
                bool success = _akademiService.SetContentActive(GetCurrentUserId(), id, req.AktifMi);
                return Ok(new { success });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ── Atama (İK yönetimi) ──

        [HttpPost("content/{id}/assign")]
        public IActionResult AssignToPersonnel(int id, [FromBody] AkademiAssignRequest req)
        {
            try
            {
                int eklenen = _akademiService.AssignToPersonnel(GetCurrentUserId(), id, req.SicilNoList, req.SonTarih, req.ZorunluMu, req.AktifIzlemeZorunlu);
                return Ok(new { success = true, eklenen });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("content/{id}/report")]
        public IActionResult GetAssignmentReport(int id)
        {
            try { return Ok(_akademiService.GetAssignmentReport(id)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ── Personel tarafı (web + mobil ortak) ──

        [HttpGet("my")]
        public IActionResult GetMyAssignments()
        {
            try { return Ok(_akademiService.GetMyAssignments(GetCurrentSicilNo())); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("{atamaId}")]
        public IActionResult GetAssignmentDetail(int atamaId)
        {
            try { return Ok(_akademiService.GetAssignmentDetail(atamaId, GetCurrentSicilNo())); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{atamaId}/progress")]
        public IActionResult UpdateProgress(int atamaId, [FromBody] AkademiProgressRequest req)
        {
            try
            {
                bool success = _akademiService.UpdateProgress(atamaId, GetCurrentSicilNo(), req.MaxIzlenenSaniye, req.AktifIzlemeSaniyeArtis, req.TamamlaZorla);
                return Ok(new { success });
            }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ── Kategori ──

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try { return Ok(_akademiService.GetCategories()); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("categories")]
        public IActionResult AddCategory([FromBody] AkademiCategoryRequest req)
        {
            try { return Ok(new { success = _akademiService.AddCategory(GetCurrentUserId(), req.Ad) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ── Faz 2 — Sınav ayarları + soru bankası (İK yönetimi) ──

        [HttpPut("content/{id}/exam-settings")]
        public IActionResult SaveExamSettings(int id, [FromBody] AkademiExamSettingsRequest req)
        {
            try
            {
                bool success = _akademiService.SaveExamSettings(GetCurrentUserId(), id, req.SinavAktif, req.SoruSayisi, req.SoruSuresiSaniye, req.GecmePuanYuzdesi);
                return Ok(new { success });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("content/{id}/questions")]
        public IActionResult GetQuestions(int id)
        {
            try { return Ok(_akademiService.GetQuestions(id)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("content/{id}/questions")]
        public IActionResult SaveQuestion(int id, [FromBody] AkademiQuestionRequest req)
        {
            try
            {
                bool success = _akademiService.SaveQuestion(GetCurrentUserId(), id, req.SoruMetni, req.SecenekA, req.SecenekB, req.SecenekC, req.SecenekD, req.DogruSecenek, req.Puan);
                return Ok(new { success });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("questions/{soruId}")]
        public IActionResult UpdateQuestion(int soruId, [FromBody] AkademiQuestionRequest req)
        {
            try
            {
                bool success = _akademiService.UpdateQuestion(GetCurrentUserId(), soruId, req.SoruMetni, req.SecenekA, req.SecenekB, req.SecenekC, req.SecenekD, req.DogruSecenek, req.Puan);
                return Ok(new { success });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("questions/{soruId}")]
        public IActionResult DeleteQuestion(int soruId)
        {
            try { return Ok(new { success = _akademiService.DeleteQuestion(GetCurrentUserId(), soruId) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("content/{id}/questions/bulk")]
        public IActionResult ImportQuestionsBulk(int id, [FromBody] AkademiQuestionBulkRequest req)
        {
            try
            {
                var rows = req.Rows?.ConvertAll(r => (r.SoruMetni, r.SecenekA, r.SecenekB, r.SecenekC, r.SecenekD, r.DogruSecenek, r.Puan)) ?? new List<(string, string, string, string?, string?, string, int)>();
                int eklenen = _akademiService.ImportQuestionsBulk(GetCurrentUserId(), id, rows);
                return Ok(new { success = true, eklenen });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ── Faz 2 — Sınav oturumu (personel tarafı) ──

        [HttpPost("{atamaId}/exam/start")]
        public IActionResult StartOrResumeExam(int atamaId)
        {
            try { return Ok(_akademiService.StartOrResumeExam(atamaId, GetCurrentSicilNo())); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{atamaId}/exam/answer")]
        public IActionResult SubmitAnswer(int atamaId, [FromBody] AkademiExamAnswerRequest req)
        {
            try { return Ok(_akademiService.SubmitAnswer(atamaId, GetCurrentSicilNo(), req.SecilenSecenek)); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{atamaId}/exam/tab-switch")]
        public IActionResult ReportTabSwitch(int atamaId)
        {
            try { return Ok(new { success = _akademiService.ReportTabSwitch(atamaId, GetCurrentSicilNo()) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("{atamaId}/exam/result")]
        public IActionResult GetExamResult(int atamaId)
        {
            try
            {
                var sonuc = _akademiService.GetExamResult(atamaId, GetCurrentSicilNo());
                if (sonuc == null) return NotFound();
                return Ok(sonuc);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ── Dosya yükleme (aynı Storage:Modules deseni, modül anahtarı "AKADEMI") ──

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile([FromBody] AkademiFileUploadRequest dto)
        {
            string storageFolder = "";
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.FileBase64) || string.IsNullOrEmpty(dto.FileName))
                    return BadRequest(new { message = "Dosya verisi gecersiz." });

                string modulePath = _tenantService.GetModulPath("AKADEMI");
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

    public class AkademiContentRequest
    {
        public string Baslik { get; set; }
        public string Aciklama { get; set; }
        public string KategoriKodu { get; set; }
        public string IcerikTipi { get; set; }
        public string DosyaUrl { get; set; }
        public int? SureSaniye { get; set; }
    }

    public class AkademiActiveRequest
    {
        public bool AktifMi { get; set; }
    }

    public class AkademiAssignRequest
    {
        public List<string> SicilNoList { get; set; }
        public DateTime? SonTarih { get; set; }
        public bool ZorunluMu { get; set; } = true;
        public bool AktifIzlemeZorunlu { get; set; } = true;
    }

    public class AkademiProgressRequest
    {
        public int MaxIzlenenSaniye { get; set; }
        public int AktifIzlemeSaniyeArtis { get; set; }
        public bool TamamlaZorla { get; set; }
    }

    public class AkademiFileUploadRequest
    {
        public string FileBase64 { get; set; }
        public string FileName { get; set; }
    }

    public class AkademiCategoryRequest
    {
        public string Ad { get; set; }
    }

    public class AkademiExamSettingsRequest
    {
        public bool SinavAktif { get; set; }
        public int? SoruSayisi { get; set; }
        public int SoruSuresiSaniye { get; set; } = 60;
        public int GecmePuanYuzdesi { get; set; } = 70;
    }

    public class AkademiQuestionRequest
    {
        public string SoruMetni { get; set; }
        public string SecenekA { get; set; }
        public string SecenekB { get; set; }
        public string SecenekC { get; set; }
        public string SecenekD { get; set; }
        public string DogruSecenek { get; set; }
        public int Puan { get; set; } = 1;
    }

    public class AkademiQuestionBulkRequest
    {
        public List<AkademiQuestionRequest> Rows { get; set; }
    }

    public class AkademiExamAnswerRequest
    {
        public string SecilenSecenek { get; set; }
    }
}
