using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.BusinessLayer.Dtos;

namespace WebPortalSpace.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TalepController : ControllerBase
    {
        private readonly ITalepService _talepService;

        public TalepController(ITalepService talepService)
        {
            _talepService = talepService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("Giriş yapan kullanıcı kimliği doğrulanamadı.");
        }

        /// <summary>
        /// Belirli bir talep türüne (IT, ERP, BAKIM vb.) göre kullanıcının ilişkili olduğu talepleri listeler.
        /// </summary>
        /// <param name="tur">Talep türü filtre değeri (IT, ERP, BAKIM vb.).</param>
        /// <returns>Taleplerin listesini döner.</returns>
        [HttpGet]
        public IActionResult GetRequests([FromQuery] string tur)
        {
            try
            {
                if (string.IsNullOrEmpty(tur))
                {
                    return BadRequest(new { message = "Talep türü belirtilmelidir (IT, ERP)." });
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
        /// Belirtilen talep türüne ait kategori tanımlarını getirir.
        /// </summary>
        /// <param name="tur">Talep türü filtre değeri.</param>
        /// <returns>Kategori tanımları listesini döner.</returns>
        [HttpGet("categories")]
        public IActionResult GetCategories([FromQuery] string tur)
        {
            try
            {
                if (string.IsNullOrEmpty(tur))
                {
                    return BadRequest(new { message = "Talep türü belirtilmelidir." });
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
        /// Belirtilen ID değerine sahip talebin detay bilgilerini getirir.
        /// </summary>
        /// <param name="id">Talebin benzersiz ID değeri.</param>
        /// <returns>Talep detay ve tarihçe verilerini döner.</returns>
        [HttpGet("{id}")]
        public IActionResult GetDetail(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                var detail = _talepService.GetRequestDetail(userId, id);
                if (detail == null)
                {
                    return NotFound(new { message = "Talep bulunamadı." });
                }
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebin kilit durumunu değiştirir (Kilitler veya kilidi kaldırır).
        /// </summary>
        /// <param name="id">İşlem yapılacak talebin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Talebi onaylaması için belirlenen amire gönderir.
        /// </summary>
        /// <param name="id">Onaya gönderilecek talebin ID değeri.</param>
        /// <param name="request">Onaylayacak amirin sicil numarasını içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/send-approval")]
        public IActionResult SendApproval(int id, [FromBody] SendApprovalRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.AmirSicil))
                {
                    return BadRequest(new { message = "Amir sicil numarası belirtilmelidir." });
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
        /// Onay sürecindeki bir talebin onay isteğini geri çeker.
        /// </summary>
        /// <param name="id">Onay isteği geri çekilecek talebin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Onay aşamasındaki bir talebi onaylar veya reddeder.
        /// </summary>
        /// <param name="id">Onaylanacak veya reddedilecek talebin ID değeri.</param>
        /// <param name="request">Onay/red durumu ve açıklama yorumunu içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/approve-reject")]
        public IActionResult ApproveReject(int id, [FromBody] ApproveRejectRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Geçersiz istek verisi." });
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
        /// Talep ile ilgili bir başka personele soru sorar ve not bırakır.
        /// </summary>
        /// <param name="id">Soru sorulacak talebin ID değeri.</param>
        /// <param name="request">Soru sorulacak personel sicili ve soru metnini içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/ask-question")]
        public IActionResult AskQuestion(int id, [FromBody] AskQuestionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.TargetSicil) || string.IsNullOrEmpty(request.QuestionText))
                {
                    return BadRequest(new { message = "Soru sorulacak personel ve soru metni boş bırakılamaz." });
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
        /// Talebe yardımcı olarak çalışacak ek personel ekler.
        /// </summary>
        /// <param name="id">Yardımcı eklenecek talebin ID değeri.</param>
        /// <param name="request">Eklenecek yardımcı personelin sicil numarasını içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/helpers")]
        public IActionResult AddHelper(int id, [FromBody] AddHelperRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.HelperSicil))
                {
                    return BadRequest(new { message = "Yardımcı personel sicil numarası belirtilmelidir." });
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
        /// Talebe atanmış olan yardımcı personeli talepten kaldırır.
        /// </summary>
        /// <param name="id">İşlem yapılacak talebin ID değeri.</param>
        /// <param name="sicilNo">Kaldırılacak yardımcı personelin sicil numarası.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpDelete("{id}/helpers/{sicilNo}")]
        public IActionResult DeleteHelper(int id, string sicilNo)
        {
            try
            {
                if (string.IsNullOrEmpty(sicilNo))
                {
                    return BadRequest(new { message = "Yardımcı personel sicil numarası belirtilmelidir." });
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
        /// Sistemdeki tüm aktif personellerin listesini getirir.
        /// </summary>
        /// <returns>Aktif personel listesini döner.</returns>
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
        /// Yeni bir talep kaydeder veya mevcut talebi günceller.
        /// </summary>
        /// <param name="dto">Talep ve bakım bilgilerini içeren veri transfer nesnesi.</param>
        /// <returns>Kayıt başarılı ise oluşturulan talep kodunu ve başarı durumunu döner.</returns>
        [HttpPost]
        public IActionResult SaveRequest([FromBody] SaveTalepRequestDto dto)
        {
            try
            {
                if (dto == null || dto.Talep == null)
                {
                    return BadRequest(new { message = "Geçersiz talep verisi." });
                }
                int userId = GetCurrentUserId();
                string code = _talepService.SaveRequest(userId, dto.Talep, dto.Bakim);
                return Ok(new { success = true, code, message = "Talep başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Talebin durumunu (Açık, Kapalı, Beklemede vb.) günceller.
        /// </summary>
        /// <param name="id">Durumu güncellenecek talebin ID değeri.</param>
        /// <param name="request">Yeni durum bilgisini içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/status")]
        public IActionResult UpdateStatus(int id, [FromBody] UpdateTalepStatusRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Status))
                {
                    return BadRequest(new { message = "Geçersiz durum verisi." });
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
        /// Talebi işleme alması için belirli bir personele atar.
        /// </summary>
        /// <param name="id">Atama yapılacak talebin ID değeri.</param>
        /// <param name="request">Atanacak personelin sicil numarasını içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/assign")]
        public IActionResult Assign(int id, [FromBody] AssignTalepRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.SicilNo))
                {
                    return BadRequest(new { message = "Geçersiz sicil numarası." });
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
        /// Talebe dair yeni bir gelişme notu veya açıklama ekler.
        /// </summary>
        /// <param name="id">Gelişme eklenecek talebin ID değeri.</param>
        /// <param name="request">Eklenmek istenen açıklama metnini içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/gelisme")]
        public IActionResult AddGelisme(int id, [FromBody] AddGelismeRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Aciklama))
                {
                    return BadRequest(new { message = "Açıklama boş bırakılamaz." });
                }
                int userId = GetCurrentUserId();
                bool success = _talepService.AddRequestGelisme(userId, id, request.Aciklama);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen talep türüne göre atama yapılabilecek yetkili personellerin listesini getirir.
        /// </summary>
        /// <param name="tur">Talep türü (IT, ERP, BAKIM vb.).</param>
        /// <returns>Atanabilir personel listesini döner.</returns>
        [HttpGet("personels")]
        public IActionResult GetPersonels([FromQuery] string tur)
        {
            try
            {
                if (string.IsNullOrEmpty(tur))
                {
                    return BadRequest(new { message = "Talep türü belirtilmelidir." });
                }
                var list = _talepService.GetPersonels(tur);
                return Ok(list);
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
        public string Aciklama { get; set; }
    }

    public class SendApprovalRequest
    {
        public string AmirSicil { get; set; }
    }

    public class ApproveRejectRequest
    {
        public bool Approve { get; set; }
        public string Comment { get; set; }
    }

    public class AskQuestionRequest
    {
        public string TargetSicil { get; set; }
        public string QuestionText { get; set; }
    }

    public class AddHelperRequest
    {
        public string HelperSicil { get; set; }
    }
}
