using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Interfaces;

namespace WebPortalSpace.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
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
        /// Belirtilen şehir için güncel hava durumu bilgilerini getirir.
        /// </summary>
        /// <param name="city">Hava durumu sorgulanacak şehir adı (Varsayılan: İZMİR).</param>
        /// <returns>Hava durumu verilerini içeren modeli döner.</returns>
        [HttpGet("weather")]
        public async Task<IActionResult> GetWeather([FromQuery] string city = "İZMİR")
        {
            try
            {
                var weather = await _dashboardService.GetWeatherAsync(city);
                if (weather == null)
                {
                    return BadRequest(new { message = $"{city} ili için hava durumu verisi alınamadı." });
                }
                return Ok(weather);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hava durumu alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Güncel döviz kurlarını (Dolar, Euro vb.) merkez bankası veya dış servislerden asenkron olarak getirir.
        /// </summary>
        /// <returns>Döviz kurları listesini döner.</returns>
        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies()
        {
            try
            {
                var currencies = await _dashboardService.GetCurrenciesAsync();
                if (currencies == null)
                {
                    return BadRequest(new { message = "Döviz kurları alınamadı." });
                }
                return Ok(currencies);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Döviz kurları alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Bugün doğum günü olan personellerin listesini getirir.
        /// </summary>
        /// <returns>Doğum günü olan personel listesini döner.</returns>
        [HttpGet("birthdays")]
        public IActionResult GetBirthdays()
        {
            try
            {
                var birthdays = _dashboardService.GetBirthdays();
                return Ok(birthdays);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Doğum günleri alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemdeki güncel veya planlanan eğitimlerin listesini getirir.
        /// </summary>
        /// <returns>Eğitim listesini döner.</returns>
        [HttpGet("trainings")]
        public IActionResult GetTrainings()
        {
            try
            {
                var trainings = _dashboardService.GetTrainings();
                return Ok(trainings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Eğitimler alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Şirket içi personel telefon ve iletişim rehberini getirir.
        /// </summary>
        /// <returns>Personel rehberi rehber listesini döner.</returns>
        [HttpGet("contacts")]
        public IActionResult GetContacts()
        {
            try
            {
                var contacts = _dashboardService.GetContacts();
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Personel rehberi alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemdeki güncel duyuruları ve haberleri listeler.
        /// </summary>
        /// <returns>Duyuru ve haber listesini döner.</returns>
        [HttpGet("news")]
        public IActionResult GetNews()
        {
            try
            {
                var news = _dashboardService.GetNews();
                return Ok(news);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Duyurular alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen ID değerine sahip duyurunun detay içeriğini getirir.
        /// </summary>
        /// <param name="id">Detayı getirilmek istenen duyurunun ID değeri.</param>
        /// <returns>Duyuru detay bilgisini döner.</returns>
        [HttpGet("news/{id}")]
        public IActionResult GetNewsDetail(int id)
        {
            try
            {
                var detail = _dashboardService.GetNewsDetail(id);
                if (detail == null)
                {
                    return NotFound(new { message = "Duyuru bulunamadı." });
                }
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Duyuru detayı alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Giriş yapmış kullanıcının yetki durumuna göre sol menü veya uygulama içi menü yapısını getirir.
        /// </summary>
        /// <returns>Kullanıcı yetkilerine uygun menü elemanlarını döner.</returns>
        [HttpGet("menu")]
        public IActionResult GetMenu()
        {
            try
            {
                int userId = GetCurrentUserId();
                var menu = _dashboardService.GetMenu(userId);
                return Ok(menu);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Menü verisi alınırken hata oluştu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Veritabanı bağlantısını test etmek ve hata ayıklama bilgilerini almak amacıyla kullanılır.
        /// </summary>
        /// <returns>Veritabanı durumunu ve bağlantı test sonuçlarını içeren debug bilgisi döner.</returns>
        [HttpGet("db-debug")]
        [AllowAnonymous]
        public IActionResult DbDebug()
        {
            try
            {
                var debugData = _dashboardService.DbDebug();
                return Ok(debugData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
