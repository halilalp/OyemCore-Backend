using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;

namespace WebPortalSpace.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly IWebHostEnvironment _env;

        public TicketsController(ITicketService ticketService, IWebHostEnvironment env)
        {
            _ticketService = ticketService;
            _env = env;
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
        /// Ticket modülünün başlangıç yapılandırma (şirket listesi, yetkili kullanıcı vb.) verilerini getirir.
        /// </summary>
        /// <returns>Ticket modülü başlangıç konfigürasyonunu döner.</returns>
        [HttpGet("init")]
        public IActionResult InitConfig()
        {
            try
            {
                int userId = GetCurrentUserId();
                var result = _ticketService.InitConfig(userId);
                if (result.Success)
                {
                    return Ok(result.Data);
                }
                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Arama kriterlerine ve sayfalama parametrelerine göre bilet (ticket) listesini ve durum sayılarını getirir.
        /// </summary>
        /// <param name="sirketKodu">Filtrelenecek şirket kodu.</param>
        /// <param name="aramaText">Arama yapılacak metin filtresi.</param>
        /// <param name="pageIndex">Sayfa indeksi (Varsayılan: 1).</param>
        /// <param name="pageSize">Sayfa boyutu (Varsayılan: 20).</param>
        /// <returns>Ticket listesi ve durum sayaçlarını döner.</returns>
        [HttpGet]
        public IActionResult GetTickets([FromQuery] string sirketKodu = "", [FromQuery] string aramaText = "", [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                int userId = GetCurrentUserId();
                var (tickets, counts) = _ticketService.GetTicketList(userId, sirketKodu, aramaText, pageIndex, pageSize);
                return Ok(new { tickets, counts });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen ticket ID'sine göre bilet detaylarını, dosyalarını ve yorumlarını getirir.
        /// </summary>
        /// <param name="id">Detayı getirilmek istenen ticket ID değeri.</param>
        /// <returns>Ticket detay bilgisini döner.</returns>
        [HttpGet("{id}")]
        public IActionResult GetTicketDetail(int id)
        {
            try
            {
                var detail = _ticketService.GetTicketDetail(id);
                if (detail == null) return NotFound(new { message = "Kayıt bulunamadı." });
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir ticket oluşturur veya mevcut ticket bilgilerini günceller.
        /// </summary>
        /// <param name="ticket">Kaydedilecek ticket bilgilerini içeren nesne.</param>
        /// <returns>Kayıt başarılı ise ticket ID ve mesaj bilgisini döner.</returns>
        [HttpPost]
        public IActionResult SaveTicket([FromBody] tb_Ticket ticket)
        {
            try
            {
                int userId = GetCurrentUserId();
                string ticketIdStr = _ticketService.SaveTicket(userId, ticket);
                return Ok(new { id = int.Parse(ticketIdStr), message = "Ticket başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Biletin durumunu (Yeni, Devam Ediyor, Tamamlandı vb.) ve isteğe bağlı olarak Kanban sırasını günceller.
        /// </summary>
        /// <param name="id">Durumu güncellenecek ticket ID değeri.</param>
        /// <param name="request">Yeni durum ve sürüklenen ticket ID bilgilerini içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/status")]
        public IActionResult UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _ticketService.UpdateTicketStatus(userId, id, request.YeniDurum, request.DraggedID);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Biletin çözümü için bir destek personeli atar.
        /// </summary>
        /// <param name="id">Atama yapılacak ticket ID değeri.</param>
        /// <param name="request">Atanacak personelin sicil numarasını içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/assign")]
        public IActionResult Assign(int id, [FromBody] AssignRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _ticketService.AssignTicket(userId, id, request.SicilNo);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bilet ile ilgili yeni bir yorum/gelişme ekler.
        /// </summary>
        /// <param name="id">Yorum eklenecek ticket ID değeri.</param>
        /// <param name="request">Yorum açıklamasını içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/comment")]
        public IActionResult SaveComment(int id, [FromBody] SaveCommentRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _ticketService.SaveComment(userId, id, request.Aciklama);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bilet için bir ek dosya (resim, pdf vb.) yükler ve veritabanına kaydeder.
        /// </summary>
        /// <param name="id">Dosya yüklenecek ticket ID değeri.</param>
        /// <param name="file">Yüklenen form dosyası.</param>
        /// <returns>İşlem başarılı ise dosya adını ve benzersiz adını döner.</returns>
        [HttpPost("{id}/upload")]
        [Consumes("multipart/form-data")]
        public IActionResult UploadFile(int id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Geçersiz dosya." });
                }

                string webRoot = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                }

                string uploadDir = Path.Combine(webRoot, "Ticket", "Docs");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                string ext = Path.GetExtension(file.FileName);
                string uniqueName = $"{Guid.NewGuid()}{ext}";
                string fullPath = Path.Combine(uploadDir, uniqueName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                bool success = _ticketService.SaveFile(id, file.FileName, uniqueName, file.ContentType);
                if (success)
                {
                    return Ok(new { success = true, filename = file.FileName, uniqueName });
                }
                return BadRequest(new { message = "Dosya veritabanına kaydedilemedi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kanban tahtasında biletlerin sıralamasını (sira) ve kolonunu toplu olarak günceller.
        /// </summary>
        /// <param name="request">Sıralanmış ticket ID'leri ve yeni durum bilgilerini içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("sira")]
        public IActionResult UpdateSira([FromBody] UpdateSiraRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _ticketService.UpdateTicketSira(userId, request.TicketIDs, request.YeniDurum, request.DraggedID);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen ticket'ı ve fiziksel ek dosyalarını sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek ticket ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                string result = _ticketService.DeleteTicket(userId, id, _env.WebRootPath ?? _env.ContentRootPath);
                if (result == "1")
                {
                    return Ok(new { success = true });
                }
                return BadRequest(new { message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Ticket modülüyle ilişkili tüm şirket listesini getirir.
        /// </summary>
        /// <returns>Şirket listesini döner.</returns>
        [HttpGet("companies")]
        public IActionResult GetCompanies()
        {
            try
            {
                var list = _ticketService.GetCompanies();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Ticket atanabilecek tüm aktif personellerin listesini getirir.
        /// </summary>
        /// <returns>Personel listesini döner.</returns>
        [HttpGet("personels")]
        public IActionResult GetPersonels()
        {
            try
            {
                var list = _ticketService.GetPersonels();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bilet istatistiklerini (Örn. toplam açılan, kapanan ticket sayıları) ve filtreleri döner.
        /// </summary>
        /// <param name="sirketKodu">Şirket kodu filtresi.</param>
        /// <param name="ay">Ay filtresi.</param>
        /// <param name="fltYil">Yıl filtresi.</param>
        /// <param name="fltAy">Ay filtresi.</param>
        /// <returns>Ticket istatistiklerini içeren modeli döner.</returns>
        [HttpGet("stats")]
        public IActionResult GetStats([FromQuery] string sirketKodu = "", [FromQuery] int ay = 0, [FromQuery] int fltYil = 0, [FromQuery] int fltAy = 0)
        {
            try
            {
                int userId = GetCurrentUserId();
                var stats = _ticketService.GetDashboardStats(userId, sirketKodu, ay, fltYil, fltAy);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class UpdateStatusRequest
    {
        public string YeniDurum { get; set; }
        public int? DraggedID { get; set; }
    }

    public class AssignRequest
    {
        public string SicilNo { get; set; }
    }

    public class SaveCommentRequest
    {
        public string Aciklama { get; set; }
    }

    public class UpdateSiraRequest
    {
        public List<int> TicketIDs { get; set; }
        public string YeniDurum { get; set; }
        public int? DraggedID { get; set; }
    }
}
