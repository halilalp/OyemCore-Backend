using System;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.BusinessLayer.Dtos;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TalepController : ControllerBase
    {
        private readonly ITalepService _talepService;
        private readonly ITenantService _tenantService;
        private readonly IWebHostEnvironment _env;

        public TalepController(ITalepService talepService, ITenantService tenantService, IWebHostEnvironment env)
        {
            _talepService = talepService;
            _tenantService = tenantService;
            _env = env;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        /// <summary>
        /// Belirli bir talep t?r?ne (IT, ERP, BAKIM vb.) g?re kullanicinin iliskili oldugu talepleri listeler.
        /// </summary>
        /// <param name="tur">Talep t?r? filtre degeri (IT, ERP, BAKIM vb.).</param>
        /// <returns>Taleplerin listesini d?ner.</returns>
        [HttpGet]
        public IActionResult GetRequests([FromQuery] string tur)
        {
            try
            {
                if (string.IsNullOrEmpty(tur))
                {
                    return BadRequest(new { message = "Talep t?r? belirtilmelidir (IT, ERP)." });
                }
                int userId = GetCurrentUserId();
                var list = _talepService.GetRequests(userId, tur);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen talep t?r?ne ait kategori tanimlarini getirir.
        /// </summary>
        /// <param name="tur">Talep t?r? filtre degeri.</param>
        /// <returns>Kategori tanimlari listesini d?ner.</returns>
        [HttpGet("categories")]
        public IActionResult GetCategories([FromQuery] string tur)
        {
            try
            {
                if (string.IsNullOrEmpty(tur))
                {
                    return BadRequest(new { message = "Talep t?r? belirtilmelidir." });
                }
                var list = _talepService.GetCategories(tur);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen ID degerine sahip talebin detay bilgilerini getirir.
        /// </summary>
        /// <param name="id">Talebin benzersiz ID degeri.</param>
        /// <returns>Talep detay ve tarih?e verilerini d?ner.</returns>
        [HttpGet("{id}")]
        public IActionResult GetDetail(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                var detail = _talepService.GetRequestDetail(userId, id);
                if (detail == null)
                {
                    return NotFound(new { message = "Talep bulunamadi." });
                }
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebin kilit durumunu degistirir (Kilitler veya kilidi kaldirir).
        /// </summary>
        /// <param name="id">Islem yapilacak talebin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/lock")]
        public IActionResult ToggleLock(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _talepService.ToggleRequestLock(userId, id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebi onaylamasi i?in belirlenen amire g?nderir.
        /// </summary>
        /// <param name="id">Onaya g?nderilecek talebin ID degeri.</param>
        /// <param name="request">Onaylayacak amirin sicil numarasini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/send-approval")]
        public IActionResult SendApproval(int id, [FromBody] SendApprovalRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.AmirSicil))
                {
                    return BadRequest(new { message = "Amir sicil numarasi belirtilmelidir." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.SendRequestForApproval(userId, id, request.AmirSicil);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Onay s?recindeki bir talebin onay istegini geri ?eker.
        /// </summary>
        /// <param name="id">Onay istegi geri ?ekilecek talebin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/retract-approval")]
        public IActionResult RetractApproval(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _talepService.RetractRequestApproval(userId, id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Onay asamasindaki bir talebi onaylar veya reddeder.
        /// </summary>
        /// <param name="id">Onaylanacak veya reddedilecek talebin ID degeri.</param>
        /// <param name="request">Onay/red durumu ve a?iklama yorumunu i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/approve-reject")]
        public IActionResult ApproveReject(int id, [FromBody] ApproveRejectRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Ge?ersiz istek verisi." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.ApproveOrRejectRequest(userId, id, request.Approve, request.Comment);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talep ile ilgili bir baska personele soru sorar ve not birakir.
        /// </summary>
        /// <param name="id">Soru sorulacak talebin ID degeri.</param>
        /// <param name="request">Soru sorulacak personel sicili ve soru metnini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/ask-question")]
        public IActionResult AskQuestion(int id, [FromBody] AskQuestionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.TargetSicil) || string.IsNullOrEmpty(request.QuestionText))
                {
                    return BadRequest(new { message = "Soru sorulacak personel ve soru metni bos birakilamaz." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.AskQuestionToPersonnel(userId, id, request.TargetSicil, request.QuestionText);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebe yardimci olarak ?alisacak ek personel ekler.
        /// </summary>
        /// <param name="id">Yardimci eklenecek talebin ID degeri.</param>
        /// <param name="request">Eklenecek yardimci personelin sicil numarasini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/helpers")]
        public IActionResult AddHelper(int id, [FromBody] AddHelperRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.HelperSicil))
                {
                    return BadRequest(new { message = "Yardimci personel sicil numarasi belirtilmelidir." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.AddHelperPersonnel(userId, id, request.HelperSicil);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebe atanmis olan yardimci personeli talepten kaldirir.
        /// </summary>
        /// <param name="id">Islem yapilacak talebin ID degeri.</param>
        /// <param name="sicilNo">Kaldirilacak yardimci personelin sicil numarasi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("{id}/helpers/{sicilNo}")]
        public IActionResult DeleteHelper(int id, string sicilNo)
        {
            try
            {
                if (string.IsNullOrEmpty(sicilNo))
                {
                    return BadRequest(new { message = "Yardimci personel sicil numarasi belirtilmelidir." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.DeleteHelperPersonnel(userId, id, sicilNo);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Sistemdeki t?m aktif personellerin listesini getirir.
        /// </summary>
        /// <returns>Aktif personel listesini d?ner.</returns>
        [HttpGet("all-personnel")]
        public IActionResult GetAllActivePersonnel()
        {
            try
            {
                var list = _talepService.GetAllActivePersonel();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir talep kaydeder veya mevcut talebi g?nceller.
        /// </summary>
        /// <param name="dto">Talep ve bakim bilgilerini i?eren veri transfer nesnesi.</param>
        /// <returns>Kayit basarili ise olusturulan talep kodunu ve basari durumunu d?ner.</returns>
        [HttpPost]
        public IActionResult SaveRequest([FromBody] SaveTalepRequestDto dto)
        {
            try
            {
                if (dto == null || dto.Talep == null)
                {
                    return BadRequest(new { message = "Ge?ersiz talep verisi." });
                }
                int userId = GetCurrentUserId();
                string code = _talepService.SaveRequest(userId, dto.Talep, dto.Bakim);
                return Ok(new { success = true, code, message = "Talep basariyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebin durumunu (A?ik, Kapali, Beklemede vb.) g?nceller.
        /// </summary>
        /// <param name="id">Durumu g?ncellenecek talebin ID degeri.</param>
        /// <param name="request">Yeni durum bilgisini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/status")]
        public IActionResult UpdateStatus(int id, [FromBody] UpdateTalepStatusRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Status))
                {
                    return BadRequest(new { message = "Ge?ersiz durum verisi." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.UpdateRequestStatus(userId, id, request.Status);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebi isleme almasi i?in belirli bir personele atar.
        /// </summary>
        /// <param name="id">Atama yapilacak talebin ID degeri.</param>
        /// <param name="request">Atanacak personelin sicil numarasini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/assign")]
        public IActionResult Assign(int id, [FromBody] AssignTalepRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.SicilNo))
                {
                    return BadRequest(new { message = "Ge?ersiz sicil numarasi." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.AssignRequest(userId, id, request.SicilNo);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebe dair yeni bir gelisme notu veya a?iklama ekler.
        /// </summary>
        /// <param name="id">Gelisme eklenecek talebin ID degeri.</param>
        /// <param name="request">Eklenmek istenen a?iklama metnini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/gelisme")]
        public IActionResult AddGelisme(int id, [FromBody] AddGelismeRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Aciklama))
                {
                    return BadRequest(new { message = "A?iklama bos birakilamaz." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.AddRequestGelisme(userId, id, request.Aciklama, request.DosyaUrl);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Helpdesk modülü için base64 formatında dosya yükler.
        /// </summary>
        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile([FromBody] FileUploadDto dto)
        {
            string storageFolder = "";
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.FileBase64) || string.IsNullOrEmpty(dto.FileName))
                {
                    return BadRequest(new { message = "Dosya verisi gecersiz." });
                }

                // Resolve path from the standardized mapping ruleset
                string modulePath = _tenantService.GetModulPath(dto.Module ?? "");
                string ext = Path.GetExtension(dto.FileName);
                string uniqueName = $"{DateTime.Now:yyMMddHHmmssfff}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{ext}";

                if (_tenantService.IsStorageRemote())
                {
                    string remoteRelativePath = $"{modulePath}/{uniqueName}".Replace("\\", "/").Replace("//", "/");
                    var result = await _tenantService.UploadToRemoteStorageAsync(remoteRelativePath, dto.FileBase64);
                    if (!result.Success)
                    {
                        return BadRequest(new { message = $"Webportal'a yükleme başarısız: {result.Error}" });
                    }
                    return Ok(new { success = true, filePath = $"/{result.RelativePath}", fileName = dto.FileName });
                }

                storageFolder = _tenantService.ResolveLocalStorageFolder(_env.ContentRootPath);

                string uploadDir = Path.Combine(storageFolder, modulePath);
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                string fullPath = Path.Combine(uploadDir, uniqueName);

                byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);
                System.IO.File.WriteAllBytes(fullPath, fileBytes);

                // Construct relative URL starting with a slash
                string relativeUrl = $"/{modulePath}/{uniqueName}".Replace("\\", "/").Replace("//", "/");
                return Ok(new { success = true, filePath = relativeUrl, fileName = dto.FileName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hata: {ex.Message}. Klasör: {storageFolder}" });
            }
        }

        /// <summary>
        /// Belirtilen talep t?r?ne g?re atama yapilabilecek yetkili personellerin listesini getirir.
        /// </summary>
        /// <param name="tur">Talep t?r? (IT, ERP, BAKIM vb.).</param>
        /// <returns>Atanabilir personel listesini d?ner.</returns>
        [HttpGet("personels")]
        public IActionResult GetPersonels([FromQuery] string tur)
        {
            try
            {
                if (string.IsNullOrEmpty(tur))
                {
                    return BadRequest(new { message = "Talep t?r? belirtilmelidir." });
                }
                var list = _talepService.GetPersonels(tur);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{talepKodu}/kontrol-kaydet")]
        public IActionResult TalepKontrolKaydet(string talepKodu, [FromBody] TalepKontrolRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool result = _talepService.TalepKontrolKaydet(userId, talepKodu, request.EksikSomun, request.Yag, request.Miknatis, request.FazlaParca, request.Guvenlik, request.Makine, request.Temizlik, request.Gida);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("is-emri-turleri")]
        public IActionResult GetIsEmriTurleri()
        {
            return Ok(_talepService.GetIsEmriTurleri());
        }

        [HttpPost("{talepKodu}/is-emri-kaydet")]
        public IActionResult IsEmriKaydet(string talepKodu, [FromBody] IsEmriKaydetRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool result = _talepService.IsEmriKaydet(userId, talepKodu, request.IsEmriTurID, request.TerminTar, request.Aciklama, request.DosyaUrl);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("is-emri-kapat/{isEmriID}")]
        public IActionResult IsEmriKapat(int isEmriID, [FromBody] IsEmriKapatRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool result = _talepService.IsEmriKapat(userId, isEmriID, request.Aciklama);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("is-emri-aksiyon/{isEmriID}")]
        public IActionResult IsEmriAksiyonGonder(int isEmriID, [FromBody] IsEmriAksiyonRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool result = _talepService.IsEmriAksiyonGonder(userId, isEmriID, request.Sicil);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class SaveTalepRequestDto
    {
        public tb_Talep Talep { get; set; }
        public tb_TalepBakim Bakim { get; set; }
    }

    public class UpdateTalepStatusRequest
    {
        public string Status { get; set; }
    }

    public class AssignTalepRequest
    {
        public string SicilNo { get; set; }
    }

    public class AddGelismeRequest
    {
        public string? Aciklama { get; set; }
        public string? DosyaUrl { get; set; }
    }

    public class FileUploadDto
    {
        public string FileName { get; set; }
        public string FileBase64 { get; set; }
        public string? Module { get; set; }
    }

    public class SendApprovalRequest
    {
        public string? AmirSicil { get; set; }
    }

    public class ApproveRejectRequest
    {
        public bool Approve { get; set; }
        public string? Comment { get; set; }
    }

    public class AskQuestionRequest
    {
        public string? TargetSicil { get; set; }
        public string? QuestionText { get; set; }
    }

    public class AddHelperRequest
    {
        public string? HelperSicil { get; set; }
    }

    public class TalepKontrolRequest
    {
        public string EksikSomun { get; set; }
        public string Yag { get; set; }
        public string Miknatis { get; set; }
        public string FazlaParca { get; set; }
        public string Guvenlik { get; set; }
        public string Makine { get; set; }
        public string Temizlik { get; set; }
        public string Gida { get; set; }
    }

    public class IsEmriKaydetRequest
    {
        public int IsEmriTurID { get; set; }
        public DateTime TerminTar { get; set; }
        public string? Aciklama { get; set; }
        public string? DosyaUrl { get; set; }
        public string? Sicil { get; set; }
    }

    public class IsEmriKapatRequest
    {
        public string? Aciklama { get; set; }
    }

    public class IsEmriAksiyonRequest
    {
        public string? Sicil { get; set; }
    }
}
