using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BakimController : ControllerBase
    {
        private readonly IBakimService _bakimService;

        public BakimController(IBakimService bakimService)
        {
            _bakimService = bakimService;
        }

        private string GetCurrentSicilNo()
        {
            var claim = User.FindFirst("SicilNo");
            if (claim != null) return claim.Value;
            throw new UnauthorizedAccessException("Giris yapan kullanicinin Sicil Numarasi bulunamadi.");
        }

        private string GetCurrentAdminBelgeTur()
        {
            var claim = User.FindFirst("AdminBelgeTur");
            return claim != null ? claim.Value : "";
        }

        /// <summary>
        /// ??irket kodu, b?l?m kodu ve arama metnine g?re kayitli makine listesini getirir.
        /// </summary>
        /// <param name="sirketKodu">??irket kodu filtresi.</param>
        /// <param name="bolumKodu">B?l?m kodu filtresi.</param>
        /// <param name="aramaText">Arama yapilacak metin.</param>
        /// <returns>Makinelerin listesini d?ner.</returns>
        [HttpGet("makine")]
        public ActionResult<IEnumerable<MakineDto>> GetMakines([FromQuery] string sirketKodu = "", [FromQuery] string bolumKodu = "", [FromQuery] string aramaText = "")
        {
            try
            {
                var list = _bakimService.GetMakines(sirketKodu, bolumKodu, aramaText);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir makine kaydeder veya mevcut makine bilgilerini g?nceller.
        /// </summary>
        /// <param name="makine">Kaydedilecek makine nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("makine")]
        public IActionResult SaveMakine([FromBody] tb_Makine makine)
        {
            try
            {
                if (string.IsNullOrEmpty(makine.MakineKodu))
                {
                    return BadRequest(new { message = "Makine Kodu bos olamaz." });
                }
                bool success = _bakimService.SaveMakine(makine);
                return Ok(new { success, message = "Makine basariyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bakim mod?l?nde kullanilacak a?ilir liste (dropdown) verilerini (??irket, B?l?m, Hat, Makine vb.) getirir.
        /// </summary>
        /// <returns>Dropdown verilerini i?eren DTO nesnesi d?ner.</returns>
        [HttpGet("dropdowns")]
        public ActionResult<BakimDropdownsDto> GetDropdowns()
        {
            try
            {
                var sicilNo = GetCurrentSicilNo();
                var adminBelgeTur = GetCurrentAdminBelgeTur();
                var dropdowns = _bakimService.GetBakimDropdowns(sicilNo, adminBelgeTur);
                return Ok(dropdowns);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Filtrelere ve sayfalama parametrelerine g?re planlanmis bakim listesini getirir.
        /// </summary>
        /// <param name="sirket">??irket filtresi.</param>
        /// <param name="bolum">B?l?m filtresi.</param>
        /// <param name="hat">Hat filtresi.</param>
        /// <param name="durum">Bakim durumu filtresi.</param>
        /// <param name="bakimTuru">Bakim t?r? filtresi.</param>
        /// <param name="arama">Genel arama metni.</param>
        /// <param name="pageIndex">Sayfa indeksi.</param>
        /// <param name="pageSize">Sayfa boyutu.</param>
        /// <returns>Sayfalanmis bakim plan listesi d?ner.</returns>
        [HttpGet("plan")]
        public ActionResult<PaginatedListDto<BakimPlanDto>> GetBakimPlans(
            [FromQuery] string sirket = "", 
            [FromQuery] string bolum = "", 
            [FromQuery] string hat = "", 
            [FromQuery] string durum = "", 
            [FromQuery] string bakimTuru = "", 
            [FromQuery] string arama = "", 
            [FromQuery] int pageIndex = 1, 
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (data, totalCount) = _bakimService.GetBakimPlanList(sirket, bolum, hat, durum, bakimTuru, arama, pageIndex, pageSize);
                return Ok(new PaginatedListDto<BakimPlanDto> { Data = data, TotalCount = totalCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir bakim plani olusturur veya g?nceller.
        /// </summary>
        /// <param name="request">Bakim plani bilgilerini i?eren istek nesnesi.</param>
        /// <returns>Kayit basarili ise plan kodunu d?ner.</returns>
        [HttpPost("plan")]
        public IActionResult SavePlan([FromBody] SavePlanRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                var code = _bakimService.SaveBakimPlan(request.PlanKodu, request.HatKodu, request.BakimTuru, request.HedefBaslangic, request.HedefBitis, sicil);
                return Ok(new { success = true, planKodu = code });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen kodlu bakim planinin durumunu (Aktif, Tamamlandi, Iptal vb.) g?nceller.
        /// </summary>
        /// <param name="code">Durumu g?ncellenecek bakim planinin kodu.</param>
        /// <param name="request">Yeni durum ve a?iklama notlarini i?eren istek nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("plan/{code}/status")]
        public IActionResult UpdatePlanStatus(string code, [FromBody] UpdatePlanStatusRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                bool success = _bakimService.UpdateBakimPlanStatus(code, request.Durum, request.Not, request.DosyaUrl, sicil);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bakim planina eklenmis olan notlari/gelismeleri listeler.
        /// </summary>
        /// <param name="code">Notlari getirilecek bakim planinin kodu.</param>
        /// <returns>Not listesini d?ner.</returns>
        [HttpGet("plan/{code}/notlar")]
        public ActionResult<IEnumerable<BakimPlanDetayDto>> GetPlanNotlar(string code)
        {
            try
            {
                var list = _bakimService.GetBakimNotlari(code);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen bakim planini sistemden siler.
        /// </summary>
        /// <param name="code">Silinecek planin kodu.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("plan/{code}")]
        public IActionResult DeletePlan(string code)
        {
            try
            {
                bool success = _bakimService.DeleteBakimPlan(code);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bakim planina ait bir gelisme/not kaydini siler.
        /// </summary>
        /// <param name="id">Silinecek gelisme kaydinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("plan/gelisme/{id}")]
        public IActionResult DeletePlanGelisme(int id)
        {
            try
            {
                bool success = _bakimService.DeleteBakimGelisme(id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Filtrelere ve sayfalama parametrelerine g?re periyodik kontrol kayitlarini listeler.
        /// </summary>
        /// <param name="sirket">??irket kodu filtresi.</param>
        /// <param name="bolum">B?l?m kodu filtresi.</param>
        /// <param name="durum">Periyodik kontrol?n durumu filtresi.</param>
        /// <param name="kontrolTuru">Kontrol t?r? filtresi.</param>
        /// <param name="arama">Genel arama metni.</param>
        /// <param name="pageIndex">Sayfa indeksi.</param>
        /// <param name="pageSize">Sayfa boyutu.</param>
        /// <returns>Sayfalanmis periyodik kontrol listesi d?ner.</returns>
        [HttpGet("periyodik")]
        public ActionResult<PaginatedListDto<PeriyodikKontrolDto>> GetPeriyodikKontrols(
            [FromQuery] string sirket = "", 
            [FromQuery] string bolum = "", 
            [FromQuery] string durum = "", 
            [FromQuery] string kontrolTuru = "", 
            [FromQuery] string arama = "", 
            [FromQuery] int pageIndex = 1, 
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (data, totalCount) = _bakimService.GetPeriyodikKontrolList(sirket, bolum, durum, kontrolTuru, arama, pageIndex, pageSize);
                return Ok(new PaginatedListDto<PeriyodikKontrolDto> { Data = data, TotalCount = totalCount });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir periyodik kontrol kaydi olusturur veya g?nceller.
        /// </summary>
        /// <param name="request">Olusturulacak periyodik kontrol detaylarini i?eren istek nesnesi.</param>
        /// <returns>Kayit basarili ise periyodik kontrol kodunu d?ner.</returns>
        [HttpPost("periyodik")]
        public IActionResult SavePeriyodik([FromBody] SavePeriyodikRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                var code = _bakimService.SavePeriyodikKontrol(request.KontrolKodu, request.BolumKodu, request.KontrolTuru, request.HedefBaslangic, request.HedefBitis, request.Aciklama, sicil);
                return Ok(new { success = true, kontrolKodu = code });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen periyodik kontrol kaydinin durumunu g?nceller.
        /// </summary>
        /// <param name="code">Durumu g?ncellenecek periyodik kontrol?n kodu.</param>
        /// <param name="request">Yeni durum ve a?iklama bilgilerini i?eren istek nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("periyodik/{code}/status")]
        public IActionResult UpdatePeriyodikStatus(string code, [FromBody] UpdatePeriyodikStatusRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                bool success = _bakimService.UpdatePeriyodikStatus(code, request.Durum, request.Aciklama, sicil);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen periyodik kontrol kaydini siler.
        /// </summary>
        /// <param name="code">Silinecek periyodik kontrol kodu.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("periyodik/{code}")]
        public IActionResult DeletePeriyodik(string code)
        {
            try
            {
                bool success = _bakimService.DeletePeriyodikKontrol(code);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen periyodik kontrol kaydina ait malzeme sarfiyat listesini getirer.
        /// </summary>
        /// <param name="code">Periyodik kontrol kodu.</param>
        /// <returns>Malzeme sarfiyat listesini d?ner.</returns>
        [HttpGet("periyodik/{code}/sarfiyat")]
        public ActionResult<IEnumerable<PeriyodikSarfiyatDto>> GetPeriyodikSarfiyats(string code)
        {
            try
            {
                var list = _bakimService.GetPeriyodikSarfiyats(code);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Periyodik kontrole ait yeni bir malzeme sarfiyati kaydeder.
        /// </summary>
        /// <param name="code">Malzeme sarfiyati eklenecek periyodik kontrol?n kodu.</param>
        /// <param name="request">Kullanilan malzeme kodu ve miktarini i?eren istek nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("periyodik/{code}/sarfiyat")]
        public IActionResult SavePeriyodikSarfiyat(string code, [FromBody] SaveSarfiyatRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                bool success = _bakimService.SavePeriyodikSarfiyat(code, request.MalzemeKodu, request.Miktar, request.MakineKodu, sicil);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kayitli bir periyodik kontrol malzeme sarfiyat kaydini siler.
        /// </summary>
        /// <param name="id">Silinecek sarfiyat kaydinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("periyodik/sarfiyat/{id}")]
        public IActionResult DeletePeriyodikSarfiyat(int id)
        {
            try
            {
                bool success = _bakimService.DeletePeriyodikSarfiyat(id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Periyodik kontrole ait eklenen gelismeleri/notlari getirir.
        /// </summary>
        /// <param name="code">Gelismeleri getirilecek periyodik kontrol kodu.</param>
        /// <returns>Gelisme/not listesini d?ner.</returns>
        [HttpGet("periyodik/{code}/gelisme")]
        public ActionResult<IEnumerable<BakimPlanDetayDto>> GetPeriyodikGelismeler(string code)
        {
            try
            {
                var list = _bakimService.GetPeriyodikGelismeler(code);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Periyodik kontrol i?in yeni bir gelisme/not kaydeder.
        /// </summary>
        /// <param name="code">Gelisme eklenecek periyodik kontrol?n kodu.</param>
        /// <param name="request">Gelisme a?iklamasi ve dosya yolu bilgilerini i?eren istek nesnesi.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("periyodik/{code}/gelisme")]
        public IActionResult SavePeriyodikGelisme(string code, [FromBody] SavePeriyodikGelismeRequest request)
        {
            try
            {
                var sicil = GetCurrentSicilNo();
                bool success = _bakimService.SavePeriyodikGelisme(code, request.Aciklama, request.DosyaUrl, sicil);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Periyodik kontrole ait bir gelisme/not kaydini siler.
        /// </summary>
        /// <param name="id">Silinecek gelismenin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpDelete("periyodik/gelisme/{id}")]
        public IActionResult DeletePeriyodikGelisme(int id)
        {
            try
            {
                bool success = _bakimService.DeletePeriyodikGelisme(id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Malzeme listesinde arama yapar.
        /// </summary>
        /// <param name="term">Arama terimi.</param>
        /// <param name="page">Sayfa numarasi.</param>
        /// <param name="pageSize">Her sayfadaki malzeme sayisi.</param>
        /// <param name="sarfOnly">Sadece sarf malzemelerini mi listeleyecegini belirten bayrak.</param>
        /// <returns>Malzeme arama sonu?larini i?eren modeli d?ner.</returns>
        [HttpGet("malzeme")]
        public ActionResult<MalzemeSearchResponseDto> SearchMalzemes([FromQuery] string term = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool sarfOnly = true)
        {
            try
            {
                var (results, hasMore) = _bakimService.SearchMalzemes(term, page, pageSize, sarfOnly);
                return Ok(new MalzemeSearchResponseDto { Results = results, Pagination = new MalzemePaginationDto { More = hasMore } });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bakim personel performans raporunu getirir.
        /// </summary>
        /// <param name="yil">Yil filtresi.</param>
        /// <param name="ay">Ay filtresi.</param>
        /// <param name="sirket">??irket kodu filtresi.</param>
        /// <returns>Personel performans raporu listesini d?ner.</returns>
        [HttpGet("rapor/personel")]
        public ActionResult<IEnumerable<PersonelPerformansRaporuDto>> GetPersonelPerformansRaporu([FromQuery] string yil = "2026", [FromQuery] string ay = "Tümü", [FromQuery] string sirket = "")
        {
            try
            {
                var list = _bakimService.GetPersonelPerformansRaporu(yil, ay, sirket);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bakim genel dashboard/KPI rapor verilerini getirir.
        /// </summary>
        /// <param name="yillar">Virg?lle ayrilmis yillar listesi (?rn: "2026").</param>
        /// <param name="sirket">??irket kodu filtresi.</param>
        /// <returns>Dashboard/KPI istatistik verilerini d?ner.</returns>
        [HttpGet("rapor/dashboard")]
        public ActionResult<IEnumerable<BakimDashboardStatsDto>> GetBakimDashboardStats([FromQuery] string yillar = "2026", [FromQuery] string sirket = "")
        {
            try
            {
                var list = _bakimService.GetBakimDashboardStats(yillar, sirket);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Referans WebServiceBakimPlani.DashboardOzetGetir birebir (planlı/periyodik özet)
        [HttpGet("dashboard-ozet")]
        public IActionResult GetDashboardOzet([FromQuery] string sirket = "")
        {
            try
            {
                return Ok(_bakimService.GetDashboardOzet(sirket));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Referans WebServiceBakimRapor.PersonelPerformansRaporuGetir birebir (Bakım HelpDesk)
        [HttpGet("rapor/performans")]
        public IActionResult GetBakimPerformans([FromQuery] string yil = "", [FromQuery] string ay = "", [FromQuery] string sirket = "")
        {
            try
            {
                return Ok(_bakimService.GetBakimHelpDeskPerformans(yil, ay, sirket));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
