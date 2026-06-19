using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;

namespace WebPortalSpace.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
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

        // ====================================================================
        // KULLANICI İŞLEMLERİ
        // ====================================================================

        /// <summary>
        /// Arama kriteri ve durum filtresine göre kayıtlı tüm kullanıcıları listeler.
        /// </summary>
        /// <param name="search">Kullanıcı adı, e-posta veya ad soyad araması için metin filtresi.</param>
        /// <param name="status">Kullanıcı durum filtresi (Aktif, Pasif vb.).</param>
        /// <returns>Kullanıcıların listesini döner.</returns>
        [HttpGet("users")]
        public IActionResult GetUsers([FromQuery] string search = "", [FromQuery] string status = "")
        {
            try
            {
                var users = _adminService.GetUsers(search, status);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanıcılar listelenirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen ID değerine sahip kullanıcının detaylı bilgilerini getirir.
        /// </summary>
        /// <param name="id">Detayı getirilmek istenen kullanıcının ID değeri.</param>
        /// <returns>Kullanıcı detay bilgisini döner.</returns>
        [HttpGet("users/{id}")]
        public IActionResult GetUserDetail(int id)
        {
            try
            {
                var user = _adminService.GetUserDetail(id);
                if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanıcı detayı alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yeni bir kullanıcı oluşturur veya mevcut kullanıcı bilgilerini günceller.
        /// </summary>
        /// <param name="model">Kaydedilecek kullanıcı bilgilerini içeren model.</param>
        /// <returns>Kayıt başarılı ise başarı mesajı ve kullanıcı ID değerini döner.</returns>
        [HttpPost("users")]
        public IActionResult SaveUser([FromBody] tb_Kullanici model)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var result = _adminService.SaveUser(currentUserId, model);
                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }
                return Ok(new { message = result.Message, id = result.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanıcı kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen kullanıcının sistemden tamamen silinmesini sağlar.
        /// </summary>
        /// <param name="id">Silinecek kullanıcının ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                var success = _adminService.DeleteUser(id);
                if (!success) return BadRequest(new { message = "Kullanıcı silinemedi." });
                return Ok(new { message = "Kullanıcı silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanıcı silinirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen kullanıcının durumunu pasifleştirir.
        /// </summary>
        /// <param name="id">Pasifleştirilecek kullanıcının ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("users/{id}/deactivate")]
        public IActionResult DeactivateUser(int id)
        {
            try
            {
                var success = _adminService.DeactivateUser(id);
                if (!success) return BadRequest(new { message = "Kullanıcı pasifleştirilemedi." });
                return Ok(new { message = "Kullanıcı pasif hale getirildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanıcı pasifleştirilirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemde kayıtlı olan personel listesini getirir.
        /// </summary>
        /// <returns>Personel listesini döner.</returns>
        [HttpGet("personnel")]
        public IActionResult GetPersonnel()
        {
            try
            {
                var personnel = _adminService.GetPersonnel();
                return Ok(personnel);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Personel listesi alınırken hata oluştu: {ex.Message}" });
            }
        }

        // ====================================================================
        // PROJE VE SAYFA YÖNETİMİ
        // ====================================================================

        /// <summary>
        /// Sistemde tanımlı tüm projelerin listesini getirir.
        /// </summary>
        /// <returns>Projelerin listesini döner.</returns>
        [HttpGet("projects")]
        public IActionResult GetProjects()
        {
            try
            {
                var projects = _adminService.GetProjects();
                return Ok(projects);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Projeler listelenirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yeni bir proje tanımlar veya var olan projenin bilgilerini günceller.
        /// </summary>
        /// <param name="model">Kaydedilecek proje nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("projects")]
        public IActionResult SaveProject([FromBody] tb_Proje model)
        {
            try
            {
                var result = _adminService.SaveProject(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Proje kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen projeyi sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek projenin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpDelete("projects/{id}")]
        public IActionResult DeleteProject(int id)
        {
            try
            {
                var result = _adminService.DeleteProject(id);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Proje silinirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Projelerin sıralama düzenini günceller.
        /// </summary>
        /// <param name="sortedIds">Sıralanmış proje ID listesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("projects/sort")]
        public IActionResult SortProjects([FromBody] List<int> sortedIds)
        {
            try
            {
                var success = _adminService.SortProjects(sortedIds);
                if (!success) return BadRequest(new { message = "Proje sıralaması güncellenemedi." });
                return Ok(new { message = "Sıralama güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sıralama güncellenirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirli bir projeye ait veya sistemdeki tüm sayfaların listesini getirir.
        /// </summary>
        /// <param name="projectId">Filtrelenecek proje ID değeri (Varsayılan: 0 - hepsi).</param>
        /// <returns>Sayfaların listesini döner.</returns>
        [HttpGet("pages")]
        public IActionResult GetPages([FromQuery] int projectId = 0)
        {
            try
            {
                var pages = _adminService.GetPages(projectId);
                return Ok(pages);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayfalar listelenirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yeni bir sayfa tanımı oluşturur veya mevcut olanı günceller.
        /// </summary>
        /// <param name="model">Kaydedilecek sayfa nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("pages")]
        public IActionResult SavePage([FromBody] tb_Sayfa model)
        {
            try
            {
                var result = _adminService.SavePage(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayfa kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen sayfa tanımını sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek sayfanın ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpDelete("pages/{id}")]
        public IActionResult DeletePage(int id)
        {
            try
            {
                var result = _adminService.DeletePage(id);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayfa silinirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sayfaların sıralama düzenini günceller.
        /// </summary>
        /// <param name="sortedIds">Sıralanmış sayfa ID listesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("pages/sort")]
        public IActionResult SortPages([FromBody] List<int> sortedIds)
        {
            try
            {
                var success = _adminService.SortPages(sortedIds);
                if (!success) return BadRequest(new { message = "Sayfa sıralaması güncellenemedi." });
                return Ok(new { message = "Sıralama güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sıralama güncellenirken hata oluştu: {ex.Message}" });
            }
        }

        // ====================================================================
        // YETKİLENDİRME (PERMISSIONS)
        // ====================================================================

        /// <summary>
        /// Belirtilen kullanıcının erişim yetkisi olan sayfaların listesini getirir.
        /// </summary>
        /// <param name="userId">Yetkileri getirilecek kullanıcının ID değeri.</param>
        /// <returns>Kullanıcının yetkili olduğu sayfa ID listesini döner.</returns>
        [HttpGet("permissions/{userId}")]
        public IActionResult GetPermissions(int userId)
        {
            try
            {
                var permissions = _adminService.GetPermissions(userId);
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Yetkiler alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Kullanıcının erişebileceği sayfa yetkilerini kaydeder.
        /// </summary>
        /// <param name="userId">Yetkilendirilecek kullanıcının ID değeri.</param>
        /// <param name="sayfaIds">Erişim verilecek sayfa ID listesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("permissions/{userId}")]
        public IActionResult SavePermissions(int userId, [FromBody] List<int> sayfaIds)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var success = _adminService.SavePermissions(currentUserId, userId, sayfaIds);
                if (!success) return BadRequest(new { message = "Yetkiler kaydedilemedi." });
                return Ok(new { message = "Yetkiler başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Yetkiler kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        // ====================================================================
        // EK AYARLAR SAYFALARI (DASHBOARD, LOGS, SMS, CATEGORIES, HIERARCHY, AI)
        // ====================================================================

        /// <summary>
        /// Yönetim paneli ana sayfası (Dashboard) için genel istatistikleri ve özet verileri getirir.
        /// </summary>
        /// <returns>Admin dashboard istatistik verilerini döner.</returns>
        [HttpGet("dashboard-stats")]
        public IActionResult GetDashboardStats()
        {
            try
            {
                var stats = _adminService.GetAdminDashboardStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"İstatistikler alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistem içi işlem/hata günlük (log) kayıtlarını filtreleyerek getirir.
        /// </summary>
        /// <param name="search">Log içeriğinde aranacak metin filtresi.</param>
        /// <returns>Sistem log kayıtları listesini döner.</returns>
        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] string search = "")
        {
            try
            {
                var logs = _adminService.GetLogs(search);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Loglar alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gönderilen SMS kayıtlarını ve durumlarını filtreleyerek listeler.
        /// </summary>
        /// <param name="search">SMS içeriğinde veya alıcı numarasında aranacak metin.</param>
        /// <returns>SMS log listesini döner.</returns>
        [HttpGet("sms-logs")]
        public IActionResult GetSmsLogs([FromQuery] string search = "")
        {
            try
            {
                var sms = _adminService.GetSmsLogs(search);
                return Ok(sms);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"SMS logları alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemdeki belge/talep hareketlerinin tarihçe günlüklerini getirir.
        /// </summary>
        /// <param name="search">Belge no veya kullanıcı adına göre filtreleme metni.</param>
        /// <returns>Belge tarihçe listesini döner.</returns>
        [HttpGet("belge-tarihce")]
        public IActionResult GetBelgeTarihce([FromQuery] string search = "")
        {
            try
            {
                var history = _adminService.GetBelgeTarihce(search);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Belge tarihçesi alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Bilet (Ticket) modülünde tanımlı kategorileri listeler.
        /// </summary>
        /// <returns>Bilet kategorilerinin listesini döner.</returns>
        [HttpGet("ticket-categories")]
        public IActionResult GetTicketCategories()
        {
            try
            {
                var list = _adminService.GetTicketCategories();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Bilet kategorileri alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Bilet (Ticket) modülü için yeni bir kategori kaydeder veya günceller.
        /// </summary>
        /// <param name="model">Kaydedilecek ticket kategori nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("ticket-categories")]
        public IActionResult SaveTicketCategory([FromBody] tb_TicketKategori model)
        {
            try
            {
                var result = _adminService.SaveTicketCategory(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategori kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen bilet kategorisini sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek kategorinin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpDelete("ticket-categories/{id}")]
        public IActionResult DeleteTicketCategory(int id)
        {
            try
            {
                var success = _adminService.DeleteTicketCategory(id);
                if (!success) return BadRequest(new { message = "Kategori silinemedi." });
                return Ok(new { message = "Kategori silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategori silinirken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Personel yönetim hiyerarşisini (organizasyon yapısını) getirir.
        /// </summary>
        /// <returns>Hiyerarşi şeması verilerini döner.</returns>
        [HttpGet("hierarchy")]
        public IActionResult GetHierarchy()
        {
            try
            {
                var hierarchy = _adminService.GetHierarchy();
                return Ok(hierarchy);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hiyerarşi listesi alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yapay zeka modülü için tanımlanmış olan ayarların listesini getirir.
        /// </summary>
        /// <returns>Yapay zeka ayarları listesini döner.</returns>
        [HttpGet("ai-settings")]
        public IActionResult GetAiSettings()
        {
            try
            {
                var list = _adminService.GetAiSettings();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Yapay zeka ayarları alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yapay zeka modülüne ait bir ayar kaydını oluşturur veya günceller.
        /// </summary>
        /// <param name="model">Kaydedilecek yapay zeka ayarı nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("ai-settings")]
        public IActionResult SaveAiSetting([FromBody] tb_AiAyarlar model)
        {
            try
            {
                var result = _adminService.SaveAiSetting(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Yapay zeka ayarı kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpPost("users/{id}/reset-password")]
        public IActionResult UpdateUserPassword(int id, [FromBody] ResetPasswordDto model)
        {
            try
            {
                var result = _adminService.UpdateUserPassword(id, model?.NewPassword);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Şifre güncellenirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("users/{id}/document-types")]
        public IActionResult GetUserDocumentTypes(int id)
        {
            try
            {
                var result = _adminService.GetUserDocumentTypes(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Belge yetkileri alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpPost("users/{id}/document-types")]
        public IActionResult SaveUserDocumentTypes(int id, [FromBody] List<string> codes)
        {
            try
            {
                var result = _adminService.SaveUserDocumentTypes(id, codes);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Belge yetkileri kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("helpdesk/categories")]
        public IActionResult GetHelpDeskCategories([FromQuery] string search = "", [FromQuery] string categoryId = "", [FromQuery] string typeCode = "")
        {
            try
            {
                var result = _adminService.GetHelpDeskCategories(search, categoryId, typeCode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"HelpDesk kategorileri alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("helpdesk/categories/{id}")]
        public IActionResult GetHelpDeskCategoryDetail(int id)
        {
            try
            {
                var result = _adminService.GetHelpDeskCategoryDetail(id);
                if (result == null) return NotFound(new { message = "Kategori bulunamadı." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategori detayı alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpPost("helpdesk/categories")]
        public IActionResult SaveHelpDeskCategory([FromBody] tb_TalepKategori model)
        {
            try
            {
                var result = _adminService.SaveHelpDeskCategory(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategori kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpDelete("helpdesk/categories/{id}")]
        public IActionResult DeleteHelpDeskCategory(int id)
        {
            try
            {
                var result = _adminService.DeleteHelpDeskCategory(id);
                if (!result) return BadRequest(new { message = "Kategori silinemedi." });
                return Ok(new { message = "Kategori silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategori silinirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpPost("helpdesk/categories/responsibles")]
        public IActionResult SaveCategoryResponsible([FromBody] tb_TalepAyar model)
        {
            try
            {
                var result = _adminService.SaveCategoryResponsible(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sorumlu personel kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpDelete("helpdesk/categories/responsibles/{id}")]
        public IActionResult DeleteCategoryResponsible(int id)
        {
            try
            {
                var result = _adminService.DeleteCategoryResponsible(id);
                if (!result) return BadRequest(new { message = "Sorumlu personel silinemedi." });
                return Ok(new { message = "Sorumlu personel silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sorumlu personel silinirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("helpdesk/types")]
        public IActionResult GetHelpDeskTypes()
        {
            try
            {
                var result = _adminService.GetHelpDeskTypes();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Talep türleri alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("helpdesk/companies")]
        public IActionResult GetCompanies()
        {
            try
            {
                var result = _adminService.GetCompanies();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Şirket listesi alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("logs/paged")]
        public IActionResult GetLogsPaged(
            [FromQuery] string search = "",
            [FromQuery] string userEmail = "",
            [FromQuery] string startDate = null,
            [FromQuery] string endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                DateTime? start = null;
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var stVal)) start = stVal;

                DateTime? end = null;
                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var endVal)) end = endVal;

                var result = _adminService.GetLogsPaged(search, userEmail, start, end, page, pageSize);
                return Ok(new { items = result.Items, totalCount = result.TotalCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Loglar listelenirken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("belge-tarihce/paged")]
        public IActionResult GetBelgeTarihcePaged(
            [FromQuery] string search = "",
            [FromQuery] string documentCode = "",
            [FromQuery] string startDate = null,
            [FromQuery] string endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                DateTime? start = null;
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var stVal)) start = stVal;

                DateTime? end = null;
                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var endVal)) end = endVal;

                var result = _adminService.GetBelgeTarihcePaged(search, documentCode, start, end, page, pageSize);
                return Ok(new { items = result.Items, totalCount = result.TotalCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Belge tarihçesi listelenirken hata oluştu: {ex.Message}" });
            }
        }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; }
    }
}
