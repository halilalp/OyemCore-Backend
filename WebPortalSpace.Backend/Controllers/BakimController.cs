using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Dtos;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;

namespace WebPortalSpace.Backend.Controllers
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
            throw new UnauthorizedAccessException("Giriş yapan kullanıcının Sicil Numarası bulunamadı.");
        }

        private string GetCurrentAdminBelgeTur()
        {
            var claim = User.FindFirst("AdminBelgeTur");
            return claim != null ? claim.Value : "";
        }

        /// <summary>
        /// Şirket kodu, bölüm kodu ve arama metnine göre kayıtlı makine listesini getirir.
        /// </summary>
        /// <param name="sirketKodu">Şirket kodu filtresi.</param>
        /// <param name="bolumKodu">Bölüm kodu filtresi.</param>
        /// <param name="aramaText">Arama yapılacak metin.</param>
        /// <returns>Makinelerin listesini döner.</returns>
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
        /// Yeni bir makine kaydeder veya mevcut makine bilgilerini günceller.
        /// </summary>
        /// <param name="makine">Kaydedilecek makine nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("makine")]
        public IActionResult SaveMakine([FromBody] tb_Makine makine)
        {
            try
            {
                if (string.IsNullOrEmpty(makine.MakineKodu))
                {
                    return BadRequest(new { message = "Makine Kodu boş olamaz." });
                }
                bool success = _bakimService.SaveMakine(makine);
                return Ok(new { success, message = "Makine başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bakım modülünde kullanılacak açılır liste (dropdown) verilerini (Şirket, Bölüm, Hat, Makine vb.) getirir.
        /// </summary>
        /// <returns>Dropdown verilerini içeren DTO nesnesi döner.</returns>
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
        /// Filtrelere ve sayfalama parametrelerine göre planlanmış bakım listesini getirir.
        /// </summary>
        /// <param name="sirket">Şirket filtresi.</param>
        /// <param name="bolum">Bölüm filtresi.</param>
        /// <param name="hat">Hat filtresi.</param>
        /// <param name="durum">Bakım durumu filtresi.</param>
        /// <param name="bakimTuru">Bakım türü filtresi.</param>
        /// <param name="arama">Genel arama metni.</param>
        /// <param name="pageIndex">Sayfa indeksi.</param>
        /// <param name="pageSize">Sayfa boyutu.</param>
        /// <returns>Sayfalanmış bakım plan listesi döner.</returns>
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
        /// Yeni bir bakım planı oluşturur veya günceller.
        /// </summary>
        /// <param name="request">Bakım planı bilgilerini içeren istek nesnesi.</param>
        /// <returns>Kayıt başarılı ise plan kodunu döner.</returns>
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
        /// Belirtilen kodlu bakım planının durumunu (Aktif, Tamamlandı, İptal vb.) günceller.
        /// </summary>
        /// <param name="code">Durumu güncellenecek bakım planının kodu.</param>
        /// <param name="request">Yeni durum ve açıklama notlarını içeren istek nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Bakım planına eklenmiş olan notları/gelişmeleri listeler.
        /// </summary>
        /// <param name="code">Notları getirilecek bakım planının kodu.</param>
        /// <returns>Not listesini döner.</returns>
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
        /// Belirtilen bakım planını sistemden siler.
        /// </summary>
        /// <param name="code">Silinecek planın kodu.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Bakım planına ait bir gelişme/not kaydını siler.
        /// </summary>
        /// <param name="id">Silinecek gelişme kaydının ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Filtrelere ve sayfalama parametrelerine göre periyodik kontrol kayıtlarını listeler.
        /// </summary>
        /// <param name="sirket">Şirket kodu filtresi.</param>
        /// <param name="bolum">Bölüm kodu filtresi.</param>
        /// <param name="durum">Periyodik kontrolün durumu filtresi.</param>
        /// <param name="kontrolTuru">Kontrol türü filtresi.</param>
        /// <param name="arama">Genel arama metni.</param>
        /// <param name="pageIndex">Sayfa indeksi.</param>
        /// <param name="pageSize">Sayfa boyutu.</param>
        /// <returns>Sayfalanmış periyodik kontrol listesi döner.</returns>
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
        /// Yeni bir periyodik kontrol kaydı oluşturur veya günceller.
        /// </summary>
        /// <param name="request">Oluşturulacak periyodik kontrol detaylarını içeren istek nesnesi.</param>
        /// <returns>Kayıt başarılı ise periyodik kontrol kodunu döner.</returns>
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
        /// Belirtilen periyodik kontrol kaydının durumunu günceller.
        /// </summary>
        /// <param name="code">Durumu güncellenecek periyodik kontrolün kodu.</param>
        /// <param name="request">Yeni durum ve açıklama bilgilerini içeren istek nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Belirtilen periyodik kontrol kaydını siler.
        /// </summary>
        /// <param name="code">Silinecek periyodik kontrol kodu.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Belirtilen periyodik kontrol kaydına ait malzeme sarfiyat listesini getirer.
        /// </summary>
        /// <param name="code">Periyodik kontrol kodu.</param>
        /// <returns>Malzeme sarfiyat listesini döner.</returns>
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
        /// Periyodik kontrole ait yeni bir malzeme sarfiyatı kaydeder.
        /// </summary>
        /// <param name="code">Malzeme sarfiyatı eklenecek periyodik kontrolün kodu.</param>
        /// <param name="request">Kullanılan malzeme kodu ve miktarını içeren istek nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Kayıtlı bir periyodik kontrol malzeme sarfiyat kaydını siler.
        /// </summary>
        /// <param name="id">Silinecek sarfiyat kaydının ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Periyodik kontrole ait eklenen gelişmeleri/notları getirir.
        /// </summary>
        /// <param name="code">Gelişmeleri getirilecek periyodik kontrol kodu.</param>
        /// <returns>Gelişme/not listesini döner.</returns>
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
        /// Periyodik kontrol için yeni bir gelişme/not kaydeder.
        /// </summary>
        /// <param name="code">Gelişme eklenecek periyodik kontrolün kodu.</param>
        /// <param name="request">Gelişme açıklaması ve dosya yolu bilgilerini içeren istek nesnesi.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Periyodik kontrole ait bir gelişme/not kaydını siler.
        /// </summary>
        /// <param name="id">Silinecek gelişmenin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// <param name="page">Sayfa numarası.</param>
        /// <param name="pageSize">Her sayfadaki malzeme sayısı.</param>
        /// <param name="sarfOnly">Sadece sarf malzemelerini mi listeleyeceğini belirten bayrak.</param>
        /// <returns>Malzeme arama sonuçlarını içeren modeli döner.</returns>
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
        /// Bakım personel performans raporunu getirir.
        /// </summary>
        /// <param name="yil">Yıl filtresi.</param>
        /// <param name="ay">Ay filtresi.</param>
        /// <param name="sirket">Şirket kodu filtresi.</param>
        /// <returns>Personel performans raporu listesini döner.</returns>
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
        /// Bakım genel dashboard/KPI rapor verilerini getirir.
        /// </summary>
        /// <param name="yillar">Virgülle ayrılmış yıllar listesi (örn: "2026").</param>
        /// <param name="sirket">Şirket kodu filtresi.</param>
        /// <returns>Dashboard/KPI istatistik verilerini döner.</returns>
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
    }
}
