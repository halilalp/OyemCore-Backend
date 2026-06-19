using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPortalSpace.DataLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using System.Text.Json;

namespace WebPortalSpace.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TedarikciController : ControllerBase
    {
        private readonly IYbsDbContext _context;

        public TedarikciController(IYbsDbContext context)
        {
            _context = context;
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

        private tb_Kullanici GetCurrentUser()
        {
            int userId = GetCurrentUserId();
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == userId);
            if (user == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");
            return user;
        }

        // ====================================================================
        // LİSTELEME VE DETAY ENDPOINTS
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
                    Durum = i.Durum == null ? "BEKLEMEDE" : (i.Durum == true ? "TAMAMLANDI" : "İPTAL EDİLDİ"),
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
                return BadRequest(new { message = $"Tedarikçi değerlendirme listesi alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("detail/{belgeNo}")]
        public IActionResult TalepDetayGetir(string belgeNo)
        {
            try
            {
                var item = _context.viewTedDegList.AsNoTracking().FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (item == null) return NotFound(new { message = "Kayıt bulunamadı." });

                // Check DataTam
                // DataTam is 1 if all scores are resolved and a risk classification has been made
                bool hasUnscoredParameters = _context.tb_TedDegKalitePuan.Any(o => o.BelgeNo == belgeNo && o.Puan == null);
                string dataTam = (item.BelgePuani != null && item.KalitePuani != null && item.FiyatPuani != null && item.TerminPuani != null && item.RiskDurum != null && !hasUnscoredParameters) ? "1" : "0";

                var detail = new
                {
                    DataTam = dataTam,
                    Durum = item.Durum == null ? "BEKLEMEDE" : (item.Durum == true ? "TAMAMLANDI" : "İPTAL EDİLDİ"),
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
                return BadRequest(new { message = $"Kayıt detayı alınırken hata oluştu: {ex.Message}" });
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
                return BadRequest(new { message = $"Tarihçe alınırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet("parameters/{belgeNo}")]
        public IActionResult KalitePuanDetayGetir(string belgeNo)
        {
            try
            {
                var deg = _context.tb_TedDeg.AsNoTracking().FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (deg == null) return NotFound(new { message = "Değerlendirme kaydı bulunamadı." });

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
                return BadRequest(new { message = $"Kalite puan detayları alınırken hata oluştu: {ex.Message}" });
            }
        }

        // ====================================================================
        // GÜNCELLEME VE İŞLEM YAPMA ENDPOINTS
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
                if (deg == null) return NotFound(new { message = "Kayıt bulunamadı." });
                if (deg.Durum == true) return BadRequest(new { message = "Tamamlanmış bir değerlendirme üzerinde değişiklik yapamazsınız." });

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
                                    Aciklama = $"Eski Değer: {eskiDeger} - Yeni Değer: {deger} (İşlem Yapan: {currentUser.AdSoyad})",
                                    BelgeKodu = model.BelgeNo,
                                    KayitTar = DateTime.Now,
                                    Konu = $"{kod} Kodlu Parametre Değeri Değişti."
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
                                    Aciklama = $"Eski Değer: {eskiDeger} - Yeni Değer: [Boş] (İşlem Yapan: {currentUser.AdSoyad})",
                                    BelgeKodu = model.BelgeNo,
                                    KayitTar = DateTime.Now,
                                    Konu = $"{kod} Kodlu Parametre Değeri Değişti."
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

                // Fiyat Puanı Hesaplama
                int FiyatPuan = 0;
                if (Toplam >= 20 && Toplam <= 50) FiyatPuan = 50;
                else if (Toplam >= 51 && Toplam <= 65) FiyatPuan = 70;
                else if (Toplam >= 66 && Toplam <= 80) FiyatPuan = 80;
                else if (Toplam >= 81) FiyatPuan = 100;
                deg.FiyatPuani = FiyatPuan;

                // Belge Puanı Hesaplama
                string eskiBelge = deg.BelgePuani?.ToString() ?? "";
                if (eskiBelge != model.BelgeDurum)
                {
                    tarihceList.Add(new tb_BelgeTarihce
                    {
                        Aciklama = $"Eski Değer: {eskiBelge} - Yeni Değer: {model.BelgeDurum} (İşlem Yapan: {currentUser.AdSoyad})",
                        BelgeKodu = model.BelgeNo,
                        KayitTar = DateTime.Now,
                        Konu = "Belge Durumu Değişti."
                    });
                }

                if (model.BelgeDurum == "100") deg.BelgePuani = 100;
                else if (model.BelgeDurum == "0") deg.BelgePuani = 0;
                else deg.BelgePuani = null;

                // Termin Puanı Hesaplama
                string eskiistTar = deg.IstekTar?.ToString("dd.MM.yyyy") ?? "";
                string eskigerTar = deg.GelisTar?.ToString("dd.MM.yyyy") ?? "";

                if (eskiistTar != model.IstTar || eskigerTar != model.GerTar)
                {
                    tarihceList.Add(new tb_BelgeTarihce
                    {
                        Aciklama = $"(İst. Tes. Tar.) Eski: {eskiistTar} - Yeni: {model.IstTar} * (Gerç. Tes. Tar.) Eski: {eskigerTar} - Yeni: {model.GerTar} (İşlem Yapan: {currentUser.AdSoyad})",
                        BelgeKodu = model.BelgeNo,
                        KayitTar = DateTime.Now,
                        Konu = "İstenen/Gerçekleşen Teslim Tarihi Değişti."
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
                        return BadRequest(new { message = "İstenilen Teslim Tarihi Gerçekleşme Tarihinden Sonra Olamaz." });
                    }

                    int GunFark = (deg.GelisTar.Value.Date - deg.IstekTar.Value.Date).Days;
                    if (GunFark <= 5) deg.TerminPuani = 100;
                    else deg.TerminPuani = 0;

                    deg.TerminGunFark = GunFark;
                }

                // Toplam Puan & Sınıf Hesaplama
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
                        Aciklama = $"Eski Değer: {eskiRisk} - Yeni Değer: {model.RiskDurum} (İşlem Yapan: {currentUser.AdSoyad})",
                        BelgeKodu = model.BelgeNo,
                        KayitTar = DateTime.Now,
                        Konu = "Risk Durumu Değişti."
                    });
                }
                deg.RiskDurum = string.IsNullOrEmpty(model.RiskDurum) ? null : model.RiskDurum;

                if (tarihceList.Count > 0)
                {
                    _context.tb_BelgeTarihce.AddRange(tarihceList);
                }

                _context.SaveChanges();

                return Ok(new { success = true, message = "Değerlendirme kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Değerlendirme kaydedilirken hata oluştu: {ex.Message}" });
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
                if (deg == null) return NotFound(new { message = "Kayıt bulunamadı." });
                if (deg.Durum == true) return BadRequest(new { message = "Bu değerlendirme zaten tamamlanmış." });

                deg.Durum = true;
                _context.SaveChanges();

                // Save to history
                _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce
                {
                    BelgeKodu = belgeNo,
                    Konu = "Tedarikçi Değerlendirme İşlemi Tamamlandı.",
                    Aciklama = $"İşlem Yapan: {currentUser.AdSoyad}",
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
                            sonuc += $"Girilen Değer {item.TanimEtiket} ise <b>{item.Puan} Puan </b></br>";
                        }
                        else if (item.Formul1 != null && item.Formul2 != null)
                        {
                            sonuc += $"Girilen Değer {item.Formul1} {item.Deger1} ve {item.Formul2} {item.Deger2} ise <b>{item.Puan} Puan {etiket}</b></br>";
                        }
                        else
                        {
                            sonuc += $"Girilen Değer {item.Formul1} {item.Deger1} ise <b>{item.Puan} Puan {etiket}</b></br>";
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

                return Ok(new { success = true, message = "Değerlendirme başarıyla tamamlandı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Değerlendirme tamamlanırken hata oluştu: {ex.Message}" });
            }
        }

        [HttpPost("cancel/{belgeNo}")]
        public IActionResult DegerlendirmeIptal(string belgeNo)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var deg = _context.tb_TedDeg.FirstOrDefault(o => o.BelgeNo == belgeNo);
                if (deg == null) return NotFound(new { message = "Kayıt bulunamadı." });
                if (deg.Durum == true) return BadRequest(new { message = "Tamamlanmış bir değerlendirme iptal edilemez." });
                if (deg.Durum == false) return BadRequest(new { message = "Bu değerlendirme zaten iptal edilmiş." });

                deg.Durum = false;
                _context.SaveChanges();

                // Save to history
                _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce
                {
                    BelgeKodu = belgeNo,
                    Konu = "Tedarikçi Değerlendirme İşlemi İptal Edildi.",
                    Aciklama = $"İşlem Yapan: {currentUser.AdSoyad}",
                    KayitTar = DateTime.Now
                });

                _context.SaveChanges();

                return Ok(new { success = true, message = "Değerlendirme başarıyla iptal edildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Değerlendirme iptal edilirken hata oluştu: {ex.Message}" });
            }
        }

        // ====================================================================
        // YENİ KAYIT OLUŞTURMA VE YARDIMCI VERİLER
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
                    return BadRequest(new { message = "Tedarikçi seçiniz." });
                if (string.IsNullOrEmpty(model.TurKod))
                    return BadRequest(new { message = "Faaliyet alanı seçiniz." });
                if (string.IsNullOrEmpty(model.IstTarih))
                    return BadRequest(new { message = "İstenen teslim tarihi seçiniz." });
                if (string.IsNullOrEmpty(model.KayitTarih))
                    return BadRequest(new { message = "Kayıt tarihi seçiniz." });
                if (string.IsNullOrEmpty(model.MahsulYil))
                    return BadRequest(new { message = "Mahsul yılı seçiniz." });

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
                    Konu = "Tedarikçi Değerlendirme Kaydı Oluşturuldu.",
                    Aciklama = $"Kayıt Sahibi: {currentUser.AdSoyad}",
                    KayitTar = DateTime.Now
                });

                _context.SaveChanges();

                return Ok(new { success = true, message = "Değerlendirme kaydı başarıyla oluşturuldu.", belgeNo = talep.BelgeNo });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Değerlendirme oluşturulurken hata oluştu: {ex.Message}" });
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
                return BadRequest(new { message = $"Veriler alınamadı: {ex.Message}" });
            }
        }
    }
}
