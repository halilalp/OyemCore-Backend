using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.BusinessLayer.Interfaces;
using System.Text.Json;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TedarikciController : ControllerBase
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;

        public TedarikciController(IYbsDbContext context, IPushNotificationService pushNotificationService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
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

        private tb_Kullanici GetCurrentUser()
        {
            int userId = GetCurrentUserId();
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == userId);
            if (user == null) throw new InvalidOperationException("Kullanici bulunamadi.");
            return user;
        }

        // ====================================================================
        // LISTELEME VE DETAY ENDPOINTS
        // ====================================================================

        [HttpGet("list")]
        public IActionResult TalepGetirPaged(
            [FromQuery] string ted = "",
            [FromQuery] string TurKod = "",
            [FromQuery] string Durum = "",
            [FromQuery] string MahsulYil = "",
            [FromQuery] string Arama = "",
            [FromQuery] string BasTar = "",
            [FromQuery] string BitTar = "",
            [FromQuery] int PageIndex = 1,
            [FromQuery] int PageSize = 15)
        {
            try
            {
                var query = _context.viewTedDegList.AsNoTracking();

                if (!string.IsNullOrEmpty(ted))
                    query = query.Where(o => o.TedarikciKodu == ted);
                if (!string.IsNullOrEmpty(TurKod))
                     query = query.Where(o => o.TurKod == TurKod);
                if (!string.IsNullOrEmpty(MahsulYil))
                     query = query.Where(o => o.MahsulYil.ToString() == MahsulYil);

                if (!string.IsNullOrEmpty(BasTar) && DateTime.TryParse(BasTar, out DateTime dtBas))
                    query = query.Where(o => o.KayitTar >= dtBas);
                if (!string.IsNullOrEmpty(BitTar) && DateTime.TryParse(BitTar, out DateTime dtBit))
                    query = query.Where(o => o.KayitTar <= dtBit.Date.AddDays(1).AddTicks(-1));

                if (Durum == "TAMAMLANDI")
                    query = query.Where(o => o.Durum == true);
                else if (Durum == "BEKLEMEDE")
                    query = query.Where(o => o.Durum == null);
                else if (Durum == "IPTAL")
                    query = query.Where(o => o.Durum == false);

                if (!string.IsNullOrEmpty(Arama))
                {
                    string aramaLower = Arama.ToLower().Replace(" ", "");
                    query = query.Where(o =>
                        (o.BelgeNo != null && o.BelgeNo.ToLower().Contains(aramaLower)) ||
                        (o.TedarikciKodu != null && o.TedarikciKodu.ToLower().Contains(aramaLower)) ||
                        (o.Unvan != null && o.Unvan.ToLower().Contains(aramaLower)) ||
                        (o.TedTurTanim != null && o.TedTurTanim.ToLower().Contains(aramaLower))
                    );
                }

                int totalCount = query.Count();

                var pagedData = query.OrderByDescending(o => o.TedDegID)
                                     .Skip((PageIndex - 1) * PageSize)
                                     .Take(PageSize)
                                     .ToList();

                var variable = pagedData.Select(i => new
                {
                    Durum = i.Durum == null ? "BEKLEMEDE" : (i.Durum == true ? "TAMAMLANDI" : "IPTAL EDILDI"),
                    BelgePuani = i.BelgePuani?.ToString() ?? "",
                    FiyatPuani = i.FiyatPuani?.ToString() ?? "",
                    KalitePuani = i.KalitePuani?.ToString() ?? "",
                    TeslimPuani = i.TerminPuani?.ToString() ?? "", // Maps to TerminPuani
                    RiskDurum = i.RiskDurum ?? "",
                    ToplamPuan = i.ToplamPuan?.ToString() ?? "",
                    Sinif = i.Sinif ?? "",
                    i.TedarikciKodu,
                    i.MahsulYil,
                    i.Unvan,
                    i.TedTurTanim,
                    i.TurKod,
                    GelisTarStr = i.GelisTar?.ToString("dd.MM.yyyy") ?? "",
                    KayitPer = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == i.KayitSicil)?.AdSoyad ?? "",
                    KayitTarStr = i.KayitTar?.ToString("dd.MM.yyyy") ?? "",
                    i.BelgeNo,
                    i.Aciklama,
                    i.TedDegID
                }).ToList();

                return Ok(new { totalCount, data = variable });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Tedarik?i degerlendirme listesi alinirken hata olustu: {ex.Message}" });
            }
        }

        // Referans WebServiceTedarikci.GetDashboardStats ile birebir aynı hesaplama.
        [HttpGet("dashboard")]
        public IActionResult GetDashboardStats([FromQuery] int ay = 0)
        {
            try
            {
                var q = _context.viewTedDegList.AsNoTracking().AsQueryable();
                if (ay > 0)
                {
                    var startDate = DateTime.Now.Date.AddMonths(-ay);
                    q = q.Where(o => o.KayitTar >= startDate);
                }
                var list = q.ToList();

                int totalEvaluations = list.Count;
                int completed = list.Count(x => x.Durum == true);
                int pending = list.Count(x => x.Durum == null);
                int canceled = list.Count(x => x.Durum == false);

                double avgScore = list.Count(x => x.Durum == true && x.ToplamPuan != null) > 0
                    ? Math.Round(list.Where(x => x.Durum == true && x.ToplamPuan != null).Average(x => x.ToplamPuan.Value), 1) : 0;
                int criticalCount = list.Count(x => x.Durum == true && x.ToplamPuan != null && x.ToplamPuan.Value < 60);
                int buAyCount = list.Count(x => x.KayitTar.HasValue && x.KayitTar.Value.Month == DateTime.Now.Month && x.KayitTar.Value.Year == DateTime.Now.Year);

                double avgKalite = list.Count(x => x.Durum == true && x.KalitePuani != null) > 0 ? Math.Round(list.Where(x => x.Durum == true && x.KalitePuani != null).Average(x => x.KalitePuani.Value), 1) : 0;
                double avgFiyat = list.Count(x => x.Durum == true && x.FiyatPuani != null) > 0 ? Math.Round(list.Where(x => x.Durum == true && x.FiyatPuani != null).Average(x => x.FiyatPuani.Value), 1) : 0;
                double avgTermin = list.Count(x => x.Durum == true && x.TerminPuani != null) > 0 ? Math.Round(list.Where(x => x.Durum == true && x.TerminPuani != null).Average(x => x.TerminPuani.Value), 1) : 0;
                double avgBelge = list.Count(x => x.Durum == true && x.BelgePuani != null) > 0 ? Math.Round(list.Where(x => x.Durum == true && x.BelgePuani != null).Average(x => x.BelgePuani.Value), 1) : 0;

                var classDist = list.Where(x => x.Durum == true && !string.IsNullOrEmpty(x.Sinif))
                    .GroupBy(x => x.Sinif).Select(g => new { Class = g.Key, Count = g.Count() }).OrderBy(x => x.Class).ToList();

                int trendAy = ay > 0 ? ay : 6;
                var trend = Enumerable.Range(0, trendAy).Select(i => DateTime.Now.AddMonths(-i)).OrderBy(d => d).Select(m => new
                {
                    Month = m.ToString("MMM yy"),
                    Created = list.Count(x => x.KayitTar.HasValue && x.KayitTar.Value.Month == m.Month && x.KayitTar.Value.Year == m.Year),
                    Completed = list.Count(x => x.Durum == true && x.KayitTar.HasValue && x.KayitTar.Value.Month == m.Month && x.KayitTar.Value.Year == m.Year)
                }).ToList();

                var topSuppliers = list.Where(x => x.Durum == true && x.ToplamPuan != null && !string.IsNullOrEmpty(x.Unvan))
                    .GroupBy(x => x.Unvan).Select(g => new { Supplier = g.Key, Score = Math.Round(g.Average(x => x.ToplamPuan.Value), 1) })
                    .OrderByDescending(x => x.Score).Take(7).ToList();

                var byType = list.Where(x => !string.IsNullOrEmpty(x.TedTurTanim))
                    .GroupBy(x => x.TedTurTanim).Select(g => new { Type = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(7).ToList();

                var riskDist = list.Where(x => x.Durum == true && !string.IsNullOrEmpty(x.RiskDurum))
                    .GroupBy(x => x.RiskDurum).Select(g => new { Risk = g.Key, Count = g.Count() }).ToList();

                var insights = new List<object>();
                if (avgScore >= 80)
                    insights.Add(new { type = "success", icon = "ki-chart-line-up-2", text = "Tedarikçi genel puan ortalaması " + avgScore + "/100 — Tedarik zinciri kalitesi oldukça yüksek." });
                else if (totalEvaluations > 3 && avgScore < 65)
                    insights.Add(new { type = "warning", icon = "ki-chart-line-down", text = "Tedarikçi genel puan ortalaması " + avgScore + "/100 — Tedarikçi seçimi ve kalite standartları gözden geçirilmelidir." });
                if (criticalCount > 0)
                    insights.Add(new { type = "danger", icon = "ki-user-cross", text = criticalCount + " tedarikçi kritik sınır altında (<60 puan) — Alternatif tedarik kaynakları değerlendirilmeli." });
                if (pending > 5)
                    insights.Add(new { type = "warning", icon = "ki-timer", text = pending + " tedarikçi değerlendirmesi bekliyor — Kalite onay süreçlerinin hızlandırılması önerilir." });
                if (avgTermin < 75 && completed > 0)
                    insights.Add(new { type = "danger", icon = "ki-truck", text = "Ortalama teslimat puanı %" + avgTermin + " — Termin sürelerine bağlı gecikmeler üretim planlamasını riske atabilir." });
                else if (avgTermin >= 90 && completed > 0)
                    insights.Add(new { type = "success", icon = "ki-truck", text = "Ortalama teslimat puanı %" + avgTermin + " — Teslimat süreleri oldukça başarılı." });
                if (avgKalite < 75 && completed > 0)
                    insights.Add(new { type = "warning", icon = "ki-category", text = "Ortalama kalite puanı " + avgKalite + "/100 — Hammadde giriş kontrollerindeki red oranları incelenmelidir." });
                if (avgFiyat < 70 && completed > 0)
                    insights.Add(new { type = "info", icon = "ki-price-tag", text = "Ortalama fiyat puanı " + avgFiyat + "/100 — Fiyat verimliliği ve satın alma maliyetleri kontrol edilmeli." });
                if (insights.Count == 0)
                    insights.Add(new { type = "success", icon = "ki-badge", text = "Tüm tedarikçi göstergeleri normal seviyede." });

                return Ok(new
                {
                    TotalEvaluations = totalEvaluations,
                    Completed = completed,
                    Pending = pending,
                    Canceled = canceled,
                    AvgScore = avgScore,
                    CriticalCount = criticalCount,
                    BuAyCount = buAyCount,
                    AvgKalite = avgKalite,
                    AvgFiyat = avgFiyat,
                    AvgTermin = avgTermin,
                    AvgBelge = avgBelge,
                    ClassDist = classDist,
                    Trend = trend,
                    TopSuppliers = topSuppliers,
                    ByType = byType,
                    RiskDist = riskDist,
                    AiInsights = insights
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Tedarikçi dashboard verisi alinirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("detail/{belgeNo}")]
        public IActionResult TalepDetayGetir(string belgeNo)
        {
            try
            {
                var item = _context.viewTedDegList.AsNoTracking().FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (item == null) return NotFound(new { message = "Kayit bulunamadi." });

                // Check DataTam
                // DataTam is 1 if all scores are resolved and a risk classification has been made
                bool hasUnscoredParameters = _context.tb_TedDegKalitePuan.Any(o => o.BelgeNo == belgeNo && o.Puan == null);
                string dataTam = (item.BelgePuani != null && item.KalitePuani != null && item.FiyatPuani != null && item.TerminPuani != null && item.RiskDurum != null && !hasUnscoredParameters) ? "1" : "0";

                var detail = new
                {
                    DataTam = dataTam,
                    Durum = item.Durum == null ? "BEKLEMEDE" : (item.Durum == true ? "TAMAMLANDI" : "IPTAL EDILDI"),
                    BelgePuani = item.BelgePuani?.ToString() ?? "",
                    FiyatPuani = item.FiyatPuani?.ToString() ?? "",
                    KalitePuani = item.KalitePuani?.ToString() ?? "",
                    TeslimPuani = item.TerminPuani?.ToString() ?? "",
                    RiskDurum = item.RiskDurum ?? "",
                    ToplamPuan = item.ToplamPuan?.ToString() ?? "",
                    Sinif = item.Sinif ?? "",
                    item.TedarikciKodu,
                    item.MahsulYil,
                    item.Unvan,
                    item.TedTurTanim,
                    item.TurKod,
                    KayitPer = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == item.KayitSicil)?.AdSoyad ?? "",
                    KayitTarStr = item.KayitTar?.ToString("dd.MM.yyyy") ?? "",
                    item.BelgeNo,
                    IstekTarStr = item.IstekTar?.ToString("dd.MM.yyyy") ?? "",
                    GelisTarStr = item.GelisTar?.ToString("dd.MM.yyyy") ?? "",
                    item.Aciklama,
                    item.TedDegID
                };

                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kayit detayi alinirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("history/{belgeNo}")]
        public IActionResult TalepTarihce(string belgeNo)
        {
            try
            {
                var history = _context.tb_BelgeTarihce.AsNoTracking()
                    .Where(o => o.BelgeKodu == belgeNo)
                    .OrderByDescending(o => o.KayitTar)
                    .Select(o => new
                    {
                        o.BelgeTarihceID,
                        o.BelgeKodu,
                        o.Konu,
                        o.Aciklama,
                        KayitTarStr = o.KayitTar.HasValue ? o.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                    })
                    .ToList();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Tarih?e alinirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("parameters/{belgeNo}")]
        public IActionResult KalitePuanDetayGetir(string belgeNo)
        {
            try
            {
                var deg = _context.tb_TedDeg.AsNoTracking().FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (deg == null) return NotFound(new { message = "Degerlendirme kaydi bulunamadi." });

                // Join tb_TedDegTurParam, tb_TedDegParam and tb_TedDegKalitePuan on PKod (filtered by BelgeNo)
                var list = (from tp in _context.tb_TedDegTurParam.AsNoTracking()
                            join p in _context.tb_TedDegParam.AsNoTracking() on tp.PKod equals p.PKod
                            join kp in _context.tb_TedDegKalitePuan.AsNoTracking().Where(k => k.BelgeNo == belgeNo) on tp.PKod equals kp.PKod into kps
                            from kp in kps.DefaultIfEmpty()
                            where tp.TurKod == deg.TurKod
                            select new
                            {
                                tp.PKod,
                                p.Tanim,
                                tp.HesapTur,
                                HedefPuan = tp.Puan,
                                tp.TurKod,
                                Deger = kp != null ? (kp.Deger ?? "") : "",
                                HesaplananPuan = kp != null ? (kp.Puan ?? 0) : 0,
                                DegerEtiket = kp != null ? (kp.DegerEtiket ?? "") : "",
                                IslemTar = kp != null ? kp.IslemTar : null,
                                IslemYapan = kp != null ? (kp.IslemYapan ?? "") : ""
                            }).ToList();

                var formulas = _context.tb_TedDegParamFormul.AsNoTracking().Where(f => f.TurKod == deg.TurKod).ToList();

                var variable = list.Select(i => new
                {
                    deg.Durum,
                    i.Deger,
                    i.DegerEtiket,
                    i.HedefPuan,
                    HesaplananPuan = i.HesaplananPuan.ToString(),
                    i.HesapTur,
                    i.PKod,
                    i.Tanim,
                    Detay = i.HesapTur == "SECIM" ? formulas.Where(o => o.PKod == i.PKod).Select(o => new { o.Puan, o.TanimEtiket }).ToList() : null,
                    Bilgi = string.IsNullOrEmpty(i.IslemYapan) ? "" : $"{i.IslemTar?.ToString("dd.MM.yyyy HH:mm") ?? ""} - {_context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == i.IslemYapan)?.AdSoyad ?? i.IslemYapan}"
                }).ToList();

                return Ok(variable);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Kalite puan detaylari alinirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // G?NCELLEME VE I??LEM YAPMA ENDPOINTS
        // ====================================================================

        public class SaveScoresModel
        {
            public string BelgeNo { get; set; }
            public string ID { get; set; } // JSON string of parameters and values
            public string IstTar { get; set; }
            public string GerTar { get; set; }
            public string BelgeDurum { get; set; }
            public string RiskDurum { get; set; }
        }

        public class ParamDegerDto
        {
            public string PKod { get; set; }
            public string Deger { get; set; }
        }

        [HttpPost("save-scores")]
        public IActionResult TedarikciDegerKaydet([FromBody] SaveScoresModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var deg = _context.tb_TedDeg.FirstOrDefault(o => o.BelgeNo == model.BelgeNo);
                if (deg == null) return NotFound(new { message = "Kayit bulunamadi." });
                if (deg.Durum == true) return BadRequest(new { message = "Tamamlanmis bir degerlendirme ?zerinde degisiklik yapamazsiniz." });

                List<ParamDegerDto> islemList = new List<ParamDegerDto>();
                if (!string.IsNullOrEmpty(model.ID))
                {
                    try
                    {
                        islemList = JsonSerializer.Deserialize<List<ParamDegerDto>>(model.ID, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                        // Fallback parsing or logging
                    }
                }

                List<tb_BelgeTarihce> tarihceList = new List<tb_BelgeTarihce>();
                int Toplam = 0;

                var formulas = _context.tb_TedDegParamFormul.AsNoTracking().Where(o => o.TurKod == deg.TurKod).ToList();

                foreach (var item in islemList)
                {
                    // Extract PKod (txtPr_K1 -> K1)
                    string kod = item.PKod.Contains("_") ? item.PKod.Split('_')[1] : item.PKod;
                    double? deger = null;
                    if (double.TryParse(item.Deger, out double dVal))
                    {
                        deger = dVal;
                    }

                    var kp = _context.tb_TedDegKalitePuan.FirstOrDefault(o => o.BelgeNo == model.BelgeNo && o.PKod == kod);
                    if (kp != null)
                    {
                        string eskiDeger = kp.Deger ?? "";
                        if (deger.HasValue)
                        {
                            int puan = 0;
                            string etiket = "";
                            string buldum = "x";

                            var paramFormulList = formulas.Where(o => o.PKod == kod).OrderByDescending(o => o.Puan).ToList();
                            foreach (var pItem in paramFormulList)
                            {
                                if (buldum == "") break;
                                if (pItem.Formul1 != null && pItem.Formul2 != null)
                                {
                                    if (FormulDetayHesap(deger.Value, pItem.Formul1, pItem.Deger1 ?? 0) == "" &&
                                        FormulDetayHesap(deger.Value, pItem.Formul2, pItem.Deger2 ?? 0) == "")
                                    {
                                        puan = pItem.Puan ?? 0;
                                        etiket = pItem.TanimEtiket;
                                        buldum = "";
                                    }
                                }
                                else
                                {
                                    if (FormulDetayHesap(deger.Value, pItem.Formul1, pItem.Deger1 ?? 0) == "")
                                    {
                                        puan = pItem.Puan ?? 0;
                                        etiket = pItem.TanimEtiket;
                                        buldum = "";
                                    }
                                }
                            }

                            if (eskiDeger != deger.Value.ToString())
                            {
                                tarihceList.Add(new tb_BelgeTarihce
                                {
                                    Aciklama = $"Eski Deger: {eskiDeger} - Yeni Deger: {deger} (Islem Yapan: {currentUser.AdSoyad})",
                                    BelgeKodu = model.BelgeNo,
                                    KayitTar = DateTime.Now,
                                    Konu = $"{kod} Kodlu Parametre Degeri Degisti. [Mobil]"
                                });
                                kp.IslemTar = DateTime.Now;
                                kp.IslemYapan = currentUser.SicilNo;
                            }

                            kp.Deger = deger.Value.ToString();
                            kp.Puan = puan;
                            if (!string.IsNullOrEmpty(etiket))
                                kp.DegerEtiket = etiket;

                            if (string.IsNullOrEmpty(kp.IslemYapan))
                            {
                                kp.IslemTar = DateTime.Now;
                                kp.IslemYapan = currentUser.SicilNo;
                            }

                            Toplam += puan;
                        }
                        else
                        {
                            if (eskiDeger != "")
                            {
                                tarihceList.Add(new tb_BelgeTarihce
                                {
                                    Aciklama = $"Eski Deger: {eskiDeger} - Yeni Deger: [Bos] (Islem Yapan: {currentUser.AdSoyad})",
                                    BelgeKodu = model.BelgeNo,
                                    KayitTar = DateTime.Now,
                                    Konu = $"{kod} Kodlu Parametre Degeri Degisti. [Mobil]"
                                });
                            }
                            kp.Deger = null;
                            kp.Puan = null;
                            kp.IslemTar = DateTime.Now;
                            kp.IslemYapan = currentUser.SicilNo;
                        }
                    }
                }

                deg.KalitePuani = Toplam;

                // Fiyat Puani Hesaplama
                int FiyatPuan = 0;
                if (Toplam >= 20 && Toplam <= 50) FiyatPuan = 50;
                else if (Toplam >= 51 && Toplam <= 65) FiyatPuan = 70;
                else if (Toplam >= 66 && Toplam <= 80) FiyatPuan = 80;
                else if (Toplam >= 81) FiyatPuan = 100;
                deg.FiyatPuani = FiyatPuan;

                // Belge Puani Hesaplama
                string eskiBelge = deg.BelgePuani?.ToString() ?? "";
                if (eskiBelge != model.BelgeDurum)
                {
                    tarihceList.Add(new tb_BelgeTarihce
                    {
                        Aciklama = $"Eski Deger: {eskiBelge} - Yeni Deger: {model.BelgeDurum} (Islem Yapan: {currentUser.AdSoyad})",
                        BelgeKodu = model.BelgeNo,
                        KayitTar = DateTime.Now,
                        Konu = "Belge Durumu Degisti. [Mobil]"
                    });
                }

                if (model.BelgeDurum == "100") deg.BelgePuani = 100;
                else if (model.BelgeDurum == "0") deg.BelgePuani = 0;
                else deg.BelgePuani = null;

                // Termin Puani Hesaplama
                string eskiistTar = deg.IstekTar?.ToString("dd.MM.yyyy") ?? "";
                string eskigerTar = deg.GelisTar?.ToString("dd.MM.yyyy") ?? "";

                if (eskiistTar != model.IstTar || eskigerTar != model.GerTar)
                {
                    tarihceList.Add(new tb_BelgeTarihce
                    {
                        Aciklama = $"(Ist. Tes. Tar.) Eski: {eskiistTar} - Yeni: {model.IstTar} * (Ger?. Tes. Tar.) Eski: {eskigerTar} - Yeni: {model.GerTar} (Islem Yapan: {currentUser.AdSoyad})",
                        BelgeKodu = model.BelgeNo,
                        KayitTar = DateTime.Now,
                        Konu = "Istenen/Ger?eklesen Teslim Tarihi Degisti. [Mobil]"
                    });
                }

                if (!string.IsNullOrEmpty(model.IstTar))
                {
                    if (DateTime.TryParse(model.IstTar, out DateTime dtIst))
                        deg.IstekTar = dtIst;
                }
                if (!string.IsNullOrEmpty(model.GerTar))
                {
                    if (DateTime.TryParse(model.GerTar, out DateTime dtGer))
                        deg.GelisTar = dtGer;
                }

                if (deg.IstekTar.HasValue && deg.GelisTar.HasValue)
                {
                    if (deg.IstekTar > deg.GelisTar)
                    {
                        return BadRequest(new { message = "Istenilen Teslim Tarihi Ger?eklesme Tarihinden Sonra Olamaz." });
                    }

                    int GunFark = (deg.GelisTar.Value.Date - deg.IstekTar.Value.Date).Days;
                    if (GunFark <= 5) deg.TerminPuani = 100;
                    else deg.TerminPuani = 0;

                    deg.TerminGunFark = GunFark;
                }

                // Toplam Puan & Sinif Hesaplama
                deg.ToplamPuan = ((deg.KalitePuani ?? 0) * 0.75) +
                                 ((deg.FiyatPuani ?? 0) * 0.15) +
                                 ((deg.TerminPuani ?? 0) * 0.05) +
                                 ((deg.BelgePuani ?? 0) * 0.05);

                if (Toplam >= 0 && Toplam <= 49) deg.Sinif = "D";
                else if (Toplam >= 50 && Toplam <= 69) deg.Sinif = "C";
                else if (Toplam >= 70 && Toplam <= 89) deg.Sinif = "B";
                else if (Toplam >= 90) deg.Sinif = "A";

                // Risk Durumu
                string eskiRisk = deg.RiskDurum ?? "";
                if (eskiRisk != model.RiskDurum)
                {
                    tarihceList.Add(new tb_BelgeTarihce
                    {
                        Aciklama = $"Eski Deger: {eskiRisk} - Yeni Deger: {model.RiskDurum} (Islem Yapan: {currentUser.AdSoyad})",
                        BelgeKodu = model.BelgeNo,
                        KayitTar = DateTime.Now,
                        Konu = "Risk Durumu Degisti. [Mobil]"
                    });
                }
                deg.RiskDurum = string.IsNullOrEmpty(model.RiskDurum) ? null : model.RiskDurum;

                if (tarihceList.Count > 0)
                {
                    _context.tb_BelgeTarihce.AddRange(tarihceList);
                }

                _context.SaveChanges();

                return Ok(new { success = true, message = "Degerlendirme kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Degerlendirme kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        private string FormulDetayHesap(double Deger, string Formul, double FormulDeger)
        {
            string buldum = "x";
            switch (Formul)
            {
                case "=":
                    if (Deger == FormulDeger) buldum = "";
                    break;
                case ">":
                    if (Deger > FormulDeger) buldum = "";
                    break;
                case ">=":
                    if (Deger >= FormulDeger) buldum = "";
                    break;
                case "<":
                    if (Deger < FormulDeger) buldum = "";
                    break;
                case "<=":
                    if (Deger <= FormulDeger) buldum = "";
                    break;
            }
            return buldum;
        }

        [HttpPost("complete/{belgeNo}")]
        public IActionResult DegerlendirmeTamamla(string belgeNo)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var deg = _context.tb_TedDeg.FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (deg == null) return NotFound(new { message = "Kayit bulunamadi." });
                if (deg.Durum == true) return BadRequest(new { message = "Bu degerlendirme zaten tamamlanmis." });

                deg.Durum = true;
                _context.SaveChanges();

                // Save to history
                _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce
                {
                    BelgeKodu = belgeNo,
                    Konu = "Tedarik?i Degerlendirme Islemi Tamamlandi. [Mobil]",
                    Aciklama = $"Islem Yapan: {currentUser.AdSoyad}",
                    KayitTar = DateTime.Now
                });

                // Write formulas to history as static html/text snapshot
                var listParam = _context.tb_TedDegTurParam.AsNoTracking().Where(o => o.TurKod == deg.TurKod).ToList();
                var formulas = _context.tb_TedDegParamFormul.AsNoTracking().Where(o => o.TurKod == deg.TurKod).ToList();

                foreach (var itemP in listParam)
                {
                    var paramFormulas = formulas.Where(o => o.PKod == itemP.PKod).ToList();
                    string sonuc = "";
                    foreach (var item in paramFormulas)
                    {
                        string etiket = !string.IsNullOrEmpty(item.TanimEtiket) ? $"({item.TanimEtiket})" : "";
                        if (item.Formul1 == "=" && itemP.HesapTur == "SECIM")
                        {
                            sonuc += $"Girilen Deger {item.TanimEtiket} ise <b>{item.Puan} Puan </b></br>";
                        }
                        else if (item.Formul1 != null && item.Formul2 != null)
                        {
                            sonuc += $"Girilen Deger {item.Formul1} {item.Deger1} ve {item.Formul2} {item.Deger2} ise <b>{item.Puan} Puan {etiket}</b></br>";
                        }
                        else
                        {
                            sonuc += $"Girilen Deger {item.Formul1} {item.Deger1} ise <b>{item.Puan} Puan {etiket}</b></br>";
                        }
                    }

                    _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce
                    {
                        BelgeKodu = belgeNo,
                        Konu = itemP.PKod,
                        Aciklama = sonuc,
                        KayitTar = DateTime.Now
                    });
                }

                _context.SaveChanges();

                _ = _pushNotificationService.NotifyTedarikciDegerlendirmeCompletedAsync(belgeNo);

                return Ok(new { success = true, message = "Degerlendirme basariyla tamamlandi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Degerlendirme tamamlanirken hata olustu: {ex.Message}" });
            }
        }

        [HttpPost("cancel/{belgeNo}")]
        public IActionResult DegerlendirmeIptal(string belgeNo)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var deg = _context.tb_TedDeg.FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (deg == null) return NotFound(new { message = "Kayit bulunamadi." });
                if (deg.Durum == true) return BadRequest(new { message = "Tamamlanmis bir degerlendirme iptal edilemez." });
                if (deg.Durum == false) return BadRequest(new { message = "Bu degerlendirme zaten iptal edilmis." });

                deg.Durum = false;
                _context.SaveChanges();

                // Save to history
                _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce
                {
                    BelgeKodu = belgeNo,
                    Konu = "Tedarik?i Degerlendirme Islemi Iptal Edildi. [Mobil]",
                    Aciklama = $"Islem Yapan: {currentUser.AdSoyad}",
                    KayitTar = DateTime.Now
                });

                _context.SaveChanges();

                _ = _pushNotificationService.NotifyTedarikciDegerlendirmeCancelledAsync(belgeNo);

                return Ok(new { success = true, message = "Degerlendirme basariyla iptal edildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Degerlendirme iptal edilirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // YENI KAYIT OLU??TURMA VE YARDIMCI VERILER
        // ====================================================================

        public class CreateModel
        {
            public string Tedarikci { get; set; }
            public string TurKod { get; set; }
            public string IstTarih { get; set; }
            public string MahsulYil { get; set; }
            public string KayitTarih { get; set; }
            public string Aciklama { get; set; }
        }

        [HttpPost("create")]
        public IActionResult TalepKaydet([FromBody] CreateModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();

                if (string.IsNullOrEmpty(model.Tedarikci))
                    return BadRequest(new { message = "Tedarik?i se?iniz." });
                if (string.IsNullOrEmpty(model.TurKod))
                    return BadRequest(new { message = "Faaliyet alani se?iniz." });
                if (string.IsNullOrEmpty(model.IstTarih))
                    return BadRequest(new { message = "Istenen teslim tarihi se?iniz." });
                if (string.IsNullOrEmpty(model.KayitTarih))
                    return BadRequest(new { message = "Kayit tarihi se?iniz." });
                if (string.IsNullOrEmpty(model.MahsulYil))
                    return BadRequest(new { message = "Mahsul yili se?iniz." });

                var talep = new tb_TedDeg
                {
                    IstekTar = Convert.ToDateTime(model.IstTarih),
                    KayitTar = Convert.ToDateTime(model.KayitTarih),
                    MahsulYil = Convert.ToInt16(model.MahsulYil),
                    Aciklama = model.Aciklama,
                    TedarikciKodu = model.Tedarikci,
                    KayitSicil = currentUser.SicilNo,
                    Durum = null,
                    TurKod = model.TurKod
                };

                _context.tb_TedDeg.Add(talep);
                _context.SaveChanges();

                // Generate BelgeNo
                talep.BelgeNo = $"TDEGER-{DateTime.Now.Year}{DateTime.Now.Month.ToString().PadLeft(2, '0')}-{talep.TedDegID}";
                _context.SaveChanges();

                // Add parameters to tb_TedDegKalitePuan
                var pList = _context.tb_TedDegTurParam.AsNoTracking().Where(o => o.TurKod == talep.TurKod).ToList();
                foreach (var item in pList)
                {
                    _context.tb_TedDegKalitePuan.Add(new tb_TedDegKalitePuan
                    {
                        BelgeNo = talep.BelgeNo,
                        PKod = item.PKod
                    });
                }

                // Add to history
                _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce
                {
                    BelgeKodu = talep.BelgeNo,
                    Konu = "Tedarik?i Degerlendirme Kaydi Olusturuldu. [Mobil]",
                    Aciklama = $"Kayit Sahibi: {currentUser.AdSoyad}",
                    KayitTar = DateTime.Now
                });

                _context.SaveChanges();

                _ = _pushNotificationService.NotifyNewTedarikciDegerlendirmeAsync(talep.BelgeNo);

                return Ok(new { success = true, message = "Degerlendirme kaydi basariyla olusturuldu.", belgeNo = talep.BelgeNo });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Degerlendirme olusturulurken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("dropdowns")]
        public IActionResult GetDropdowns()
        {
            try
            {
                var tedarikciler = _context.tb_Tedarikci.AsNoTracking()
                    .Where(t => t.Durum == true)
                    .OrderBy(t => t.Unvan)
                    .Select(t => new { id = t.TedarikciKodu, name = t.Unvan })
                    .ToList();

                var turler = new List<object>();
                try
                {
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "SELECT TurKod, Tanim FROM tb_TedDegTur WHERE Durum = 1 OR Durum IS NULL ORDER BY Tanim DESC";
                        _context.Database.OpenConnection();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                turler.Add(new
                                {
                                    id = reader["TurKod"]?.ToString() ?? "",
                                    name = reader["Tanim"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback using distinct values from viewTedDegList
                    var list = _context.viewTedDegList.AsNoTracking()
                        .Where(x => x.TurKod != null)
                        .Select(x => new { id = x.TurKod, name = x.TedTurTanim })
                        .Distinct()
                        .ToList();
                    
                    foreach (var x in list)
                    {
                        turler.Add(new { id = x.id, name = x.name });
                    }
                }

                var currentYear = DateTime.Now.Year;
                var yillar = new List<int>();
                for (int y = currentYear; y >= 2020; y--)
                {
                    yillar.Add(y);
                }

                return Ok(new { tedarikciler, turler, yillar });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Veriler alinamadi: {ex.Message}" });
            }
        }
    }
}
