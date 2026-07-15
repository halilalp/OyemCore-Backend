using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TakvimController : ControllerBase
    {
        private readonly ITakvimService _takvimService;
        private readonly ITenantService _tenantService;
        private readonly IWebHostEnvironment _env;

        public TakvimController(ITakvimService takvimService, ITenantService tenantService, IWebHostEnvironment env)
        {
            _takvimService = takvimService;
            _tenantService = tenantService;
            _env = env;
        }

        private void SyncCalendarJson()
        {
            try
            {
                var events = _takvimService.GetTakvimEventsAsync().GetAwaiter().GetResult();

                var jsonData = events.Select(e => new {
                    Id = e.TakvimID.ToString(),
                    title = e.Konu ?? "",
                    start = e.BasTar.HasValue ? e.BasTar.Value.ToString("yyyy-MM-ddTHH:mm") : "",
                    end = e.BitTar.HasValue ? e.BitTar.Value.ToString("yyyy-MM-ddTHH:mm") : "",
                    backgroundColor = e.BgColor ?? "#0F172A",
                    aciklama = e.Aciklama ?? "",
                    konu = e.Konu ?? ""
                }).ToList();

                string json = System.Text.Json.JsonSerializer.Serialize(jsonData, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

                if (_tenantService.IsStorageRemote())
                {
                    // Uzak (URL) storage'lı tenantlarda JSON, webportal'ın kendi takvimini
                    // beslemeye devam edebilmesi için upload webservice'i ile webportal'a yazılır.
                    // Mobil ana sayfa (GetHomeEvents) bu tenantlarda zaten doğrudan DB'den okur.
                    string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                    var result = _tenantService.UploadToRemoteStorageAsync("Dashboard/json/data.json", base64).GetAwaiter().GetResult();
                    if (!result.Success)
                    {
                        Console.WriteLine($"TakvimController.SyncCalendarJson remote upload failed: {result.Error}");
                    }
                    return;
                }

                string storageFolder = _tenantService.ResolveLocalStorageFolder(_env.ContentRootPath);

                string jsonFolder = Path.Combine(storageFolder, "Dashboard", "json");
                Directory.CreateDirectory(jsonFolder);

                string jsonPath = Path.Combine(jsonFolder, "data.json");
                System.IO.File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TakvimController.SyncCalendarJson error: {ex.Message}");
            }
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("Kullanıcı kimliği doğrulanamadı.");
        }

        // JSON tabanlı anasayfa endpoint'i — OS cache sayesinde hızlı, DB bağlantısı yok.
        // Mobil anasayfada ±1 ay penceresi için kullanılır.
        [HttpGet("home")]
        public IActionResult GetHomeEvents([FromQuery] string startDate, [FromQuery] string endDate)
        {
            try
            {
                string jsonPath = _tenantService.IsStorageRemote()
                    ? null
                    : Path.Combine(_tenantService.ResolveLocalStorageFolder(_env.ContentRootPath), "Dashboard", "json", "data.json");

                if (jsonPath == null || !System.IO.File.Exists(jsonPath))
                {
                    // JSON henüz oluşturulmamışsa DB'den fallback
                    var dbStart = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var s) ? s : (DateTime?)null;
                    var dbEnd = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var e) ? e : (DateTime?)null;
                    var dbEvents = _takvimService.GetTakvimEventsAsync(dbStart, dbEnd).GetAwaiter().GetResult();
                    return Ok(dbEvents.Select(ev => new {
                        Id = ev.TakvimID.ToString(), title = ev.Konu ?? "",
                        start = ev.BasTar?.ToString("yyyy-MM-ddTHH:mm") ?? "",
                        end = ev.BitTar?.ToString("yyyy-MM-ddTHH:mm") ?? "",
                        backgroundColor = ev.BgColor ?? "#0F172A",
                        aciklama = ev.Aciklama ?? "", konu = ev.Konu ?? ""
                    }));
                }

                string rawJson = System.IO.File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var allEvents = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(rawJson);

                if (allEvents == null) return Ok(Array.Empty<object>());

                // Tarih filtresi
                DateTime filterStart = !string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var fs) ? fs : DateTime.Today.AddMonths(-1);
                DateTime filterEnd   = !string.IsNullOrEmpty(endDate)   && DateTime.TryParse(endDate,   out var fe) ? fe : DateTime.Today.AddMonths(2);

                var filtered = allEvents.Where(ev => {
                    if (ev.TryGetProperty("start", out var startProp) &&
                        DateTime.TryParse(startProp.GetString(), out var evStart))
                    {
                        return evStart >= filterStart && evStart <= filterEnd;
                    }
                    return true;
                }).ToArray();

                return Ok(filtered);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Takvim verisi alınamadı: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var events = await _takvimService.GetTakvimEventsAsync(startDate, endDate);
                return Ok(events);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Takvim etkinlikleri alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventById(int id)
        {
            try
            {
                var ev = await _takvimService.GetTakvimEventByIdAsync(id);
                if (ev == null) return NotFound(new { message = "Etkinlik bulunamadı." });
                return Ok(ev);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Etkinlik alınırken hata: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] TakvimDto dto)
        {
            try
            {
                var sicilNoClaim = User.FindFirst("SicilNo");
                if (sicilNoClaim != null)
                {
                    dto.KayitSicil = sicilNoClaim.Value;
                }

                var created = await _takvimService.CreateTakvimEventAsync(dto);
                SyncCalendarJson();
                return Ok(created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Etkinlik oluşturulurken hata: {ex.Message}" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] TakvimDto dto)
        {
            try
            {
                var existingEvent = await _takvimService.GetTakvimEventByIdAsync(id);
                if (existingEvent == null) return NotFound(new { message = "Güncellenecek etkinlik bulunamadı." });

                var sicilNoClaim = User.FindFirst("SicilNo");
                if (sicilNoClaim == null || existingEvent.KayitSicil != sicilNoClaim.Value)
                {
                    return Forbid("Bu etkinliği güncelleme yetkiniz yok.");
                }

                // Protect KayitSicil from being overwritten
                dto.KayitSicil = existingEvent.KayitSicil;

                var success = await _takvimService.UpdateTakvimEventAsync(id, dto);
                if (!success) return NotFound(new { message = "Güncellenecek etkinlik bulunamadı." });
                SyncCalendarJson();
                return Ok(new { message = "Etkinlik başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Etkinlik güncellenirken hata: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                var existingEvent = await _takvimService.GetTakvimEventByIdAsync(id);
                if (existingEvent == null) return NotFound(new { message = "Silinecek etkinlik bulunamadı." });

                var sicilNoClaim = User.FindFirst("SicilNo");
                if (sicilNoClaim == null || existingEvent.KayitSicil != sicilNoClaim.Value)
                {
                    return Forbid("Bu etkinliği silme yetkiniz yok.");
                }

                var success = await _takvimService.DeleteTakvimEventAsync(id);
                if (!success) return NotFound(new { message = "Silinecek etkinlik bulunamadı." });
                SyncCalendarJson();
                return Ok(new { message = "Etkinlik silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Etkinlik silinirken hata: {ex.Message}" });
            }
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _takvimService.GetCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kategoriler alınırken hata: {ex.Message}" });
            }
        }
    }
}
