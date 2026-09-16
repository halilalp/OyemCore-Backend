using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Bakım Planı / Periyodik Kontrol Planı ortak "Temizlik Onay Formu" akışı.
    // Referans: WebPortal App_Code/WebServicePlanTemizlikOnay.cs.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TemizlikOnayController : ControllerBase
    {
        private readonly ITemizlikOnayService _temizlikOnayService;

        public TemizlikOnayController(ITemizlikOnayService temizlikOnayService)
        {
            _temizlikOnayService = temizlikOnayService;
        }

        private string GetCurrentSicilNo()
        {
            var claim = User.FindFirst("SicilNo");
            if (claim != null) return claim.Value;
            throw new UnauthorizedAccessException("Giris yapan kullanicinin Sicil Numarasi bulunamadi.");
        }

        /// <summary>
        /// Belirtilen plan/kontrol icin en son temizlik onay kaydinin durumunu getirir.
        /// </summary>
        [HttpGet("durum")]
        public ActionResult<TemizlikOnayDurumDto> GetDurum([FromQuery] string planTuru, [FromQuery] string planKodu)
        {
            try
            {
                return Ok(_temizlikOnayService.GetDurum(planTuru, planKodu));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Giris yapan kullanicinin doldurmasi bekleyen temizlik onay formlarini listeler.
        /// </summary>
        [HttpGet("bekleyenlerim")]
        public ActionResult<IEnumerable<TemizlikOnayBekleyenDto>> GetBekleyenlerim()
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                return Ok(_temizlikOnayService.GetBekleyenlerim(sicil));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Doldurulacak formun mevcut icerigini (8 madde + plan aciklamasi) getirir.
        /// </summary>
        [HttpGet("{onayId}")]
        public ActionResult<TemizlikOnayDetayDto> GetDetay(int onayId)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                return Ok(_temizlikOnayService.GetDetay(onayId, sicil));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Formu onaylar: 8 maddeyi kaydeder, plan/kontrolu TAMAMLANDI durumuna gecirir.
        /// </summary>
        [HttpPost("{onayId}/kaydet")]
        public IActionResult Kaydet(int onayId, [FromBody] SaveTemizlikOnayRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                _temizlikOnayService.Kaydet(onayId, sicil, request);
                return Ok(new { success = true, message = "Temizlik onay formu başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Formu reddeder: islem tamamlanmadi olarak DEVAM durumuna geri doner.
        /// </summary>
        [HttpPost("{onayId}/reddet")]
        public IActionResult Reddet(int onayId, [FromBody] RejectTemizlikOnayRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                _temizlikOnayService.Reddet(onayId, sicil, request?.Aciklama);
                return Ok(new { success = true, message = "İşlem, tamamlanmadı olarak geri gönderildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
