using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly ITenantService _tenantService;
        private readonly IWebHostEnvironment _env;

        public TicketsController(ITicketService ticketService, ITenantService tenantService, IWebHostEnvironment env)
        {
            _ticketService = ticketService;
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
        /// Ticket mod?l?n?n baslangi? yapilandirma (sirket listesi, yetkili kullanici vb.) verilerini getirir.
        /// </summary>
        /// <returns>Ticket mod?l? baslangi? konfig?rasyonunu d?ner.</returns>
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
        /// Arama kriterlerine ve sayfalama parametrelerine g?re bilet (ticket) listesini ve durum sayilarini getirir.
        /// </summary>
        /// <param name="sirketKodu">Filtrelenecek sirket kodu.</param>
        /// <param name="aramaText">Arama yapilacak metin filtresi.</param>
        /// <param name="pageIndex">Sayfa indeksi (Varsayilan: 1).</param>
        /// <param name="pageSize">Sayfa boyutu (Varsayilan: 20).</param>
        /// <returns>Ticket listesi ve durum saya?larini d?ner.</returns>
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
        /// Belirtilen ticket ID'sine g?re bilet detaylarini, dosyalarini ve yorumlarini getirir.
        /// </summary>
        /// <param name="id">Detayi getirilmek istenen ticket ID degeri.</param>
        /// <returns>Ticket detay bilgisini d?ner.</returns>
        [HttpGet("{id}")]
        public IActionResult GetTicketDetail(int id)
        {
            try
            {
                var detail = _ticketService.GetTicketDetail(id);
                if (detail == null) return NotFound(new { message = "Kayit bulunamadi." });
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir ticket olusturur veya mevcut ticket bilgilerini g?nceller.
        /// </summary>
        /// <param name="ticket">Kaydedilecek ticket bilgilerini i?eren nesne.</param>
        /// <returns>Kayit basarili ise ticket ID ve mesaj bilgisini d?ner.</returns>
        [HttpPost]
        public IActionResult SaveTicket([FromBody] tb_Ticket ticket)
        {
            try
            {
                int userId = GetCurrentUserId();
                string ticketIdStr = _ticketService.SaveTicket(userId, ticket);
                return Ok(new { id = int.Parse(ticketIdStr), message = "Ticket basariyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Biletin durumunu (Yeni, Devam Ediyor, Tamamlandi vb.) ve istege bagli olarak Kanban sirasini g?nceller.
        /// </summary>
        /// <param name="id">Durumu g?ncellenecek ticket ID degeri.</param>
        /// <param name="request">Yeni durum ve s?r?klenen ticket ID bilgilerini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
        /// Biletin ??z?m? i?in bir destek personeli atar.
        /// </summary>
        /// <param name="id">Atama yapilacak ticket ID degeri.</param>
        /// <param name="request">Atanacak personelin sicil numarasini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
        /// Bilet ile ilgili yeni bir yorum/gelisme ekler.
        /// </summary>
        /// <param name="id">Yorum eklenecek ticket ID degeri.</param>
        /// <param name="request">Yorum a?iklamasini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/comment")]
        public IActionResult SaveComment(int id, [FromBody] SaveCommentRequest request)
        {
            try
            {
                int userId = GetCurrentUserId();
                if (request == null || string.IsNullOrWhiteSpace(request.Aciklama))
                    return BadRequest(new { message = "Gelişme açıklaması boş olamaz." });
                bool success = _ticketService.SaveComment(userId, id, request.Aciklama);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                var detay = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest(new { message = detay });
            }
        }

        /// <summary>
        /// Bilet i?in bir ek dosya (resim, pdf vb.) y?kler ve veritabanina kaydeder.
        /// </summary>
        /// <param name="id">Dosya y?klenecek ticket ID degeri.</param>
        /// <param name="dto">Y?klenen dosya verilerini i?eren nesne.</param>
        /// <returns>Islem basarili ise dosya url ve bilgilerini d?ner.</returns>
        [HttpPost("{id}/upload-file")]
        public async Task<IActionResult> UploadFile(int id, [FromBody] FileUploadDto dto)
        {
            string storageFolder = "";
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.FileBase64) || string.IsNullOrEmpty(dto.FileName))
                {
                    return BadRequest(new { message = "Dosya verisi gecersiz." });
                }

                string modulePath = _tenantService.GetModulPath("TICKET");
                string ext = Path.GetExtension(dto.FileName);
                string uniqueName = $"{DateTime.Now:yyMMddHHmmssfff}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{ext}";
                string relativeUrl;

                if (_tenantService.IsStorageRemote())
                {
                    string remoteRelativePath = $"{modulePath}/{uniqueName}".Replace("\\", "/").Replace("//", "/");
                    var uploadResult = await _tenantService.UploadToRemoteStorageAsync(remoteRelativePath, dto.FileBase64);
                    if (!uploadResult.Success)
                    {
                        return BadRequest(new { message = $"Webportal'a yükleme başarısız: {uploadResult.Error}" });
                    }
                    relativeUrl = $"/{uploadResult.RelativePath}";
                }
                else
                {
                    storageFolder = _tenantService.ResolveLocalStorageFolder(_env.ContentRootPath);

                    string uploadDir = Path.Combine(storageFolder, modulePath);
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    string fullPath = Path.Combine(uploadDir, uniqueName);

                    byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);
                    System.IO.File.WriteAllBytes(fullPath, fileBytes);

                    relativeUrl = $"/{modulePath}/{uniqueName}".Replace("\\", "/").Replace("//", "/");
                }

                // Referans (webportal) convention'ı: tb_TicketDosya.DosyaYolu'nda SADECE dosya adı
                // saklanır (örn. "abc123.jpg"); tam yol saklanırsa webportal dosyayı bulamıyor.
                var storedFileName = relativeUrl.Split('/').Last();
                var success = _ticketService.SaveFile(id, dto.FileName, storedFileName, ext);
                if (!success)
                {
                    return BadRequest(new { message = "Dosya yüklendi ancak bilete eklenemedi." });
                }

                return Ok(new { success = true, filePath = relativeUrl, fileName = dto.FileName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hata: {ex.Message}. Klasör: {storageFolder}" });
            }
        }

        /// <summary>
        /// Kanban tahtasinda biletlerin siralamasini (sira) ve kolonunu toplu olarak g?nceller.
        /// </summary>
        /// <param name="request">Siralanmis ticket ID'leri ve yeni durum bilgilerini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
        /// Belirtilen ticket'i ve fiziksel ek dosyalarini sistemden siler.
        /// </summary>
        /// <param name="id">Silinecek ticket ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
        /// Ticket mod?l?yle iliskili t?m sirket listesini getirir.
        /// </summary>
        /// <returns>??irket listesini d?ner.</returns>
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
        /// Belirtilen sirkete bagli aktif ticket kategorilerini getirir (yeni kayit formu icin).
        /// </summary>
        [HttpGet("categories")]
        public IActionResult GetCategories([FromQuery] string sirketKodu = "")
        {
            try
            {
                var list = _ticketService.GetCategories(sirketKodu);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Ticket atanabilecek t?m aktif personellerin listesini getirir.
        /// </summary>
        /// <returns>Personel listesini d?ner.</returns>
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
        /// Bilet istatistiklerini (?rn. toplam a?ilan, kapanan ticket sayilari) ve filtreleri d?ner.
        /// </summary>
        /// <param name="sirketKodu">??irket kodu filtresi.</param>
        /// <param name="ay">Ay filtresi.</param>
        /// <param name="fltYil">Yil filtresi.</param>
        /// <param name="fltAy">Ay filtresi.</param>
        /// <returns>Ticket istatistiklerini i?eren modeli d?ner.</returns>
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
