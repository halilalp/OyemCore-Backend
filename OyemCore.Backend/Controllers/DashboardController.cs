using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
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
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        /// <summary>
        /// Belirtilen sehir i?in g?ncel hava durumu bilgilerini getirir.
        /// </summary>
        /// <param name="city">Hava durumu sorgulanacak sehir adi (Varsayilan: IZMIR).</param>
        /// <returns>Hava durumu verilerini i?eren modeli d?ner.</returns>
        [HttpGet("weather")]
        public async Task<IActionResult> GetWeather([FromQuery] string city = "IZMIR")
        {
            try
            {
                var weather = await _dashboardService.GetWeatherAsync(city);
                if (weather == null)
                {
                    return BadRequest(new { message = $"{city} ili i?in hava durumu verisi alinamadi." });
                }
                return Ok(weather);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hava durumu alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// G?ncel d?viz kurlarini (Dolar, Euro vb.) merkez bankasi veya dis servislerden asenkron olarak getirir.
        /// </summary>
        /// <returns>D?viz kurlari listesini d?ner.</returns>
        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies()
        {
            try
            {
                var currencies = await _dashboardService.GetCurrenciesAsync();
                if (currencies == null)
                {
                    return BadRequest(new { message = "D?viz kurlari alinamadi." });
                }
                return Ok(currencies);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"D?viz kurlari alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Bug?n dogum g?n? olan personellerin listesini getirir.
        /// </summary>
        /// <returns>Dogum g?n? olan personel listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Dogum g?nleri alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemdeki g?ncel veya planlanan egitimlerin listesini getirir.
        /// </summary>
        /// <returns>Egitim listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Egitimler alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// ??irket i?i personel telefon ve iletisim rehberini getirir.
        /// </summary>
        /// <returns>Personel rehberi rehber listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Personel rehberi alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sistemdeki g?ncel duyurulari ve haberleri listeler.
        /// </summary>
        /// <returns>Duyuru ve haber listesini d?ner.</returns>
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
                return BadRequest(new { message = $"Duyurular alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Belirtilen ID degerine sahip duyurunun detay i?erigini getirir.
        /// </summary>
        /// <param name="id">Detayi getirilmek istenen duyurunun ID degeri.</param>
        /// <returns>Duyuru detay bilgisini d?ner.</returns>
        [HttpGet("news/{id}")]
        public IActionResult GetNewsDetail(int id)
        {
            try
            {
                var detail = _dashboardService.GetNewsDetail(id);
                if (detail == null)
                {
                    return NotFound(new { message = "Duyuru bulunamadi." });
                }
                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Duyuru detayi alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Giris yapmis kullanicinin yetki durumuna g?re sol men? veya uygulama i?i men? yapisini getirir.
        /// </summary>
        /// <returns>Kullanici yetkilerine uygun men? elemanlarini d?ner.</returns>
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
                return BadRequest(new { message = $"Men? verisi alinirken hata olustu: {ex.Message}" });
            }
        }

        /// <summary>
        /// Veritabani baglantisini test etmek ve hata ayiklama bilgilerini almak amaciyla kullanilir.
        /// </summary>
        /// <returns>Veritabani durumunu ve baglanti test sonu?larini i?eren debug bilgisi d?ner.</returns>
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
