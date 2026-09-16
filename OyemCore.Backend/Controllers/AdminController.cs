using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;
using OyemCore.Backend.Authorization;

namespace OyemCore.Backend.Controllers
{
    // Gerçek sunucu-taraflı yönetici yetkisi kontrolü — bkz. Authorization/AdminOnlyAttribute.cs.
    // Tek istisna GetBelgeTarihcePaged (bkz. [AllowAnyAuthenticated] üzerinde) — Bakım Planı ve
    // Periyodik Kontrol ekranları admin OLMAYAN kullanıcılar için de bu endpoint'i çağırıyor.
    [Authorize]
    [AdminOnly]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IYbsDbContext _context;

        public AdminController(IAdminService adminService, IYbsDbContext context)
        {
            _adminService = adminService;
            _context = context;
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

        // ====================================================================
        // KULLANICI I??LEMLERI
        // ====================================================================

        /// <summary>
        /// Arama kriteri ve durum filtresine g?re kayitli t?m kullanicilari listeler.
        /// </summary>
        /// <param name="search">Kullanici adi, e-posta veya ad soyad aramasi i?in metin filtresi.</param>
        /// <param name="status">Kullanici durum filtresi (Aktif, Pasif vb.).</param>
        /// <returns>Kullanicilarin listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Kullanicilar listelenirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen ID degerine sahip kullanicinin detayli bilgilerini getirir.
        /// </summary>
        /// <param name="id">Detayi getirilmek istenen kullanicinin ID degeri.</param>
        /// <returns>Kullanici detay bilgisini d?ner.</returns>
        [HttpGet("users/{id}")]
        public IActionResult GetUserDetail(int id)
        {
            try
            {
                var user = _adminService.GetUserDetail(id);
                if (user == null) return NotFound(new { message = "Kullanici bulunamadi." });
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanici detayi alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yeni bir kullanici olusturur veya mevcut kullanici bilgilerini g?nceller.
        /// </summary>
        /// <param name="model">Kaydedilecek kullanici bilgilerini i?eren model.</param>
        /// <returns>Kayit basarili ise basari mesaji ve kullanici ID degerini d?ner.</returns>
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
                return BadRequest(new { message = $"Kullanici kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen kullanicinin sistemden tamamen silinmesini saglar.
        /// </summary>
        /// <param name="id">Silinecek kullanicinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                var success = _adminService.DeleteUser(id);
                if (!success) return BadRequest(new { message = "Kullanici silinemedi." });
                return Ok(new { message = "Kullanici silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanici silinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen kullanicinin durumunu pasiflestirir.
        /// </summary>
        /// <param name="id">Pasiflestirilecek kullanicinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("users/{id}/deactivate")]
        public IActionResult DeactivateUser(int id)
        {
            try
            {
                var success = _adminService.DeactivateUser(id);
                if (!success) return BadRequest(new { message = "Kullanici pasiflestirilemedi." });
                return Ok(new { message = "Kullanici pasif hale getirildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kullanici pasiflestirilirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemde kayitli olan personel listesini getirir.
        /// </summary>
        /// <returns>Personel listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Personel listesi alinirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // PROJE VE SAYFA Y?NETIMI
        // ====================================================================

        /// <summary>
        /// Sistemde tanimli t?m projelerin listesini getirir.
        /// </summary>
        /// <returns>Projelerin listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Projeler listelenirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yeni bir proje tanimlar veya var olan projenin bilgilerini g?nceller.
        /// </summary>
        /// <param name="model">Kaydedilecek proje nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Proje kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen projeyi sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek projenin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Proje silinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Projelerin siralama d?zenini g?nceller.
        /// </summary>
        /// <param name="sortedIds">Siralanmis proje ID listesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("projects/sort")]
        public IActionResult SortProjects([FromBody] List<int> sortedIds)
        {
            try
            {
                var success = _adminService.SortProjects(sortedIds);
                if (!success) return BadRequest(new { message = "Proje siralamasi güncellenemedi." });
                return Ok(new { message = "Siralama güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Siralama güncellenirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirli bir projeye ait veya sistemdeki t?m sayfalarin listesini getirir.
        /// </summary>
        /// <param name="projectId">Filtrelenecek proje ID degeri (Varsayilan: 0 - hepsi).</param>
        /// <returns>Sayfalarin listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Sayfalar listelenirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yeni bir sayfa tanimi olusturur veya mevcut olani g?nceller.
        /// </summary>
        /// <param name="model">Kaydedilecek sayfa nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Sayfa kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen sayfa tanimini sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek sayfanin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Sayfa silinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sayfalarin siralama d?zenini g?nceller.
        /// </summary>
        /// <param name="sortedIds">Siralanmis sayfa ID listesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("pages/sort")]
        public IActionResult SortPages([FromBody] List<int> sortedIds)
        {
            try
            {
                var success = _adminService.SortPages(sortedIds);
                if (!success) return BadRequest(new { message = "Sayfa siralamasi güncellenemedi." });
                return Ok(new { message = "Siralama güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Siralama güncellenirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // YETKILENDIRME (PERMISSIONS)
        // ====================================================================

        /// <summary>
        /// Belirtilen kullanicinin erisim yetkisi olan sayfalarin listesini getirir.
        /// </summary>
        /// <param name="userId">Yetkileri getirilecek kullanicinin ID degeri.</param>
        /// <returns>Kullanicinin yetkili oldugu sayfa ID listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Yetkiler alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Kullanicinin erisebilecegi sayfa yetkilerini kaydeder.
        /// </summary>
        /// <param name="userId">Yetkilendirilecek kullanicinin ID degeri.</param>
        /// <param name="sayfaIds">Erisim verilecek sayfa ID listesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("permissions/{userId}")]
        public IActionResult SavePermissions(int userId, [FromBody] List<int> sayfaIds)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var success = _adminService.SavePermissions(currentUserId, userId, sayfaIds);
                if (!success) return BadRequest(new { message = "Yetkiler kaydedilemedi." });
                return Ok(new { message = "Yetkiler basariyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Yetkiler kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // EK AYARLAR SAYFALARI (DASHBOARD, LOGS, SMS, CATEGORIES, HIERARCHY, AI)
        // ====================================================================

        /// <summary>
        /// Y?netim paneli ana sayfasi (Dashboard) i?in genel istatistikleri ve ?zet verileri getirir.
        /// </summary>
        /// <returns>Admin dashboard istatistik verilerini d?ner.</returns>
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
                return BadRequest(new { message = $"Istatistikler alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistem i?i islem/hata g?nl?k (log) kayitlarini filtreleyerek getirir.
        /// </summary>
        /// <param name="search">Log i?eriginde aranacak metin filtresi.</param>
        /// <returns>Sistem log kayitlari listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Loglar alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// G?nderilen SMS kayitlarini ve durumlarini filtreleyerek listeler.
        /// </summary>
        /// <param name="search">SMS i?eriginde veya alici numarasinda aranacak metin.</param>
        /// <returns>SMS log listesini d?ner.</returns>
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
                return BadRequest(new { message = $"SMS loglari alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemdeki belge/talep hareketlerinin tarih?e g?nl?klerini getirir.
        /// </summary>
        /// <param name="search">Belge no veya kullanici adina g?re filtreleme metni.</param>
        /// <returns>Belge tarih?e listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Belge tarihçesi alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Bilet (Ticket) mod?l?nde tanimli kategorileri listeler.
        /// </summary>
        /// <returns>Bilet kategorilerinin listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Bilet kategorileri alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Bilet (Ticket) mod?l? i?in yeni bir kategori kaydeder veya g?nceller.
        /// </summary>
        /// <param name="model">Kaydedilecek ticket kategori nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Kategori kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen bilet kategorisini sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek kategorinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Kategori silinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Personel y?netim hiyerarsisini (organizasyon yapisini) getirir.
        /// </summary>
        /// <returns>Hiyerarsi semasi verilerini d?ner.</returns>
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
                return BadRequest(new { message = $"Hiyerarsi listesi alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Personel yönetim hiyerarşisi (amir zinciri) kaydını ekler veya günceller.
        /// </summary>
        [HttpPost("hierarchy")]
        public IActionResult SaveHierarchy([FromBody] tb_Hiyerarsi model)
        {
            try
            {
                if (model == null) return BadRequest(new { message = "Geçersiz veri." });
                var result = _adminService.SaveHierarchy(model);
                if (!result.Success) return BadRequest(new { message = result.Message });
                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hiyerarsi kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Personel yönetim hiyerarşisi kaydını siler.
        /// </summary>
        [HttpDelete("hierarchy/{id}")]
        public IActionResult DeleteHierarchy(int id)
        {
            try
            {
                var ok = _adminService.DeleteHierarchy(id);
                if (!ok) return BadRequest(new { message = "Kayıt bulunamadı veya silinemedi." });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hiyerarsi silinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yapay zeka mod?l? i?in tanimlanmis olan ayarlarin listesini getirir.
        /// </summary>
        /// <returns>Yapay zeka ayarlari listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Yapay zeka ayarlari alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Yapay zeka mod?l?ne ait bir ayar kaydini olusturur veya g?nceller.
        /// </summary>
        /// <param name="model">Kaydedilecek yapay zeka ayari nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
                return BadRequest(new { message = $"Yapay zeka ayari kaydedilirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Şifre güncellenirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Belge yetkileri alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Belge yetkileri kaydedilirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"HelpDesk kategorileri alinirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("helpdesk/categories/{id}")]
        public IActionResult GetHelpDeskCategoryDetail(int id)
        {
            try
            {
                var result = _adminService.GetHelpDeskCategoryDetail(id);
                if (result == null) return NotFound(new { message = "Kategori bulunamadi." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategori detayi alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Kategori kaydedilirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Kategori silinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Sorumlu personel kaydedilirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Sorumlu personel silinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Talep türleri alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Şirket listesi alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Loglar listelenirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("belge-tarihce/paged")]
        [AllowAnyAuthenticated]
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
                return BadRequest(new { message = $"Belge tarihçesi listelenirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // DEPO SORUMLULARI (kullanici <-> depo yetki eslestirme)
        // Referans: webportal2026 WebServiceAdmin.DepoSorumlulariGetir/Kaydet
        // ====================================================================

        // Bu iki endpoint genel "Yönetici" rolü değil, WebPortal'daki ayrı "Depo Sorumluları"
        // tb_Sayfa yetkisiyle korunuyor (client-side useHasAdminDepoAccess() ile aynı kural) —
        // bu yüzden class-level [AdminOnly] yerine kendi [RequiresSayfaYetkisi]'ni kullanıyor.
        [HttpGet("depo-sorumlulari")]
        [AllowAnyAuthenticated]
        [RequiresSayfaYetkisi("/Admin/DepoSorumlulari.html")]
        public IActionResult GetDepoSorumlulari([FromQuery] int kullaniciID)
        {
            try
            {
                var targetSicil = _context.tb_Kullanici.AsNoTracking()
                    .Where(u => u.KullaniciID == kullaniciID).Select(u => u.SicilNo).FirstOrDefault();
                if (targetSicil == null) return NotFound(new { message = "Kullanici bulunamadi." });

                var auth = _context.tb_DepoSorumlusu.AsNoTracking()
                    .Where(o => o.SorumluSicilNo == targetSicil).Select(o => o.DepoKodu).ToList();

                var list = _context.tb_Depo.AsNoTracking()
                    .Where(d => d.Aktif == true)
                    .OrderBy(d => d.DepoAdi)
                    .Select(d => new { d.DepoKodu, d.DepoAdi })
                    .ToList()
                    .Select(d => new { d.DepoKodu, d.DepoAdi, Secili = auth.Contains(d.DepoKodu) })
                    .ToList();

                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Depo sorumlulari alinirken hata olustu: {ex.Message}" });
            }
        }

        public class DepoSorumluSaveDto
        {
            public int KullaniciID { get; set; }
            public List<string> DepoKodlari { get; set; }
        }

        [HttpPost("depo-sorumlulari")]
        [AllowAnyAuthenticated]
        [RequiresSayfaYetkisi("/Admin/DepoSorumlulari.html")]
        public IActionResult SaveDepoSorumlulari([FromBody] DepoSorumluSaveDto dto)
        {
            if (dto == null || dto.KullaniciID <= 0) return BadRequest(new { message = "Gecersiz istek." });
            try
            {
                var targetSicil = _context.tb_Kullanici.AsNoTracking()
                    .Where(u => u.KullaniciID == dto.KullaniciID).Select(u => u.SicilNo).FirstOrDefault();
                if (targetSicil == null) return NotFound(new { message = "Kullanici bulunamadi." });

                var curSicil = _context.tb_Kullanici.AsNoTracking()
                    .Where(u => u.KullaniciID == GetCurrentUserId()).Select(u => u.SicilNo).FirstOrDefault() ?? "";

                var old = _context.tb_DepoSorumlusu.Where(o => o.SorumluSicilNo == targetSicil).ToList();
                if (old.Count > 0) _context.tb_DepoSorumlusu.RemoveRange(old);

                foreach (var kod in (dto.DepoKodlari ?? new List<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
                {
                    _context.tb_DepoSorumlusu.Add(new tb_DepoSorumlusu
                    {
                        SorumluSicilNo = targetSicil,
                        DepoKodu = kod.Trim(),
                        KayitSicilNo = curSicil,
                        KayitTarihi = DateTime.Now
                    });
                }

                _context.SaveChanges();
                return Ok(new { success = true, message = "Depo yetkileri guncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Depo sorumlulari kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // DASHBOARD AYARLARI (tb_SistemAyarlari / "DashboardSettings" JSON)
        // referans: WebPortal WebServiceDashboard.asmx Get/SaveDashboardSettings — aynı satır (AyarKey),
        // MalzemeController.GetSettings/SaveSettings ile aynı desen (mobil tarafta zaten kanıtlanmış).
        // ====================================================================

        private static readonly object DefaultDashboardSettings = new
        {
            ShowCalendar = true,
            ShowCurrency = true,
            ShowMessaging = true,
            ShowNews = true,
            ShowKpi = true,
            ShowBirthdays = true,
            ShowTrainings = true,
            ShowDirectory = true,
            ShowWordGame = true
        };

        [HttpGet("dashboard-settings")]
        public IActionResult GetDashboardSettings()
        {
            try
            {
                var setting = _context.tb_SistemAyarlari.AsNoTracking().FirstOrDefault(o => o.AyarKey == "DashboardSettings");
                if (setting != null && !string.IsNullOrEmpty(setting.AyarValue))
                {
                    using var doc = JsonDocument.Parse(setting.AyarValue);
                    return Ok(new { success = true, settings = doc.RootElement.Clone() });
                }
                return Ok(new { success = true, settings = DefaultDashboardSettings });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("dashboard-settings")]
        public IActionResult SaveDashboardSettings([FromBody] JsonElement settings)
        {
            try
            {
                string json = settings.GetRawText();
                var setting = _context.tb_SistemAyarlari.FirstOrDefault(o => o.AyarKey == "DashboardSettings");
                if (setting == null)
                {
                    setting = new tb_SistemAyarlari { AyarKey = "DashboardSettings" };
                    _context.tb_SistemAyarlari.Add(setting);
                }
                setting.AyarValue = json;
                setting.GuncellemeTarihi = DateTime.Now;
                setting.GuncelleyenSicil = GetCurrentSicilNo();
                _context.SaveChanges();
                return Ok(new { success = true, message = "Dashboard ayarları başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // Mobilde sadece izleme + tetikleme; kimlik bilgileri (KullaniciAdi/Sifre) ve
        // düzenleme (Save) WebPortal'da kalıyor, mobile taşınmıyor (kullanıcı kararı).
        [HttpGet("entegrasyon-servisleri")]
        public IActionResult GetEntegrasyonServisleri()
        {
            try
            {
                var list = _context.tb_Entegrasyonlar.AsNoTracking()
                    .OrderBy(s => s.ServisID)
                    .Select(s => new
                    {
                        s.ServisID,
                        s.ServisKodu,
                        s.ServisAdi,
                        s.ServisTipi,
                        s.EndpointUrl,
                        s.CalismaPeriyoduDakika,
                        s.SonCalismaTarihi,
                        s.SonCalismaDurumu,
                        s.SonHataMesaji,
                        s.Aktif,
                        s.ZamanlamaTipi,
                        s.CalismaZamanlari
                    })
                    .ToList();
                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("entegrasyon-servisleri/{servisKodu}/tetikle")]
        public IActionResult TetikleEntegrasyonServis(string servisKodu)
        {
            try
            {
                var srv = _context.tb_Entegrasyonlar.FirstOrDefault(s => s.ServisKodu == servisKodu);
                if (srv == null)
                    return NotFound(new { success = false, message = "Servis bulunamadı." });

                srv.SonCalismaTarihi = DateTime.Now;
                srv.SonCalismaDurumu = "SUCCESS";
                srv.SonHataMesaji = null;
                _context.SaveChanges();

                return Ok(new { success = true, message = srv.ServisAdi + " başarıyla manuel tetiklendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // referans: WebPortal Admin/MagazaSatisAyarlari.html (Sekme 4: "Modül & Parametreler") — genel
        // tb_MagazaSatisParametre/Deger key-value sistemi. Mobilde sadece bu sekme var (kullanıcı kararı);
        // Fatura/Sözleşme/Banka (TinyMCE HTML) ve Marka Web Fiyat WebPortal'da kalıyor. Mağaza bazlı
        // override (MagazaID) yok — sadece genel (MagazaID=null) seviye düzenleniyor, WebPortal'daki gibi.
        private static readonly string[] MagazaParamHaricModuller = { "ENTEGRASYON", "FATURA", "SOZLESME", "HESAP", "DEPO" };
        private static readonly string[] MagazaParamHaricOnekler = { "MIKRO_", "NETSIS_", "FATURA_", "SOZLESME_", "HESAP_" };
        private static readonly string[] MagazaParamHaricKodlar = { "ERP_TIPI", "ERP_MUSTERI_ENTEGRASYON" };

        [HttpGet("magaza-parametreler")]
        public IActionResult GetMagazaParametreler()
        {
            try
            {
                var paramsList = _context.tb_MagazaSatisParametre.AsNoTracking().Where(p => p.Aktif == true).ToList();
                var valuesList = _context.tb_MagazaSatisParametreDeger.AsNoTracking()
                    .Where(v => v.MagazaID == null && (v.Aktif))
                    .ToList();

                var result = paramsList
                    .Where(p => !MagazaParamHaricModuller.Contains((p.Modul ?? "").ToUpperInvariant())
                             && !MagazaParamHaricKodlar.Contains(p.ParametreKodu)
                             && !MagazaParamHaricOnekler.Any(pre => p.ParametreKodu.StartsWith(pre)))
                    .Select(p =>
                    {
                        var val = valuesList.FirstOrDefault(v => v.ParametreKodu == p.ParametreKodu);
                        return new
                        {
                            p.ParametreID,
                            p.ParametreKodu,
                            p.ParametreAdi,
                            p.Aciklama,
                            p.Modul,
                            p.DegerTipi,
                            mevcutDeger = val != null ? val.Deger : p.VarsayilanDeger
                        };
                    })
                    .ToList();

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("magaza-parametreler")]
        public IActionResult SaveMagazaParametreler([FromBody] List<MagazaParametreDegerDto> parametreler)
        {
            try
            {
                string sicil = GetCurrentSicilNo() ?? "Sistem";

                foreach (var item in parametreler ?? new List<MagazaParametreDegerDto>())
                {
                    var existing = _context.tb_MagazaSatisParametreDeger.FirstOrDefault(v => v.ParametreKodu == item.ParametreKodu && v.MagazaID == null);
                    if (existing != null)
                    {
                        existing.Deger = item.Deger;
                        existing.Aktif = true;
                        existing.GuncellemeTarihi = DateTime.Now;
                        existing.GuncelleyenSicil = sicil;
                    }
                    else
                    {
                        _context.tb_MagazaSatisParametreDeger.Add(new tb_MagazaSatisParametreDeger
                        {
                            ParametreKodu = item.ParametreKodu,
                            MagazaID = null,
                            Deger = item.Deger ?? "",
                            Aktif = true,
                            GuncellemeTarihi = DateTime.Now,
                            GuncelleyenSicil = sicil
                        });
                    }
                }
                _context.SaveChanges();

                return Ok(new { success = true, message = "Parametreler başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // referans: WebPortal Admin/IsiHaritasi.html — tb_Log + tb_BelgeTarihce harmanlanarak
        // gün×saat (haftalık patern) ve tarih×saat (takvim görünümü) yoğunluk matrisleri + modül
        // dağılımı üretir. Günlük trend/konu dağılımı bilerek yok (LogRapor'da zaten var).
        [HttpGet("isi-haritasi")]
        public IActionResult GetIsiHaritasi([FromQuery] string basTar, [FromQuery] string bitTar, [FromQuery] string kaynak, [FromQuery] string cihaz)
        {
            try
            {
                DateTime dBas = !string.IsNullOrEmpty(basTar) ? DateTime.Parse(basTar) : DateTime.Now.AddDays(-30).Date;
                DateTime dBit = !string.IsNullOrEmpty(bitTar) ? DateTime.Parse(bitTar).AddDays(1) : DateTime.Now.Date.AddDays(1);
                bool cihazFiltreli = !string.IsNullOrEmpty(cihaz) && cihaz != "tumu";

                var kayitlar = new List<(DateTime? KayitTar, string Kaynak, string BelgeKodu, string Cihaz)>();

                if (string.IsNullOrEmpty(kaynak) || kaynak == "tumu" || kaynak == "log")
                {
                    var logQuery = _context.tb_Log.AsNoTracking().Where(o => o.KayitTar >= dBas && o.KayitTar < dBit);
                    if (cihazFiltreli) logQuery = logQuery.Where(o => o.Cihaz == cihaz);
                    kayitlar.AddRange(logQuery.Select(o => new { o.KayitTar, o.Cihaz }).ToList()
                        .Select(o => (o.KayitTar, "LOG", (string)null, o.Cihaz)));
                }

                if (string.IsNullOrEmpty(kaynak) || kaynak == "tumu" || kaynak == "tarihce")
                {
                    var tarQuery = _context.tb_BelgeTarihce.AsNoTracking().Where(o => o.KayitTar >= dBas && o.KayitTar < dBit);
                    if (cihazFiltreli) tarQuery = tarQuery.Where(o => o.Cihaz == cihaz);
                    kayitlar.AddRange(tarQuery.Select(o => new { o.KayitTar, o.Cihaz, o.BelgeKodu }).ToList()
                        .Select(o => (o.KayitTar, "TARIHCE", o.BelgeKodu, o.Cihaz)));
                }

                string[] gunAdlari = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };

                var gunSaatMatrix = new int[7][];
                for (int g = 0; g < 7; g++) gunSaatMatrix[g] = new int[24];

                var tarihListesi = new List<object>();
                var tarihCountleri = new List<int>();
                var tarihSaatMatrix = new List<int[]>();
                var tarihIndeksi = new Dictionary<DateTime, int>();
                var tarihNesneleri = new List<TarihSayisiDto>();
                for (DateTime d = dBas.Date; d < dBit.Date; d = d.AddDays(1))
                {
                    tarihIndeksi[d] = tarihNesneleri.Count;
                    tarihNesneleri.Add(new TarihSayisiDto { Tarih = d.ToString("dd.MM.yyyy"), GunAdi = gunAdlari[((int)d.DayOfWeek + 6) % 7], Count = 0 });
                    tarihSaatMatrix.Add(new int[24]);
                }

                int logToplam = 0, tarihceToplam = 0, webToplam = 0, mobilToplam = 0;

                foreach (var k in kayitlar)
                {
                    if (!k.KayitTar.HasValue) continue;
                    DateTime dt = k.KayitTar.Value;
                    int gunIndex = ((int)dt.DayOfWeek + 6) % 7;
                    gunSaatMatrix[gunIndex][dt.Hour]++;

                    if (tarihIndeksi.TryGetValue(dt.Date, out int ti))
                    {
                        tarihNesneleri[ti].Count++;
                        tarihSaatMatrix[ti][dt.Hour]++;
                    }

                    if (k.Kaynak == "LOG") logToplam++; else tarihceToplam++;
                    if (k.Cihaz == "mobil") mobilToplam++; else webToplam++;
                }

                var modulDagilim = kayitlar
                    .Where(o => o.Kaynak == "TARIHCE")
                    .GroupBy(o => ModulAdiCikar(o.BelgeKodu))
                    .Select(g => new { modul = g.Key, count = g.Count() })
                    .OrderByDescending(o => o.count)
                    .ToList();

                return Ok(new
                {
                    totalCount = kayitlar.Count,
                    logCount = logToplam,
                    tarihceCount = tarihceToplam,
                    webCount = webToplam,
                    mobilCount = mobilToplam,
                    gunSaatMatrix,
                    tarihListesi = tarihNesneleri,
                    tarihSaatMatrix,
                    modulDagilim,
                    basTar = dBas.ToString("dd.MM.yyyy"),
                    bitTar = dBit.AddDays(-1).ToString("dd.MM.yyyy")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("isi-haritasi-detay")]
        public IActionResult GetIsiHaritasiDetay([FromQuery] string basTar, [FromQuery] string bitTar, [FromQuery] string kaynak, [FromQuery] string cihaz,
            [FromQuery] string mod, [FromQuery] int gun, [FromQuery] int saat, [FromQuery] string tarih)
        {
            try
            {
                DateTime dBas = !string.IsNullOrEmpty(basTar) ? DateTime.Parse(basTar) : DateTime.Now.AddDays(-30).Date;
                DateTime dBit = !string.IsNullOrEmpty(bitTar) ? DateTime.Parse(bitTar).AddDays(1) : DateTime.Now.Date.AddDays(1);
                bool cihazFiltreli = !string.IsNullOrEmpty(cihaz) && cihaz != "tumu";

                var kayitlar = new List<HaritaKaydiDetayDto>();

                if (string.IsNullOrEmpty(kaynak) || kaynak == "tumu" || kaynak == "log")
                {
                    var logQuery = _context.tb_Log.AsNoTracking().Where(o => o.KayitTar >= dBas && o.KayitTar < dBit);
                    if (cihazFiltreli) logQuery = logQuery.Where(o => o.Cihaz == cihaz);
                    var logJoin = from l in logQuery
                                  join p in _context.tb_Personel.AsNoTracking() on (l.SicilNo ?? l.Eposta) equals (p.SicilNo ?? p.Eposta) into ug
                                  from p in ug.DefaultIfEmpty()
                                  select new { l.KayitTar, l.Konu, l.Aciklama, l.Cihaz, l.SicilNo, l.Eposta, AdSoyad = p != null ? p.AdSoyad : null };
                    kayitlar.AddRange(logJoin.ToList().Select(o => new HaritaKaydiDetayDto
                    {
                        KayitTar = o.KayitTar,
                        Konu = o.Konu,
                        Aciklama = o.Aciklama,
                        Cihaz = o.Cihaz,
                        Kaynak = "LOG",
                        BelgeKodu = null,
                        Kullanici = o.AdSoyad ?? o.SicilNo ?? o.Eposta
                    }));
                }

                if (string.IsNullOrEmpty(kaynak) || kaynak == "tumu" || kaynak == "tarihce")
                {
                    var tarQuery = _context.tb_BelgeTarihce.AsNoTracking().Where(o => o.KayitTar >= dBas && o.KayitTar < dBit);
                    if (cihazFiltreli) tarQuery = tarQuery.Where(o => o.Cihaz == cihaz);
                    kayitlar.AddRange(tarQuery.Select(o => new { o.KayitTar, o.Konu, o.Aciklama, o.Cihaz, o.BelgeKodu }).ToList()
                        .Select(o => new HaritaKaydiDetayDto
                        {
                            KayitTar = o.KayitTar,
                            Konu = o.Konu,
                            Aciklama = o.Aciklama,
                            Cihaz = o.Cihaz,
                            Kaynak = "TARIHCE",
                            BelgeKodu = o.BelgeKodu,
                            Kullanici = KullaniciAdiCikar(o.Aciklama)
                        }));
                }

                IEnumerable<HaritaKaydiDetayDto> hucreKayitlari;
                if (mod == "pattern")
                {
                    hucreKayitlari = kayitlar.Where(o => o.KayitTar.HasValue
                        && ((int)o.KayitTar.Value.DayOfWeek + 6) % 7 == gun
                        && o.KayitTar.Value.Hour == saat);
                }
                else
                {
                    if (!DateTime.TryParseExact(tarih, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime tarihDeger))
                        return BadRequest(new { message = "Geçersiz tarih." });

                    hucreKayitlari = saat >= 0
                        ? kayitlar.Where(o => o.KayitTar.HasValue && o.KayitTar.Value.Date == tarihDeger && o.KayitTar.Value.Hour == saat)
                        : kayitlar.Where(o => o.KayitTar.HasValue && o.KayitTar.Value.Date == tarihDeger);
                }

                var hucreListesi = hucreKayitlari.OrderByDescending(o => o.KayitTar).ToList();

                var kullaniciDagilim = hucreListesi
                    .GroupBy(o => string.IsNullOrEmpty(o.Kullanici) ? "(Belirtilmemiş)" : o.Kullanici)
                    .Select(g => new { kullanici = g.Key, count = g.Count() })
                    .OrderByDescending(o => o.count)
                    .ToList();

                var modulDagilim = hucreListesi
                    .GroupBy(o => o.Kaynak == "LOG" ? "Sistem Logu" : ModulAdiCikar(o.BelgeKodu))
                    .Select(g => new { modul = g.Key, count = g.Count() })
                    .OrderByDescending(o => o.count)
                    .ToList();

                var kayitDetayi = hucreListesi.Take(200).Select(o => new
                {
                    kayitTar = o.KayitTar.HasValue ? o.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    konu = o.Konu,
                    aciklama = o.Aciklama != null && o.Aciklama.Length > 200 ? o.Aciklama.Substring(0, 200) + "..." : o.Aciklama,
                    cihaz = o.Cihaz,
                    kaynak = o.Kaynak,
                    kullanici = o.Kullanici
                }).ToList();

                return Ok(new
                {
                    totalCount = hucreListesi.Count,
                    kullaniciDagilim,
                    modulDagilim,
                    kayitlar = kayitDetayi
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // tb_BelgeTarihce'de ayrı bir kullanıcı kolonu yok — kim yaptığı Aciklama metnine gömülü
        // (ör. "(İşlem Yapan: Ali Veli)"). Yaygın kalıpları best-effort yakalar; eşleşmezse null döner.
        private static string KullaniciAdiCikar(string aciklama)
        {
            if (string.IsNullOrEmpty(aciklama)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(aciklama,
                @"(?:İşlem Yapan|Yapan|Kayıt Sahibi|Ekleyen|Onaylayan|Reddeden|Atayan|Değiştiren|Düzenleyen)\s*:\s*([^,\)]+)");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string ModulAdiCikar(string belgeKodu)
        {
            if (string.IsNullOrEmpty(belgeKodu)) return "Diğer";
            int idx = belgeKodu.IndexOf('-');
            string aday = idx > 0 ? belgeKodu.Substring(0, idx) : belgeKodu;
            if (aday.Length < 2 || aday.Length > 12) return "Diğer";
            foreach (char c in aday)
            {
                if (!char.IsLetter(c)) return "Diğer";
            }
            return aday.ToUpperInvariant();
        }

        private class HaritaKaydiDetayDto
        {
            public DateTime? KayitTar { get; set; }
            public string Konu { get; set; }
            public string Aciklama { get; set; }
            public string Cihaz { get; set; }
            public string Kaynak { get; set; }
            public string BelgeKodu { get; set; }
            public string Kullanici { get; set; }
        }

        private class TarihSayisiDto
        {
            public string Tarih { get; set; }
            public string GunAdi { get; set; }
            public int Count { get; set; }
        }

        private string GetCurrentSicilNo()
        {
            try
            {
                var kullaniciId = GetCurrentUserId();
                return _context.tb_Kullanici.AsNoTracking().FirstOrDefault(o => o.KullaniciID == kullaniciId)?.SicilNo;
            }
            catch { return null; }
        }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; }
    }

    public class MagazaParametreDegerDto
    {
        public string ParametreKodu { get; set; }
        public string Deger { get; set; }
    }
}
