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
    public class MalzemeController : ControllerBase
    {
        private readonly IYbsDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public MalzemeController(IYbsDbContext context, IHubContext<ChatHub> hubContext)
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

        private string GetSicilNo()
        {
            return _context.tb_Kullanici.AsNoTracking()
                .Where(u => u.KullaniciID == GetCurrentUserId())
                .Select(u => u.SicilNo).FirstOrDefault() ?? "";
        }

        private string GetAdminBelgeTur()
        {
            return User.FindFirst("AdminBelgeTur")?.Value ?? "";
        }

        // STOKADMIN veya genel ADMIN yetkisi (web ClsYetki.UserYetkiKontrol karsiligi).
        // ONEMLI: Belge yildizla ayrik ("*IT*STOKADMIN*") saklanir; gevsek Contains("ADMIN")
        // "BAKIMADMIN" gibi ILGISIZ tokenlari da eslerdi. Referanstaki gibi tam token eslesmesi yapilir.
        private bool HasStokAdmin()
        {
            var tokens = (GetAdminBelgeTur() ?? "")
                .Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToUpperInvariant())
                .ToList();
            return tokens.Contains("ADMIN") || tokens.Contains("STOKADMIN");
        }

        // ====================================================================
        // MALZEME LISTESI
        // ====================================================================

        [HttpGet("list")]
        public IActionResult GetList(
            [FromQuery] string hizli = "",
            [FromQuery] string durum = "",
            [FromQuery] string grupKodu = "",
            [FromQuery] string stokTakip = "",
            [FromQuery] string urunTipKodu = "",
            [FromQuery] bool skuDahil = false,
            [FromQuery] string orderBy = "",
            [FromQuery] string orderDir = "",
            [FromQuery] int PageIndex = 1,
            [FromQuery] int PageSize = 15)
        {
            try
            {
                var query = from m in _context.tb_Malzeme.AsNoTracking()
                            join g in _context.tb_MalzemeGrubu.AsNoTracking() on m.MalzemeGrupKodu equals g.GrupKodu into grp
                            from g in grp.DefaultIfEmpty()
                            join t in _context.tb_MalzemeTip.AsNoTracking() on m.MalzemeTipKodu equals t.MalzemeTipKodu into tps
                            from t in tps.DefaultIfEmpty()
                            select new
                            {
                                m.MalzemeKodu,
                                m.MalzemeAdi,
                                m.MalzemeGrupKodu,
                                m.BirimKodu,
                                m.Aktif,
                                m.ParentMalzemeKodu,
                                m.StokTakip,
                                m.LotTakibi,
                                m.SatinAlinabilir,
                                m.Satilabilir,
                                m.Uretilebilir,
                                m.MalzemeTipKodu,
                                m.KoleksiyonKodu,
                                m.Marka,
                                m.Model,
                                m.Barkod,
                                m.Ek1,
                                m.Ek2,
                                m.Ek3,
                                m.Ek4,
                                BolumKodu = g.UstGrupKodu ?? "",
                                GrupAdi = g.GrupAdi ?? "",
                                TipAdi = t.MalzemeTipAdi ?? ""
                            };

                // SKU (varyant) dahil degilse yalnizca ana malzemeler
                if (!skuDahil)
                    query = query.Where(o => o.ParentMalzemeKodu == null || o.ParentMalzemeKodu == "");

                if (!string.IsNullOrEmpty(hizli))
                    query = query.Where(o => o.MalzemeAdi.Contains(hizli) || o.MalzemeKodu.Contains(hizli) || (o.Barkod != null && o.Barkod.Contains(hizli)));

                if (!string.IsNullOrEmpty(durum))
                {
                    bool d = (durum == "True" || durum == "1" || durum == "true");
                    query = query.Where(o => o.Aktif == d);
                }

                if (!string.IsNullOrEmpty(grupKodu))
                    query = query.Where(o => o.MalzemeGrupKodu == grupKodu);

                if (!string.IsNullOrEmpty(urunTipKodu))
                    query = query.Where(o => o.MalzemeTipKodu == urunTipKodu);

                if (!string.IsNullOrEmpty(stokTakip))
                {
                    bool st = (stokTakip == "True" || stokTakip == "1" || stokTakip == "true");
                    query = query.Where(o => o.StokTakip == st);
                }

                bool isDesc = orderDir == "desc";
                switch (orderBy)
                {
                    case "MalzemeKodu": query = isDesc ? query.OrderByDescending(o => o.MalzemeKodu) : query.OrderBy(o => o.MalzemeKodu); break;
                    case "MalzemeGrupKodu": query = isDesc ? query.OrderByDescending(o => o.GrupAdi) : query.OrderBy(o => o.GrupAdi); break;
                    case "Aktif": query = isDesc ? query.OrderByDescending(o => o.Aktif) : query.OrderBy(o => o.Aktif); break;
                    default: query = isDesc ? query.OrderByDescending(o => o.MalzemeAdi) : query.OrderBy(o => o.MalzemeAdi); break;
                }

                int totalCount = query.Count();
                if (PageIndex < 1) PageIndex = 1;

                var data = query
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                return Ok(new { totalCount, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // QR / barkod ile malzeme bulma: MalzemeKodu veya Barkod tam eşleşme
        // (yoksa Contains yedeği). Telefondan okutunca kullanılır.
        [HttpGet("find")]
        public IActionResult FindByCode([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Kod bos olamaz." });
            try
            {
                var c = code.Trim();
                var m = _context.tb_Malzeme.AsNoTracking()
                    .Where(o => o.MalzemeKodu == c || o.Barkod == c)
                    .Select(o => new { o.MalzemeKodu, o.MalzemeAdi, o.BirimKodu, o.Barkod, o.StokTakip, o.LotTakibi })
                    .FirstOrDefault();
                if (m == null)
                {
                    m = _context.tb_Malzeme.AsNoTracking()
                        .Where(o => o.MalzemeKodu.Contains(c) || (o.Barkod != null && o.Barkod.Contains(c)))
                        .Select(o => new { o.MalzemeKodu, o.MalzemeAdi, o.BirimKodu, o.Barkod, o.StokTakip, o.LotTakibi })
                        .FirstOrDefault();
                }
                if (m == null) return Ok(new { found = false });
                return Ok(new { found = true, malzeme = m });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====================================================================
        // DROPDOWN VERILERI (Bolum / Grup / Birim / Tip)
        // ====================================================================

        [HttpGet("dropdowns")]
        public IActionResult GetDropdowns()
        {
            try
            {
                var bolumler = _context.tb_MalzemeGrubu.AsNoTracking()
                    .Where(o => o.Aktif && (o.UstGrupKodu == null || o.UstGrupKodu == ""))
                    .OrderBy(o => o.GrupAdi)
                    .Select(o => new { ID = o.GrupKodu, Tanim = o.GrupAdi })
                    .ToList();

                var gruplar = _context.tb_MalzemeGrubu.AsNoTracking()
                    .Where(o => o.Aktif)
                    .OrderBy(o => o.GrupAdi)
                    .Select(o => new { ID = o.GrupKodu, Tanim = o.GrupAdi, ParentID = o.UstGrupKodu ?? "" })
                    .ToList();

                var birimler = _context.tb_Birim.AsNoTracking()
                    .OrderBy(o => o.BirimAdi)
                    .Select(o => new { ID = o.BirimKodu, Tanim = o.BirimAdi })
                    .ToList();

                var tipler = _context.tb_MalzemeTip.AsNoTracking()
                    .Where(o => o.Aktif)
                    .OrderBy(o => o.MalzemeTipAdi)
                    .Select(o => new { ID = o.MalzemeTipKodu, Tanim = o.MalzemeTipAdi })
                    .ToList();

                var koleksiyonlar = _context.tb_Koleksiyon.AsNoTracking()
                    .Where(o => o.Aktif)
                    .OrderBy(o => o.KoleksiyonAdi)
                    .Select(o => new { ID = o.KoleksiyonKodu, Tanim = o.KoleksiyonAdi })
                    .ToList();

                return Ok(new { Bolumler = bolumler, Gruplar = gruplar, Birimler = birimler, Tipler = tipler, Koleksiyonlar = koleksiyonlar });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ====================================================================
        // MALZEME KAYIT / GUNCELLEME
        // ====================================================================

        public class MalzemeSaveRequest
        {
            public string MalzemeKodu { get; set; }
            public string MalzemeAdi { get; set; }
            public string MalzemeGrupKodu { get; set; }
            public string BirimKodu { get; set; }
            public string MalzemeTipKodu { get; set; }
            public string Marka { get; set; }
            public string Model { get; set; }
            public string Barkod { get; set; }
            public string KoleksiyonKodu { get; set; }
            public string Ek1 { get; set; }
            public string Ek2 { get; set; }
            public string Ek3 { get; set; }
            public string Ek4 { get; set; }
            public bool StokTakip { get; set; }
            public bool LotTakibi { get; set; }
            public bool SatinAlinabilir { get; set; }
            public bool Satilabilir { get; set; }
            public bool Uretilebilir { get; set; }
            public bool Aktif { get; set; } = true;
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] MalzemeSaveRequest req)
        {
            if (req == null) return BadRequest(new { message = "Gecersiz istek." });
            if (string.IsNullOrWhiteSpace(req.MalzemeAdi))
                return BadRequest(new { message = "Malzeme adi bos olamaz." });

            try
            {
                string kod = req.MalzemeKodu;

                // Yeni kayit ve kod bos ise otomatik uret (grup bazli).
                if (string.IsNullOrEmpty(kod))
                {
                    if (string.IsNullOrEmpty(req.MalzemeGrupKodu))
                        return BadRequest(new { message = "Otomatik kod icin grup secimi gerekli." });
                    kod = MalzemeKoduUret(req.MalzemeGrupKodu);
                }

                var m = _context.tb_Malzeme.FirstOrDefault(o => o.MalzemeKodu == kod);
                bool yeni = false;
                if (m == null)
                {
                    m = new tb_Malzeme { MalzemeKodu = kod, KayitTar = DateTime.Now };
                    _context.tb_Malzeme.Add(m);
                    yeni = true;
                }

                m.MalzemeAdi = req.MalzemeAdi;
                m.MalzemeGrupKodu = req.MalzemeGrupKodu;
                m.BirimKodu = req.BirimKodu;
                m.MalzemeTipKodu = req.MalzemeTipKodu;
                m.Marka = req.Marka;
                m.Model = req.Model;
                m.Barkod = req.Barkod;
                m.KoleksiyonKodu = req.KoleksiyonKodu;
                m.Ek1 = req.Ek1;
                m.Ek2 = req.Ek2;
                m.Ek3 = req.Ek3;
                m.Ek4 = req.Ek4;
                m.StokTakip = req.StokTakip;
                m.LotTakibi = req.LotTakibi;
                m.SatinAlinabilir = req.SatinAlinabilir;
                m.Satilabilir = req.Satilabilir;
                m.Uretilebilir = req.Uretilebilir;
                m.Aktif = req.Aktif;

                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = yeni ? "Malzeme eklendi." : "Malzeme guncellendi.", malzemeKodu = kod });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // Web MalzemeKoduUret mantiginin sadelestirilmis karsiligi.
        // Format: [TIP][BOLUM][GRUP][OZEL][8 haneli sira]
        private string MalzemeKoduUret(string grupKodu)
        {
            string tip = "ML";
            string[] parts = grupKodu.Split('-');
            string bolum = parts.Length > 0 ? parts[0] : "00";
            string grup = parts.Length > 1 ? parts[1] : "00";
            string ozel = "STD";
            if (grup == "AM" || (parts.Length > 0 && parts[0] == "AM")) tip = "AM";

            string prefix = tip + bolum + grup + ozel;

            var lastCode = _context.tb_Malzeme.AsNoTracking()
                .Where(o => o.MalzemeKodu.StartsWith(prefix))
                .OrderByDescending(o => o.MalzemeKodu)
                .Select(o => o.MalzemeKodu)
                .FirstOrDefault();

            int nextSeq = 1;
            if (lastCode != null && lastCode.Length > prefix.Length)
            {
                if (int.TryParse(lastCode.Substring(prefix.Length), out int seq))
                    nextSeq = seq + 1;
            }

            return prefix + (10000000 + nextSeq).ToString();
        }

        // ====================================================================
        // MALZEME SIL
        // ====================================================================

        [HttpDelete("{kodu}")]
        public async Task<IActionResult> Delete(string kodu)
        {
            try
            {
                var m = _context.tb_Malzeme.FirstOrDefault(o => o.MalzemeKodu == kodu);
                if (m == null) return NotFound(new { message = "Kayit bulunamadi." });

                if (_context.tb_MalzemeHareket.Any(o => o.MalzemeKodu == kodu))
                    return BadRequest(new { message = "Hareket gormus malzeme silinemez." });

                var altKodlar = _context.tb_Malzeme.Where(o => o.ParentMalzemeKodu == kodu).Select(o => o.MalzemeKodu).ToList();
                if (altKodlar.Count > 0 && _context.tb_MalzemeHareket.Any(o => altKodlar.Contains(o.MalzemeKodu)))
                    return BadRequest(new { message = "Alt malzemeleri hareket gormus, silinemez." });

                var altlar = _context.tb_Malzeme.Where(o => o.ParentMalzemeKodu == kodu).ToList();
                if (altlar.Count > 0) _context.tb_Malzeme.RemoveRange(altlar);
                _context.tb_Malzeme.Remove(m);
                _context.SaveChanges();

                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Malzeme silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ====================================================================
        // MALZEME GRUBU (Bolum / Grup hiyerarsisi)
        // ====================================================================

        [HttpGet("gruplar")]
        public IActionResult GetGruplar()
        {
            try
            {
                var list = _context.tb_MalzemeGrubu.AsNoTracking()
                    .OrderBy(o => o.GrupAdi)
                    .Select(o => new { o.GrupKodu, o.GrupAdi, o.UstGrupKodu, o.Aktif })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public class GrupSaveRequest
        {
            public string Kodu { get; set; }
            public string Adi { get; set; }
            public string UstGrupKodu { get; set; }
            public bool Aktif { get; set; } = true;
        }

        [HttpPost("gruplar")]
        public async Task<IActionResult> SaveGrup([FromBody] GrupSaveRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Kodu) || string.IsNullOrWhiteSpace(req.Adi))
                return BadRequest(new { message = "Grup kodu ve adi zorunludur." });

            try
            {
                var grp = _context.tb_MalzemeGrubu.FirstOrDefault(o => o.GrupKodu == req.Kodu);
                if (grp == null)
                {
                    grp = new tb_MalzemeGrubu { GrupKodu = req.Kodu };
                    _context.tb_MalzemeGrubu.Add(grp);
                }
                grp.GrupAdi = req.Adi;
                grp.UstGrupKodu = string.IsNullOrEmpty(req.UstGrupKodu) || req.UstGrupKodu == "0" ? null : req.UstGrupKodu;
                grp.Aktif = req.Aktif;
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ====================================================================
        // MALZEME TEDARIKCI KODLARI
        // ====================================================================

        [HttpGet("{malzemeKodu}/tedarikci-kodlari")]
        public IActionResult GetTedarikciKodlari(string malzemeKodu)
        {
            try
            {
                var list = (from k in _context.tb_MalzemeTedarikciKodu.AsNoTracking()
                            join t in _context.tb_Tedarikci.AsNoTracking() on k.TedarikciKodu equals t.TedarikciKodu into tg
                            from t in tg.DefaultIfEmpty()
                            where k.MalzemeKodu == malzemeKodu
                            select new
                            {
                                k.ID,
                                k.MalzemeKodu,
                                k.TedarikciKodu,
                                TedarikciUnvan = t.Unvan ?? "",
                                k.TedarikciStokKodu
                            }).ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public class TedarikciKoduSaveRequest
        {
            public string MalzemeKodu { get; set; }
            public string TedarikciKodu { get; set; }
            public string TedarikciStokKodu { get; set; }
        }

        [HttpPost("tedarikci-kodu")]
        public async Task<IActionResult> SaveTedarikciKodu([FromBody] TedarikciKoduSaveRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.MalzemeKodu) ||
                string.IsNullOrWhiteSpace(req.TedarikciKodu) || string.IsNullOrWhiteSpace(req.TedarikciStokKodu))
                return BadRequest(new { message = "Lutfen tum alanlari doldurunuz." });

            try
            {
                var existing = _context.tb_MalzemeTedarikciKodu
                    .FirstOrDefault(k => k.MalzemeKodu == req.MalzemeKodu && k.TedarikciKodu == req.TedarikciKodu);
                if (existing != null)
                {
                    existing.TedarikciStokKodu = req.TedarikciStokKodu;
                }
                else
                {
                    _context.tb_MalzemeTedarikciKodu.Add(new tb_MalzemeTedarikciKodu
                    {
                        MalzemeKodu = req.MalzemeKodu,
                        TedarikciKodu = req.TedarikciKodu,
                        TedarikciStokKodu = req.TedarikciStokKodu
                    });
                }
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("tedarikci-kodu/{id}")]
        public async Task<IActionResult> DeleteTedarikciKodu(int id)
        {
            try
            {
                var item = _context.tb_MalzemeTedarikciKodu.FirstOrDefault(k => k.ID == id);
                if (item != null)
                {
                    _context.tb_MalzemeTedarikciKodu.Remove(item);
                    _context.SaveChanges();
                    await _hubContext.Clients.All.SendAsync("malzemeChanged");
                }
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ====================================================================
        // FIZIKSEL ANALIZ TANIMLARI (tb_MalzemeOzellikTanim / KALITE_ANALIZI)
        // ====================================================================

        [HttpGet("fiziksel-analiz")]
        public IActionResult GetFizikselAnaliz()
        {
            try
            {
                var list = _context.tb_MalzemeOzellikTanim.AsNoTracking()
                    .Where(o => o.Tipi == "KALITE_ANALIZI" && o.Aktif == true)
                    .Select(o => new { o.ID, AnalizAdi = o.Tanim, o.VeriTipi, o.Aktif })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public class FizikselAnalizSaveRequest
        {
            public int ID { get; set; }
            public string AnalizAdi { get; set; }
            public string VeriTipi { get; set; }
            public bool Aktif { get; set; } = true;
        }

        [HttpPost("fiziksel-analiz")]
        public async Task<IActionResult> SaveFizikselAnaliz([FromBody] FizikselAnalizSaveRequest req)
        {
            if (!HasStokAdmin())
                return StatusCode(403, new { message = "Bu islem icin yetkiniz bulunmamaktadir (STOKADMIN)." });
            if (req == null || string.IsNullOrWhiteSpace(req.AnalizAdi))
                return BadRequest(new { message = "Analiz adi bos olamaz." });
            if (req.VeriTipi != "SAYISAL" && req.VeriTipi != "METINSEL")
                return BadRequest(new { message = "Gecersiz veri tipi." });

            try
            {
                tb_MalzemeOzellikTanim tanim;
                if (req.ID > 0)
                {
                    tanim = _context.tb_MalzemeOzellikTanim.FirstOrDefault(t => t.ID == req.ID);
                    if (tanim == null) return NotFound(new { message = "Tanim bulunamadi." });
                }
                else
                {
                    tanim = new tb_MalzemeOzellikTanim
                    {
                        Tipi = "KALITE_ANALIZI",
                        Kod = req.AnalizAdi.ToUpperInvariant().Replace(" ", "_")
                    };
                    _context.tb_MalzemeOzellikTanim.Add(tanim);
                }

                tanim.Tanim = req.AnalizAdi;
                tanim.VeriTipi = req.VeriTipi;
                tanim.Aktif = req.Aktif;
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("fiziksel-analiz/{id}")]
        public async Task<IActionResult> DeleteFizikselAnaliz(int id)
        {
            if (!HasStokAdmin())
                return StatusCode(403, new { message = "Bu islem icin yetkiniz bulunmamaktadir (STOKADMIN)." });
            try
            {
                var tanim = _context.tb_MalzemeOzellikTanim.FirstOrDefault(t => t.ID == id);
                if (tanim == null) return NotFound(new { message = "Tanim bulunamadi." });
                _context.tb_MalzemeOzellikTanim.Remove(tanim);
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ====================================================================
        // OZELLIK (ATTRIBUTE) YONETIMI + SKU / VARYANT
        // ====================================================================

        [HttpGet("ozellik-tanimlar")]
        public IActionResult GetOzellikTanimlar()
        {
            try
            {
                var list = _context.tb_MalzemeOzellikTanim.AsNoTracking()
                    .Where(o => o.Aktif == true && (o.Tipi == "URUN_OZELLIGI" || o.Tipi == null || o.Tipi == ""))
                    .OrderBy(o => o.Tanim)
                    .Select(o => new { o.ID, o.Tanim, o.Kod, o.Tipi })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class OzellikTanimSaveRequest { public int ID { get; set; } public string Tanim { get; set; } public string Kod { get; set; } }

        [HttpPost("ozellik-tanim")]
        public async Task<IActionResult> SaveOzellikTanim([FromBody] OzellikTanimSaveRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Tanim))
                return BadRequest(new { message = "Ozellik adi bos olamaz." });
            try
            {
                tb_MalzemeOzellikTanim oz = req.ID > 0 ? _context.tb_MalzemeOzellikTanim.FirstOrDefault(o => o.ID == req.ID) : null;
                if (oz == null)
                {
                    oz = new tb_MalzemeOzellikTanim { Aktif = true, Tipi = "URUN_OZELLIGI", VeriTipi = "METINSEL" };
                    _context.tb_MalzemeOzellikTanim.Add(oz);
                }
                oz.Tanim = req.Tanim;
                oz.Kod = req.Kod;
                if (string.IsNullOrEmpty(oz.Tipi)) oz.Tipi = "URUN_OZELLIGI";
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili.", id = oz.ID });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpDelete("ozellik-tanim/{id}")]
        public async Task<IActionResult> DeleteOzellikTanim(int id)
        {
            try
            {
                if (_context.tb_MalzemeOzellikDeger.Any(o => o.OzellikID == id && o.Aktif == true))
                    return BadRequest(new { message = "Bu ozellige ait deger var. Once degerleri siliniz." });
                if (_context.tb_MalzemeOzellik.Any(o => o.OzellikID == id))
                    return BadRequest(new { message = "Bu ozellik bazi malzemelerde kullaniliyor. Silinemez." });
                var oz = _context.tb_MalzemeOzellikTanim.FirstOrDefault(o => o.ID == id);
                if (oz != null) 
                { 
                    oz.Aktif = false; 
                    _context.SaveChanges();
                    await _hubContext.Clients.All.SendAsync("malzemeChanged");
                }
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpGet("ozellik/{ozellikID}/degerler")]
        public IActionResult GetOzellikDegerler(int ozellikID)
        {
            try
            {
                var list = _context.tb_MalzemeOzellikDeger.AsNoTracking()
                    .Where(o => o.Aktif == true && o.OzellikID == ozellikID)
                    .OrderBy(o => o.Sira).ThenBy(o => o.Deger)
                    .Select(o => new { o.ID, o.Deger, o.Kod, o.Sira })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class OzellikDegerSaveRequest { public int ID { get; set; } public int OzellikID { get; set; } public string Deger { get; set; } public string Kod { get; set; } public int Sira { get; set; } }

        [HttpPost("ozellik-deger")]
        public async Task<IActionResult> SaveOzellikDeger([FromBody] OzellikDegerSaveRequest req)
        {
            if (req == null || req.OzellikID <= 0 || string.IsNullOrWhiteSpace(req.Deger))
                return BadRequest(new { message = "Ozellik ve deger zorunludur." });
            try
            {
                tb_MalzemeOzellikDeger val = req.ID > 0 ? _context.tb_MalzemeOzellikDeger.FirstOrDefault(o => o.ID == req.ID) : null;
                if (val == null)
                {
                    val = new tb_MalzemeOzellikDeger { Aktif = true, OzellikID = req.OzellikID };
                    _context.tb_MalzemeOzellikDeger.Add(val);
                }
                val.Deger = req.Deger;
                val.Kod = req.Kod;
                val.Sira = req.Sira;
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili.", id = val.ID });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpDelete("ozellik-deger/{id}")]
        public async Task<IActionResult> DeleteOzellikDeger(int id)
        {
            try
            {
                var val = _context.tb_MalzemeOzellikDeger.FirstOrDefault(o => o.ID == id);
                if (val != null) 
                { 
                    val.Aktif = false; 
                    _context.SaveChanges(); 
                    await _hubContext.Clients.All.SendAsync("malzemeChanged");
                }
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpGet("{malzemeKodu}/ozellikler")]
        public IActionResult GetMalzemeOzellikler(string malzemeKodu)
        {
            try
            {
                var list = (from mo in _context.tb_MalzemeOzellik.AsNoTracking()
                            join ot in _context.tb_MalzemeOzellikTanim.AsNoTracking() on mo.OzellikID equals ot.ID
                            where mo.MalzemeKodu == malzemeKodu && ot.Aktif == true
                            orderby mo.Sira
                            select new { mo.ID, mo.OzellikID, OzellikAdi = ot.Tanim, OzellikKodu = ot.Kod, mo.ZorunluMu, mo.Sira })
                            .ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class MalzemeOzellikEkleRequest { public string MalzemeKodu { get; set; } public int OzellikID { get; set; } public bool Zorunlu { get; set; } public int Sira { get; set; } }

        [HttpPost("ozellik")]
        public async Task<IActionResult> AddMalzemeOzellik([FromBody] MalzemeOzellikEkleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.MalzemeKodu) || req.OzellikID <= 0)
                return BadRequest(new { message = "Malzeme ve ozellik zorunludur." });
            try
            {
                if (_context.tb_MalzemeOzellik.Any(o => o.MalzemeKodu == req.MalzemeKodu && o.OzellikID == req.OzellikID))
                    return BadRequest(new { message = "Bu ozellik zaten ekli." });
                _context.tb_MalzemeOzellik.Add(new tb_MalzemeOzellik
                {
                    MalzemeKodu = req.MalzemeKodu,
                    MalzemeID = 0,
                    OzellikID = req.OzellikID,
                    ZorunluMu = req.Zorunlu,
                    Sira = req.Sira
                });
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpGet("{parentKodu}/varyantlar")]
        public IActionResult GetVaryantlar(string parentKodu)
        {
            try
            {
                var list = _context.tb_Malzeme.AsNoTracking()
                    .Where(m => m.ParentMalzemeKodu == parentKodu)
                    .OrderBy(m => m.MalzemeKodu)
                    .Select(m => new { m.MalzemeKodu, m.MalzemeAdi, m.Aktif })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class SkuSecim { public int OzellikID { get; set; } public int DegerID { get; set; } }
        public class SkuOlusturRequest { public string ParentKodu { get; set; } public List<SkuSecim> Secimler { get; set; } }

        [HttpPost("sku")]
        public async Task<IActionResult> SkuOlustur([FromBody] SkuOlusturRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ParentKodu) || req.Secimler == null || req.Secimler.Count == 0)
                return BadRequest(new { message = "Ana malzeme ve ozellik secimi zorunludur." });
            try
            {
                var parent = _context.tb_Malzeme.FirstOrDefault(o => o.MalzemeKodu == req.ParentKodu);
                if (parent == null) return NotFound(new { message = "Ana malzeme bulunamadi." });

                bool ozellikVar = _context.tb_MalzemeOzellik.Any(o => o.MalzemeKodu == req.ParentKodu);
                if (!ozellikVar)
                {
                    int sira = 1;
                    foreach (var s in req.Secimler)
                        _context.tb_MalzemeOzellik.Add(new tb_MalzemeOzellik { MalzemeKodu = req.ParentKodu, MalzemeID = 0, OzellikID = s.OzellikID, ZorunluMu = true, Sira = sira++ });
                    _context.SaveChanges();
                }

                var malzemeOzellikleri = _context.tb_MalzemeOzellik.AsNoTracking()
                    .Where(o => o.MalzemeKodu == req.ParentKodu).OrderBy(o => o.Sira).ToList();

                string skuKodu = req.ParentKodu;
                string varyantAdiEk = "";
                foreach (var mo in malzemeOzellikleri)
                {
                    var secim = req.Secimler.FirstOrDefault(s => s.OzellikID == mo.OzellikID);
                    if (secim != null)
                    {
                        var deg = _context.tb_MalzemeOzellikDeger.AsNoTracking().FirstOrDefault(d => d.ID == secim.DegerID);
                        if (deg != null)
                        {
                            skuKodu += "-" + (string.IsNullOrEmpty(deg.Kod) ? deg.ID.ToString() : deg.Kod);
                            varyantAdiEk += " " + deg.Deger;
                        }
                    }
                }

                if (skuKodu == req.ParentKodu) return BadRequest(new { message = "Ozellik secimi yapilmadi." });
                if (_context.tb_Malzeme.Any(o => o.MalzemeKodu == skuKodu))
                    return BadRequest(new { message = "Bu varyant zaten mevcut: " + skuKodu });

                _context.tb_Malzeme.Add(new tb_Malzeme
                {
                    MalzemeKodu = skuKodu,
                    MalzemeAdi = parent.MalzemeAdi + varyantAdiEk,
                    ParentMalzemeKodu = req.ParentKodu,
                    MalzemeGrupKodu = parent.MalzemeGrupKodu,
                    BirimKodu = parent.BirimKodu,
                    MalzemeTipKodu = parent.MalzemeTipKodu,
                    Aktif = true,
                    KayitTar = DateTime.Now
                });

                _context.tb_MalzemeSKU.Add(new tb_MalzemeSKU
                {
                    ParentMalzemeKodu = req.ParentKodu,
                    SKUKodu = skuKodu,
                    BilesenJSON = JsonSerializer.Serialize(req.Secimler),
                    FiyatFarki = 0,
                    Aktif = true
                });

                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Varyant olusturuldu.", skuKodu });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        // ====================================================================
        // MALZEME YONETIMI AYARLARI (tb_SistemAyarlari / MaterialSettings JSON)
        // ====================================================================

        private static readonly object DefaultMaterialSettings = new
        {
            Kod = new { Visible = true, Required = true },
            Ad = new { Visible = true, Required = true },
            Bolum = new { Visible = true, Required = true },
            Kategori = new { Visible = true, Required = true },
            Birim = new { Visible = true, Required = true },
            Tip = new { Visible = true, Required = false },
            Marka = new { Visible = true, Required = false },
            Model = new { Visible = true, Required = false },
            Koleksiyon = new { Visible = true, Required = false },
            Ek1 = new { Visible = true, Required = false },
            Ek2 = new { Visible = true, Required = false },
            Ek3 = new { Visible = true, Required = false },
            Ek4 = new { Visible = true, Required = false },
            Aktif = new { Visible = true, Required = false },
            StokTakip = new { Visible = true, Required = false },
            LotTakibi = new { Visible = true, Required = false },
            Uretilebilir = new { Visible = true, Required = false },
            SatinAlinabilir = new { Visible = true, Required = false },
            Satilabilir = new { Visible = true, Required = false },
            LotTerimi = "Lot",
            FizikselAnalizAktif = false
        };

        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            try
            {
                var setting = _context.tb_SistemAyarlari.AsNoTracking().FirstOrDefault(o => o.AyarKey == "MaterialSettings");
                if (setting != null && !string.IsNullOrEmpty(setting.AyarValue))
                {
                    using var doc = JsonDocument.Parse(setting.AyarValue);
                    return Ok(new { success = true, settings = doc.RootElement.Clone() });
                }
                return Ok(new { success = true, settings = DefaultMaterialSettings });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // Ayarlari kaydeder. Govde: settings JSON nesnesi (oldugu gibi saklanir).
        [HttpPost("settings")]
        public async Task<IActionResult> SaveSettings([FromBody] JsonElement settings)
        {
            if (!HasStokAdmin())
                return StatusCode(403, new { message = "Bu islem icin yetkiniz bulunmamaktadir (STOKADMIN)." });
            try
            {
                string json = settings.GetRawText();
                var setting = _context.tb_SistemAyarlari.FirstOrDefault(o => o.AyarKey == "MaterialSettings");
                if (setting == null)
                {
                    setting = new tb_SistemAyarlari { AyarKey = "MaterialSettings" };
                    _context.tb_SistemAyarlari.Add(setting);
                }
                setting.AyarValue = json;
                setting.GuncellemeTarihi = DateTime.Now;
                setting.GuncelleyenSicil = GetSicilNo();
                _context.SaveChanges();
                await _hubContext.Clients.All.SendAsync("malzemeChanged");
                return Ok(new { success = true, message = "Malzeme yonetim ayarlari kaydedildi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
