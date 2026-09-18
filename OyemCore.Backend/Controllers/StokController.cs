using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;
using OyemCore.Backend.Hubs;
using System.Threading.Tasks;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StokController : ControllerBase
    {
        private readonly IYbsDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public StokController(IYbsDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Kullanici kimligi dogrulanamadi.");
        }

        private string GetAdminBelgeTur() => User.FindFirst("AdminBelgeTur")?.Value ?? "";
        private bool HasStokAdmin() => OyemCore.BusinessLayer.Common.AdminBelgeTuruHelper.HasYetki(GetAdminBelgeTur(), "STOKADMIN");

        private string GetSicilNo()
        {
            return _context.tb_Kullanici.AsNoTracking()
                .Where(u => u.KullaniciID == GetCurrentUserId())
                .Select(u => u.SicilNo).FirstOrDefault() ?? "";
        }

        // Web GetAuthorizedWarehouses karsiligi. Depo erisimi HERKES icin (STOKADMIN dahil)
        // yalnizca tb_DepoSorumlusu'ndaki atamalardan gelir; admin bypass'i YOKTUR (referansla ayni).
        // STOKADMIN/MALZEMEADMIN belgesi ayri admin islemlerini yetkilendirir, depo gorunurlugunu degil.
        private List<string> GetAuthorizedWarehouses()
        {
            var sicil = GetSicilNo();
            if (string.IsNullOrEmpty(sicil)) return new List<string>();

            return _context.tb_DepoSorumlusu.AsNoTracking()
                .Where(o => o.SorumluSicilNo == sicil)
                .Select(o => o.DepoKodu).ToList();
        }

        // ====================================================================
        // STOK DURUM RAPORU
        // ====================================================================

        [HttpGet("durum")]
        public IActionResult GetDurum(
            [FromQuery] string depoKodu = "",
            [FromQuery] string arama = "",
            [FromQuery] int page = 1,
            [FromQuery] int count = 15)
        {
            try
            {
                var auth = GetAuthorizedWarehouses();
                if (auth.Count == 0) return Ok(new { totalCount = 0, data = new List<object>() });

                var query = from dm in _context.tb_DepoMalzeme.AsNoTracking()
                            join m in _context.tb_Malzeme.AsNoTracking() on dm.MalzemeKodu equals m.MalzemeKodu
                            join d in _context.tb_Depo.AsNoTracking() on dm.DepoKodu equals d.DepoKodu
                            where dm.Miktar != 0 && auth.Contains(dm.DepoKodu)
                            select new
                            {
                                dm.DepoKodu,
                                DepoAdi = d.DepoAdi,
                                dm.MalzemeKodu,
                                m.MalzemeAdi,
                                m.BirimKodu,
                                dm.Miktar,
                                dm.SonIslemTarihi
                            };

                if (!string.IsNullOrEmpty(depoKodu))
                    query = query.Where(o => o.DepoKodu == depoKodu);
                if (!string.IsNullOrEmpty(arama))
                    query = query.Where(o => o.MalzemeAdi.Contains(arama) || o.MalzemeKodu.Contains(arama));

                int totalCount = query.Count();
                if (page < 1) page = 1;
                var data = query.OrderBy(o => o.DepoKodu).ThenBy(o => o.MalzemeAdi)
                    .Skip((page - 1) * count).Take(count).ToList();

                return Ok(new { totalCount, data });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====================================================================
        // STOK HAREKETLERI
        // ====================================================================

        [HttpGet("hareketler")]
        public IActionResult GetHareketler(
            [FromQuery] string f_malzemekodu = "",
            [FromQuery] string f_depokodu = "",
            [FromQuery] string f_tip = "",
            [FromQuery] string f_status = "",
            [FromQuery] int page = 1,
            [FromQuery] int count = 15)
        {
            try
            {
                var auth = GetAuthorizedWarehouses();
                if (auth.Count == 0) return Ok(new { totalCount = 0, data = new List<object>() });

                var query = from h in _context.tb_MalzemeHareket.AsNoTracking()
                            join m in _context.tb_Malzeme.AsNoTracking() on h.MalzemeKodu equals m.MalzemeKodu into mJ
                            from m in mJ.DefaultIfEmpty()
                            join d in _context.tb_Depo.AsNoTracking() on h.DepoKodu equals d.DepoKodu into dJ
                            from d in dJ.DefaultIfEmpty()
                            join mt in _context.tb_MalzemeHareketTip.AsNoTracking() on h.IslemTipi equals mt.HareketTipKodu into mtJ
                            from mt in mtJ.DefaultIfEmpty()
                            where auth.Contains(h.DepoKodu)
                            select new
                            {
                                h.ID,
                                h.HareketNo,
                                h.DepoKodu,
                                DepoAdi = d != null ? d.DepoAdi : h.DepoKodu,
                                h.MalzemeKodu,
                                MalzemeAdi = m != null ? m.MalzemeAdi : "",
                                Birim = m != null ? m.BirimKodu : "",
                                h.Miktar,
                                h.Aciklama,
                                Tarih = h.IslemTarihi,
                                IslemTip = h.IslemTipi,
                                IslemTipAdi = mt != null ? mt.HareketTipAdi : h.IslemTipi,
                                GirisMi = mt != null && mt.GirisMi,
                                CikisMi = mt != null && mt.CikisMi,
                                h.LotNo,
                                h.OnayDurumu,
                                h.TedarikciKodu,
                            };

                if (!string.IsNullOrEmpty(f_malzemekodu))
                    query = query.Where(o => o.MalzemeKodu.Contains(f_malzemekodu) || o.MalzemeAdi.Contains(f_malzemekodu));
                if (!string.IsNullOrEmpty(f_depokodu))
                    query = query.Where(o => o.DepoKodu == f_depokodu);
                if (!string.IsNullOrEmpty(f_tip))
                    query = query.Where(o => o.IslemTip == f_tip);
                if (!string.IsNullOrEmpty(f_status))
                    query = query.Where(o => o.OnayDurumu == f_status);

                int totalCount = query.Count();
                if (page < 1) page = 1;
                var data = query.OrderByDescending(o => o.ID).Skip((page - 1) * count).Take(count).ToList();

                return Ok(new { totalCount, data });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpGet("hareket-tipleri")]
        public IActionResult GetHareketTipleri()
        {
            try
            {
                var list = _context.tb_MalzemeHareketTip.AsNoTracking()
                    .Where(o => o.Aktif)
                    .OrderBy(o => o.HareketTipAdi)
                    .Select(o => new { Kodu = o.HareketTipKodu, Adi = o.HareketTipAdi, o.GirisMi, o.CikisMi })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====================================================================
        // STOK FISLERI (liste + detay + basit kayit)
        // ====================================================================

        [HttpGet("fisler")]
        public IActionResult GetFisler(
            [FromQuery] string depoKodu = "",
            [FromQuery] string tip = "",
            [FromQuery] string arama = "",
            [FromQuery] int PageIndex = 1,
            [FromQuery] int PageSize = 15)
        {
            try
            {
                var auth = GetAuthorizedWarehouses();
                if (auth.Count == 0) return Ok(new { totalCount = 0, data = new List<object>() });

                var query = from f in _context.tb_MalzemeFis.AsNoTracking()
                            join d in _context.tb_Depo.AsNoTracking() on f.DepoKodu equals d.DepoKodu into dJ
                            from d in dJ.DefaultIfEmpty()
                            join t in _context.tb_MalzemeHareketTip.AsNoTracking() on f.HareketTipKodu equals t.HareketTipKodu into tJ
                            from t in tJ.DefaultIfEmpty()
                            where auth.Contains(f.DepoKodu) || (f.HedefDepoKodu != null && auth.Contains(f.HedefDepoKodu))
                            select new
                            {
                                f.FisNo,
                                Tarih = f.FisTarihi,
                                f.DepoKodu,
                                f.HedefDepoKodu,
                                DepoAdi = d != null ? d.DepoAdi : f.DepoKodu,
                                IslemTipi = f.HareketTipKodu,
                                IslemTipiAdi = t != null ? t.HareketTipAdi : f.HareketTipKodu,
                                f.Aciklama,
                                f.OnayDurumu,
                                KalemSayisi = _context.tb_MalzemeHareket.Count(h => h.FisNo == f.FisNo)
                            };

                if (!string.IsNullOrEmpty(depoKodu))
                    query = query.Where(o => o.DepoKodu == depoKodu);
                if (!string.IsNullOrEmpty(tip))
                    query = query.Where(o => o.IslemTipi.Contains(tip));
                if (!string.IsNullOrEmpty(arama))
                    query = query.Where(o => o.FisNo.Contains(arama) || o.Aciklama.Contains(arama) || o.DepoAdi.Contains(arama));

                int totalCount = query.Count();
                if (PageIndex < 1) PageIndex = 1;
                var data = query.OrderByDescending(o => o.FisNo)
                    .Skip((PageIndex - 1) * PageSize).Take(PageSize).ToList();

                return Ok(new { totalCount, data });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpGet("fis/{fisNo}")]
        public IActionResult GetFis(string fisNo)
        {
            try
            {
                var fis = _context.tb_MalzemeFis.AsNoTracking().FirstOrDefault(o => o.FisNo == fisNo);
                if (fis == null) return NotFound(new { message = "Fis bulunamadi." });

                var auth = GetAuthorizedWarehouses();
                if (!auth.Contains(fis.DepoKodu) && !(fis.HedefDepoKodu != null && auth.Contains(fis.HedefDepoKodu)))
                    return StatusCode(403, new { message = "Bu fisin deposunda yetkiniz yok." });

                var kalemler = (from h in _context.tb_MalzemeHareket.AsNoTracking()
                                join m in _context.tb_Malzeme.AsNoTracking() on h.MalzemeKodu equals m.MalzemeKodu into mJ
                                from m in mJ.DefaultIfEmpty()
                                where h.FisNo == fisNo && h.DepoKodu == fis.DepoKodu
                                select new
                                {
                                    h.MalzemeKodu,
                                    MalzemeAdi = m != null ? m.MalzemeAdi : "",
                                    h.Miktar,
                                    Birim = m != null ? (m.BirimKodu ?? "ADET") : "ADET",
                                    Aciklama = h.Aciklama ?? "",
                                    LotNo = h.LotNo ?? ""
                                }).ToList();

                var tipAdi = _context.tb_MalzemeHareketTip.AsNoTracking()
                    .Where(t => t.HareketTipKodu == fis.HareketTipKodu).Select(t => t.HareketTipAdi).FirstOrDefault();

                return Ok(new
                {
                    fis.FisNo,
                    IslemTipi = fis.HareketTipKodu,
                    IslemTipiAdi = tipAdi ?? fis.HareketTipKodu,
                    fis.DepoKodu,
                    HedefDepoKodu = fis.HedefDepoKodu ?? "",
                    Aciklama = fis.Aciklama ?? "",
                    BelgeNo = fis.BelgeNo ?? "",
                    Tarih = fis.FisTarihi,
                    fis.OnayDurumu,
                    fis.KayitSicil,
                    Kalemler = kalemler
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class FisKalemDto
        {
            public string MalzemeKodu { get; set; }
            public decimal Miktar { get; set; }
            public string Aciklama { get; set; }
            public string LotNo { get; set; }   // lot takipli malzemeler icin (giris: atanacak, cikis: secilen lot)
        }

        public class FisSaveRequest
        {
            public string Tip { get; set; }        // hareket tip kodu (tb_MalzemeHareketTip)
            public string DepoKodu { get; set; }
            public string HedefDepoKodu { get; set; }  // transfer fisleri icin
            public string Aciklama { get; set; }
            public string BelgeNo { get; set; }
            public string CariKodu { get; set; }
            public List<FisKalemDto> Kalemler { get; set; }
        }

        // Genel lot takibi acik mi? (MaterialSettings.LotTakibi.Visible)
        private bool IsGlobalLotActive()
        {
            try
            {
                var setting = _context.tb_SistemAyarlari.AsNoTracking().FirstOrDefault(o => o.AyarKey == "MaterialSettings");
                if (setting == null || string.IsNullOrEmpty(setting.AyarValue)) return false;
                using var doc = JsonDocument.Parse(setting.AyarValue);
                foreach (var name in new[] { "LotTakibi", "lotTakibi" })
                {
                    if (doc.RootElement.TryGetProperty(name, out var lt))
                    {
                        foreach (var vname in new[] { "Visible", "visible" })
                            if (lt.TryGetProperty(vname, out var v) && (v.ValueKind == JsonValueKind.True)) return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private bool IsGirisTip(string islemTipi)
        {
            if (islemTipi == "GIRIS" || islemTipi == "TERS_GIRIS" || islemTipi == "MALZEME_TRANSFER_G") return true;
            return _context.tb_MalzemeHareketTip.Any(t => t.HareketTipKodu == islemTipi && t.GirisMi);
        }
        private bool IsCikisTip(string islemTipi)
        {
            if (islemTipi == "CIKIS" || islemTipi == "TERS_CIKIS" || islemTipi == "MALZEME_TRANSFER_C") return true;
            return _context.tb_MalzemeHareketTip.Any(t => t.HareketTipKodu == islemTipi && t.CikisMi);
        }

        // Belirli bir lot'un depodaki kalan bakiyesi (giris - cikis).
        private decimal LotBakiye(string depoKodu, string malzemeKodu, string lotNo)
        {
            var hareketler = _context.tb_MalzemeHareket.AsNoTracking()
                .Where(x => x.MalzemeKodu == malzemeKodu && x.DepoKodu == depoKodu && x.LotNo == lotNo)
                .Select(x => new { x.IslemTipi, x.Miktar, x.OnayDurumu }).ToList();
            decimal giris = hareketler.Where(x => x.OnayDurumu == "ONAYLI" && IsGirisTip(x.IslemTipi)).Sum(x => x.Miktar);
            decimal cikis = hareketler.Where(x => IsCikisTip(x.IslemTipi)).Sum(x => x.Miktar);
            return giris - cikis;
        }

        // FIFO cikis dagitimi: en eski onayli girislerden baslayarak lot bazli tahsis.
        // FIFO cikis dagitimi. Onayli giris hareketlerini (yalnizca bu depoda) kronolojik
        // sirada gezer; her giris hareketinden, ait oldugu lotun kalan bakiyesi ve bu
        // hareketin miktari kadar tahsis yapar. Ayni lot birden fazla giris hareketiyle
        // gelmisse ( or. Lot-1: 1 + 63), her hareket ayri tahsis olur -> her tahsis icin
        // ayri stok hareketi (transfer'de ayrica hedef giris) uretilir.
        private (bool ok, string error) AllocateExitFIFO(string depoKodu, string malzemeKodu, decimal totalMiktar, List<(string lot, decimal qty)> allocations)
        {
            var entries = _context.tb_MalzemeHareket.AsNoTracking()
                .Where(h => h.MalzemeKodu == malzemeKodu && h.DepoKodu == depoKodu && h.OnayDurumu == "ONAYLI")
                .OrderBy(h => h.IslemTarihi).ThenBy(h => h.ID)
                .Select(h => new { h.LotNo, h.Miktar, h.IslemTipi }).ToList()
                .Where(h => IsGirisTip(h.IslemTipi) && !string.IsNullOrEmpty(h.LotNo) && h.LotNo != "-").ToList();

            // Her lotun (bu depodaki) kalan bakiyesi = giris - cikis (mevcut, onayli).
            var lotBalances = new Dictionary<string, decimal>();
            foreach (var lot in entries.Select(e => e.LotNo).Distinct())
                lotBalances[lot] = LotBakiye(depoKodu, malzemeKodu, lot);

            var allocatedPerLot = new Dictionary<string, decimal>();
            decimal remaining = totalMiktar;
            foreach (var entry in entries)
            {
                if (remaining <= 0) break;
                allocatedPerLot.TryGetValue(entry.LotNo, out var already);
                // Bu lotun bu batch'te henuz ayrilmamis kalan bakiyesi.
                decimal lotAvail = (lotBalances.TryGetValue(entry.LotNo, out var bal) ? bal : 0) - already;
                if (lotAvail <= 0) continue;
                // Tahsis = bu giris hareketinin miktari, lotun kalani ve ihtiyacin en kucugu.
                decimal take = Math.Min(Math.Min(entry.Miktar, lotAvail), remaining);
                if (take > 0)
                {
                    allocations.Add((entry.LotNo, take));
                    allocatedPerLot[entry.LotNo] = already + take;
                    remaining -= take;
                }
            }
            if (remaining > 0)
                return (false, $"Depoda yeterli bakiye yok. Urun: {malzemeKodu}, eksik: {remaining}");
            return (true, null);
        }

        // Otomatik envanter/lot no uretimi: [YIL][TIP].[4 hane]
        private string GenerateEnvanterNo(string malzemeKodu, Dictionary<string, int> localCounters)
        {
            var tipKodu = (_context.tb_Malzeme.AsNoTracking()
                .Where(m => m.MalzemeKodu == malzemeKodu).Select(m => m.MalzemeTipKodu).FirstOrDefault() ?? "GENEL").Trim();
            string prefix = DateTime.Now.Year.ToString() + tipKodu + ".";
            int nextSeq = 1;
            if (localCounters.ContainsKey(prefix))
            {
                nextSeq = localCounters[prefix] + 1;
            }
            else
            {
                var last = _context.tb_MalzemeHareket.AsNoTracking()
                    .Where(o => o.LotNo != null && o.LotNo.StartsWith(prefix))
                    .OrderByDescending(o => o.LotNo).Select(o => o.LotNo).FirstOrDefault();
                if (last != null && last.Length > prefix.Length && int.TryParse(last.Substring(prefix.Length), out int ls))
                    nextSeq = ls + 1;
            }
            localCounters[prefix] = nextSeq;
            return prefix + nextSeq.ToString("D4");
        }

        // Tam parite fis kaydi: giris/cikis, transfer (cift depo), lot takibi (FIFO cikis / lot olusturma).
        [HttpPost("fis")]
        public async Task<IActionResult> SaveFis([FromBody] FisSaveRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Tip) || string.IsNullOrWhiteSpace(req.DepoKodu))
                return BadRequest(new { message = "Hareket tipi ve depo zorunludur." });
            if (req.Kalemler == null || req.Kalemler.Count == 0)
                return BadRequest(new { message = "Fis kalemleri bos olamaz." });

            var hareketTipi = _context.tb_MalzemeHareketTip.FirstOrDefault(o => o.HareketTipKodu == req.Tip);
            if (hareketTipi == null) return BadRequest(new { message = "Gecersiz hareket tipi." });

            bool isTransfer = req.Tip.Contains("TRANSFER");
            if (isTransfer && string.IsNullOrWhiteSpace(req.HedefDepoKodu))
                return BadRequest(new { message = "Transfer icin hedef depo zorunludur." });
            if (isTransfer && req.HedefDepoKodu == req.DepoKodu)
                return BadRequest(new { message = "Kaynak ve hedef depo ayni olamaz." });

            var auth = GetAuthorizedWarehouses();
            if (!auth.Contains(req.DepoKodu))
                return StatusCode(403, new { message = "Kaynak depoda islem yetkiniz yok." });
            if (isTransfer && !auth.Contains(req.HedefDepoKodu))
                return StatusCode(403, new { message = "Hedef depoda islem yetkiniz yok." });

            bool isExit = isTransfer || hareketTipi.CikisMi;
            bool isGlobalLot = IsGlobalLotActive();
            string sicil = GetSicilNo();
            var localCounters = new Dictionary<string, int>();

            try
            {
                var fisNo = "STK" + DateTime.Now.ToString("yyyyMMddHHmmss");
                var now = DateTime.Now;
                var fis = new tb_MalzemeFis
                {
                    FisNo = fisNo,
                    HareketTipKodu = req.Tip,
                    CariKodu = req.CariKodu,
                    BelgeNo = req.BelgeNo,
                    DepoKodu = req.DepoKodu,
                    HedefDepoKodu = isTransfer ? req.HedefDepoKodu : null,
                    FisTarihi = now,
                    Aciklama = req.Aciklama,
                    OnayDurumu = "ONAYLI",
                    KayitSicil = sicil,
                    KayitTar = now
                };
                _context.tb_MalzemeFis.Add(fis);

                tb_MalzemeHareket MakeHareket(string depo, string mlz, decimal miktar, string islemTipi, string lot, string aciklama) => new tb_MalzemeHareket
                {
                    FisNo = fisNo,
                    HareketNo = Guid.NewGuid().ToString().Substring(0, 8),
                    MalzemeKodu = mlz,
                    DepoKodu = depo,
                    Miktar = miktar,
                    IslemTarihi = now,
                    IslemYapan = sicil,
                    IslemTipi = islemTipi,
                    BirimMaliyet = 0,
                    ParaBirimi = "TL",
                    Aciklama = aciklama,
                    TedarikciKodu = string.IsNullOrEmpty(req.CariKodu) ? null : req.CariKodu,
                    GirisTarihi = now,
                    LotNo = lot,
                    OnayDurumu = "ONAYLI"
                };

                tb_DepoMalzeme GetStok(string depo, string mlz)
                {
                    var s = _context.tb_DepoMalzeme.FirstOrDefault(o => o.DepoKodu == depo && o.MalzemeKodu == mlz);
                    if (s == null)
                    {
                        s = new tb_DepoMalzeme { DepoKodu = depo, MalzemeKodu = mlz, Miktar = 0, SonIslemTarihi = now };
                        _context.tb_DepoMalzeme.Add(s);
                    }
                    return s;
                }

                foreach (var k in req.Kalemler)
                {
                    if (string.IsNullOrWhiteSpace(k.MalzemeKodu) || k.Miktar <= 0) continue;

                    var malzeme = _context.tb_Malzeme.AsNoTracking().FirstOrDefault(m => m.MalzemeKodu == k.MalzemeKodu);
                    bool lotActive = isGlobalLot && malzeme != null && malzeme.LotTakibi == true;
                    string lineIslemTipi = isTransfer ? "MALZEME_TRANSFER_C" : req.Tip;
                    var stokK = GetStok(req.DepoKodu, k.MalzemeKodu);
                    var stokH = isTransfer ? GetStok(req.HedefDepoKodu, k.MalzemeKodu) : null;

                    if (isExit)
                    {
                        if (lotActive)
                        {
                            if (string.IsNullOrWhiteSpace(k.LotNo) || k.LotNo == "-")
                                return BadRequest(new { message = $"Urun {k.MalzemeKodu} icin lot secimi zorunlu." });
                            decimal available = LotBakiye(req.DepoKodu, k.MalzemeKodu, k.LotNo);
                            if (k.Miktar > available)
                                return BadRequest(new { message = $"Lot {k.LotNo} bakiyesi yetersiz. Mevcut: {available}" });

                            _context.tb_MalzemeHareket.Add(MakeHareket(req.DepoKodu, k.MalzemeKodu, k.Miktar, lineIslemTipi, k.LotNo, k.Aciklama));
                            stokK.Miktar -= k.Miktar; stokK.SonIslemTarihi = now;
                            if (isTransfer)
                            {
                                _context.tb_MalzemeHareket.Add(MakeHareket(req.HedefDepoKodu, k.MalzemeKodu, k.Miktar, "MALZEME_TRANSFER_G", k.LotNo, $"Transfer Girisi (Kaynak: {req.DepoKodu})"));
                                stokH.Miktar += k.Miktar; stokH.SonIslemTarihi = now;
                            }
                        }
                        else
                        {
                            var allocations = new List<(string lot, decimal qty)>();
                            var (ok, err) = AllocateExitFIFO(req.DepoKodu, k.MalzemeKodu, k.Miktar, allocations);
                            if (!ok) return BadRequest(new { message = err });
                            if (stokK.Miktar < k.Miktar)
                                return BadRequest(new { message = $"Kaynak depoda yeterli bakiye yok. Urun: {k.MalzemeKodu}, mevcut: {stokK.Miktar}" });

                            stokK.Miktar -= k.Miktar; stokK.SonIslemTarihi = now;
                            foreach (var alloc in allocations)
                            {
                                _context.tb_MalzemeHareket.Add(MakeHareket(req.DepoKodu, k.MalzemeKodu, alloc.qty, lineIslemTipi, alloc.lot, (k.Aciklama ?? "") + " (FIFO)"));
                                if (isTransfer)
                                {
                                    _context.tb_MalzemeHareket.Add(MakeHareket(req.HedefDepoKodu, k.MalzemeKodu, alloc.qty, "MALZEME_TRANSFER_G", alloc.lot, $"Transfer Girisi (Kaynak: {req.DepoKodu}) (FIFO)"));
                                    stokH.Miktar += alloc.qty; stokH.SonIslemTarihi = now;
                                }
                            }
                        }
                    }
                    else
                    {
                        // GIRIS: lot atama (verilen ya da otomatik) + lot kaydi
                        string assignedLot;
                        if (lotActive && !string.IsNullOrWhiteSpace(k.LotNo) && k.LotNo != "-")
                        {
                            assignedLot = k.LotNo;
                            if (!_context.tb_MalzemeLot.Any(x => x.LotNo == assignedLot && x.MalzemeKodu == k.MalzemeKodu))
                                _context.tb_MalzemeLot.Add(new tb_MalzemeLot { MalzemeKodu = k.MalzemeKodu, LotNo = assignedLot, KayitTarihi = now, KayitSicilNo = sicil, OnayDurumu = "ONAYLI" });
                        }
                        else
                        {
                            assignedLot = GenerateEnvanterNo(k.MalzemeKodu, localCounters);
                            _context.tb_MalzemeLot.Add(new tb_MalzemeLot { MalzemeKodu = k.MalzemeKodu, LotNo = assignedLot, KayitTarihi = now, KayitSicilNo = sicil, OnayDurumu = "ONAYLI" });
                        }

                        _context.tb_MalzemeHareket.Add(MakeHareket(req.DepoKodu, k.MalzemeKodu, k.Miktar, req.Tip, assignedLot, k.Aciklama));
                        stokK.Miktar += k.Miktar; stokK.SonIslemTarihi = now;
                    }
                }

                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("stokChanged");
                return Ok(new { success = true, message = "Fis kaydedildi.", fisNo });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // Bir malzeme+depo icin kalan bakiyesi olan lot'lar (cikis/lot-takipli fis formunda secim).
        [HttpGet("lot-ara")]
        public IActionResult LotAra([FromQuery] string malzemeKodu, [FromQuery] string depoKodu = "", [FromQuery] string q = "")
        {
            try
            {
                var query = from l in _context.tb_MalzemeLot.AsNoTracking()
                            join m in _context.tb_Malzeme.AsNoTracking() on l.MalzemeKodu equals m.MalzemeKodu into mJ
                            from m in mJ.DefaultIfEmpty()
                            select new { l.LotNo, l.MalzemeKodu, MalzemeAdi = m != null ? m.MalzemeAdi : "" };

                if (!string.IsNullOrEmpty(malzemeKodu))
                    query = query.Where(o => o.MalzemeKodu == malzemeKodu);
                if (!string.IsNullOrEmpty(q))
                    query = query.Where(o => o.LotNo.Contains(q) || o.MalzemeAdi.Contains(q));

                var list = query.Take(100).ToList();
                var result = new List<object>();
                foreach (var item in list)
                {
                    decimal kalan = string.IsNullOrEmpty(depoKodu)
                        ? 0
                        : LotBakiye(depoKodu, item.MalzemeKodu, item.LotNo);
                    if (!string.IsNullOrEmpty(depoKodu) && kalan <= 0) continue;
                    result.Add(new { id = item.LotNo, lotNo = item.LotNo, malzemeKodu = item.MalzemeKodu, malzemeAdi = item.MalzemeAdi, kalan });
                }
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====================================================================
        // STOK DASHBOARD (ozet istatistikler)
        // ====================================================================

        [HttpGet("dashboard")]
        public IActionResult GetDashboard()
        {
            try
            {
                var auth = GetAuthorizedWarehouses();
                var depoMalzeme = _context.tb_DepoMalzeme.AsNoTracking()
                    .Where(o => auth.Contains(o.DepoKodu)).ToList();

                double toplamMiktar = depoMalzeme.Sum(x => (double)x.Miktar);
                int aktifUrunSayisi = depoMalzeme.Where(x => x.Miktar != 0).Select(x => x.MalzemeKodu).Distinct().Count();
                int kritikStokSayisi = depoMalzeme.Count(x => x.Miktar != 0 && x.Miktar <= 10);
                int toplamDepoSayisi = auth.Count;

                var birAyOnce = DateTime.Now.AddMonths(-1);
                var fisler = _context.tb_MalzemeFis.AsNoTracking()
                    .Where(f => f.FisTarihi >= birAyOnce && (auth.Contains(f.DepoKodu) || (f.HedefDepoKodu != null && auth.Contains(f.HedefDepoKodu))))
                    .Select(f => f.HareketTipKodu).ToList();

                int girisAdet = fisler.Count(t => t != null && (t.Contains("GIRIS") || t.Contains("GİRİŞ") || t.EndsWith("_G") || t.Contains("ALIM")));
                int cikisAdet = fisler.Count(t => t != null && (t.Contains("CIKIS") || t.Contains("ÇIKIŞ") || t.EndsWith("_C") || t.Contains("SARF") || t.Contains("SATIS")));
                int transferAdet = fisler.Count(t => t != null && t.Contains("TRANSFER"));

                var depolar = _context.tb_Depo.AsNoTracking().ToList();
                var depoDagilim = depoMalzeme.Where(x => x.Miktar != 0)
                    .GroupBy(x => x.DepoKodu)
                    .Select(g => new
                    {
                        Ad = depolar.FirstOrDefault(d => d.DepoKodu == g.Key)?.DepoAdi ?? g.Key,
                        Miktar = Math.Round(g.Sum(x => (double)x.Miktar), 1)
                    })
                    .OrderByDescending(x => x.Miktar).ToList();

                var malzemeAdlari = _context.tb_Malzeme.AsNoTracking()
                    .Select(m => new { m.MalzemeKodu, m.MalzemeAdi }).ToList();
                var enCokStoklar = depoMalzeme
                    .GroupBy(x => x.MalzemeKodu)
                    .Select(g => new
                    {
                        Ad = malzemeAdlari.FirstOrDefault(m => m.MalzemeKodu == g.Key)?.MalzemeAdi ?? g.Key,
                        Miktar = Math.Round(g.Sum(x => (double)x.Miktar), 1)
                    })
                    .OrderByDescending(x => x.Miktar).Take(7).ToList();

                return Ok(new
                {
                    toplamMiktar,
                    aktifUrunSayisi,
                    kritikStokSayisi,
                    toplamDepoSayisi,
                    girisAdet,
                    cikisAdet,
                    transferAdet,
                    depoDagilim,
                    enCokStoklar
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====================================================================
        // FIZIKSEL ANALIZ GIRISI (Lot bazli kalite analizi)
        // ====================================================================

        [HttpGet("lotlar")]
        public IActionResult GetLotlar(
            [FromQuery] string arama = "",
            [FromQuery] string analizDurumu = "",
            [FromQuery] int page = 1,
            [FromQuery] int size = 15)
        {
            try
            {
                int activeAnalysisCount = _context.tb_MalzemeOzellikTanim.Count(o => o.Tipi == "KALITE_ANALIZI" && o.Aktif == true);

                var q = from l in _context.tb_MalzemeLot.AsNoTracking()
                        join m in _context.tb_Malzeme.AsNoTracking() on l.MalzemeKodu equals m.MalzemeKodu into mJ
                        from m in mJ.DefaultIfEmpty()
                        select new { l.LotNo, l.MalzemeKodu, MalzemeAdi = m != null ? m.MalzemeAdi : "", l.KayitTarihi, l.KayitSicilNo, l.OnayDurumu };

                if (!string.IsNullOrEmpty(arama))
                    q = q.Where(o => o.LotNo.Contains(arama) || o.MalzemeKodu.Contains(arama) || o.MalzemeAdi.Contains(arama));

                var raw = q.OrderByDescending(o => o.KayitTarihi).ToList();

                var processed = new List<object>();
                foreach (var item in raw)
                {
                    int filled = _context.tb_MalzemeLotAnaliz.Count(la => la.LotNo == item.LotNo && la.Deger != null && la.Deger != "");
                    bool isCompleted = activeAnalysisCount > 0 && filled >= activeAnalysisCount;

                    if (analizDurumu == "TAMAMLANDI" && !isCompleted) continue;
                    if (analizDurumu == "BEKLEMEDE" && isCompleted) continue;

                    processed.Add(new
                    {
                        item.LotNo,
                        item.MalzemeKodu,
                        item.MalzemeAdi,
                        KayitTarihi = item.KayitTarihi.ToString("dd.MM.yyyy HH:mm"),
                        KayitSicilNo = item.KayitSicilNo ?? "-",
                        IsCompleted = isCompleted,
                        OnayDurumu = item.OnayDurumu ?? "TASLAK"
                    });
                }

                int totalCount = processed.Count;
                if (page < 1) page = 1;
                var paged = processed.Skip((page - 1) * size).Take(size).ToList();
                return Ok(new { totalCount, data = paged });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpGet("lot/{lotNo}/analiz")]
        public IActionResult GetLotAnaliz(string lotNo)
        {
            try
            {
                var tanimlar = _context.tb_MalzemeOzellikTanim.AsNoTracking()
                    .Where(o => o.Tipi == "KALITE_ANALIZI" && o.Aktif == true).ToList();
                var current = _context.tb_MalzemeLotAnaliz.AsNoTracking()
                    .Where(o => o.LotNo == lotNo).ToList();

                var list = tanimlar.Select(t => new
                {
                    AnalizID = t.ID,
                    AnalizAdi = t.Tanim,
                    t.VeriTipi,
                    Deger = current.FirstOrDefault(c => c.AnalizID == t.ID)?.Deger ?? ""
                }).ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class LotAnalizItem
        {
            public int AnalizID { get; set; }
            public string Deger { get; set; }
        }

        [HttpPost("lot/{lotNo}/analiz")]
        public async Task<IActionResult> SaveLotAnaliz(string lotNo, [FromBody] List<LotAnalizItem> analizler)
        {
            if (analizler == null) return BadRequest(new { message = "Analiz listesi bos." });
            try
            {
                var old = _context.tb_MalzemeLotAnaliz.Where(o => o.LotNo == lotNo).ToList();
                if (old.Count > 0) _context.tb_MalzemeLotAnaliz.RemoveRange(old);

                foreach (var a in analizler)
                {
                    _context.tb_MalzemeLotAnaliz.Add(new tb_MalzemeLotAnaliz
                    {
                        LotNo = lotNo,
                        AnalizID = a.AnalizID,
                        Deger = a.Deger ?? ""
                    });
                }
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("stokChanged");
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }
    }
}
