using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.Backend.Controllers
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
                if (!success) return BadRequest(new { message = "Proje siralamasi g?ncellenemedi." });
                return Ok(new { message = "Siralama g?ncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Siralama g?ncellenirken hata olustu: {ex.Message}" });
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
                if (!success) return BadRequest(new { message = "Sayfa siralamasi g?ncellenemedi." });
                return Ok(new { message = "Siralama g?ncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Siralama g?ncellenirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Belge tarih?esi alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"??ifre g?ncellenirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Talep t?rleri alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"??irket listesi alinirken hata olustu: {ex.Message}" });
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
                return BadRequest(new { message = $"Belge tarih?esi listelenirken hata olustu: {ex.Message}" });
            }
        }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; }
    }
}
