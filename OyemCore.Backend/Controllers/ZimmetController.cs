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

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ZimmetController : ControllerBase
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;

        public ZimmetController(IYbsDbContext context, IPushNotificationService pushNotificationService)
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
        // KULLANICI I??LEMLERI (MY DEBITS)
        // ====================================================================

        [HttpGet("my-debits")]
        public IActionResult GetMyDebits([FromQuery] string search = "")
        {
            try
            {
                var user = GetCurrentUser();
                var query = _context.viewAygitList.AsNoTracking().Where(o => o.ZimmetliSicil == user.SicilNo);

                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower().Replace(" ", "");
                    query = query.Where(o => o.Tanim.ToLower().Contains(searchLower) || 
                                             o.SeriNo.ToLower().Contains(searchLower) ||
                                             o.DemirbasKodu.ToLower().Contains(searchLower));
                }

                var list = query.OrderByDescending(o => o.AygitID).ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Zimmetlerim listesi alinirken hata olustu: {ex.Message}" });
            }
        }

        public class ObjectionModel
        {
            public int AygitId { get; set; }
            public string Aciklama { get; set; }
        }

        [HttpPost("objection")]
        public IActionResult ReportObjection([FromBody] ObjectionModel model)
        {
            try
            {
                var user = GetCurrentUser();
                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == model.AygitId);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });

                if (aygit.ZimmetliSicil != user.SicilNo)
                {
                    return BadRequest(new { message = "Sadece kendi üzerinize zimmetli demirbaslar için hata bildirebilirsiniz." });
                }

                aygit.HataBildir = true;
                
                // Add to log
                _context.tb_Log.Add(new tb_Log
                {
                    Eposta = user.Eposta,
                    SicilNo = user.SicilNo,
                    Konu = $"DEMIRBAS_HATA - AygitID: {model.AygitId} [Mobil]",
                    Aciklama = $"Zimmetli personel hata/itiraz bildirdi: {model.Aciklama}",
                    Cihaz = "mobil",
                    KayitTar = DateTime.Now
                });

                _context.SaveChanges();
                return Ok(new { success = true, message = "Hata/Itiraz bildirimi basariyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Itiraz kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // Y?NETICI/ZIMMET SORUMLUSU I??LEMLERI (DEMIRBA?? Y?NETIMI)
        // ====================================================================

        private bool IsZimmetManager(tb_Kullanici user)
        {
            return user.Yonetici == true || user.ZimmetSorumlusu == true || user.KullaniciAdi == "admin";
        }

        [HttpGet("all-assets")]
        public IActionResult GetAllAssets([FromQuery] string search = "", [FromQuery] string categoryId = "0", [FromQuery] string brandId = "0", [FromQuery] string status = "0", [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 15)
        {
            try
            {
                var user = GetCurrentUser();
                if (!IsZimmetManager(user)) return Forbid();

                var query = _context.viewAygitList.AsNoTracking();

                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower().Replace(" ", "");
                    query = query.Where(o => o.AygitID.ToString().Contains(search) ||
                                             o.SeriNo.ToLower().Contains(searchLower) ||
                                             o.DemirbasKodu.ToLower().Contains(searchLower) ||
                                             o.Tanim.ToLower().Contains(searchLower) ||
                                             o.ZimmetliSicil.ToLower().Contains(searchLower));
                }

                if (categoryId != "0")
                {
                    if (int.TryParse(categoryId, out int catId))
                        query = query.Where(o => o.UstKatID == catId || o.AygitKategoriID == catId);
                }

                if (brandId != "0" && int.TryParse(brandId, out int brndId))
                {
                    query = query.Where(o => o.MarkaID == brndId);
                }

                if (status == "1") // Bosta (Zimmetlenebilir)
                    query = query.Where(o => o.Durum == true);
                else if (status == "2") // Zimmetli
                    query = query.Where(o => o.Durum == false);

                int totalCount = query.Count();

                var list = query.OrderByDescending(o => o.AygitID)
                                .Skip((pageIndex - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

                return Ok(new { totalCount, data = list });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Demirbas listesi alinirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("asset/{id}")]
        public IActionResult GetAssetDetail(int id)
        {
            try
            {
                var user = GetCurrentUser();
                if (!IsZimmetManager(user)) return Forbid();

                var aygit = _context.tb_Aygit.AsNoTracking().FirstOrDefault(a => a.AygitID == id);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });

                // Map extra names
                var detail = new
                {
                    aygit.AygitID,
                    aygit.Tanim,
                    aygit.SeriNo,
                    aygit.Aciklama,
                    aygit.AygitKategoriID,
                    aygit.MarkaID,
                    aygit.Miktar,
                    aygit.SorumluDepKod,
                    aygit.HataBildir,
                    aygit.DemirbasKodu,
                    aygit.Konum,
                    aygit.MasrafMerkezi,
                    aygit.Durum,
                    aygit.KullanimSekli,
                    aygit.KayitTar,
                    aygit.ZimmetliSicil,
                    aygit.HurdaDurum,
                    aygit.BarkodOnay,
                    aygit.Ozellik1,
                    aygit.Ozellik2,
                    aygit.Ozellik3,
                    aygit.Ozellik4,
                    BrandName = _context.tb_Marka.AsNoTracking().FirstOrDefault(m => m.MarkaID == aygit.MarkaID)?.MarkaAdi ?? "",
                    CategoryName = _context.tb_AygitKategori.AsNoTracking().FirstOrDefault(k => k.AygitKategoriID == aygit.AygitKategoriID)?.Tanim ?? "",
                    ZimmetliAdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == aygit.ZimmetliSicil)?.AdSoyad ?? ""
                };

                return Ok(detail);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Detay bilgisi alinirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("asset/{id}/history")]
        public IActionResult GetAssetHistory(int id)
        {
            try
            {
                var user = GetCurrentUser();
                if (!IsZimmetManager(user)) return Forbid();

                var history = (from ap in _context.tb_AygitPersonel
                               join p in _context.tb_Personel on ap.PersonelSicil equals p.SicilNo into ps
                               from p in ps.DefaultIfEmpty()
                               join te in _context.tb_Personel on ap.TeslimEdenSicil equals te.SicilNo into tes
                               from te in tes.DefaultIfEmpty()
                               join ta in _context.tb_Personel on ap.TeslimAlanSicil equals ta.SicilNo into tas
                               from ta in tas.DefaultIfEmpty()
                               where ap.AygitID == id
                               orderby ap.TeslimEtTar descending
                               select new
                               {
                                   ap.AygitPersonelID,
                                   ap.AygitID,
                                   ap.PersonelSicil,
                                   PersonelAdSoyad = p != null ? p.AdSoyad : (ap.PersonelAdSoyad ?? ""),
                                   TeslimEtTarStr = ap.TeslimEtTar != null ? ap.TeslimEtTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                   TeslimEden = te != null ? te.AdSoyad : (ap.TeslimEdenSicil ?? "Belirtilmemis"),
                                   ap.Aciklama,
                                   TeslimAlan = ta != null ? ta.AdSoyad : (ap.TeslimAlanSicil ?? ""),
                                   TeslimAlTarStr = ap.TeslimAlTar != null ? ap.TeslimAlTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                   KullanimSekli = ""
                               }).ToList();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Zimmet geçmisi alinirken hata olustu: {ex.Message}" });
            }
        }

        public class AssignModel
        {
            public int AygitId { get; set; }
            public string SicilNo { get; set; }
            public string Aciklama { get; set; }
            public string KullanimSekli { get; set; }
        }

        [HttpPost("assign")]
        public IActionResult AssignAsset([FromBody] AssignModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == model.AygitId);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });
                if (aygit.HurdaDurum == true) return BadRequest(new { message = "Bu demirbas hurda durumundadir, zimmetlenemez." });
                if (aygit.BakimDurumu == true) return BadRequest(new { message = "Bu demirbas su anda bakim/tamir surecinde, zimmetlenemez." });
                if (aygit.Durum == false) return BadRequest(new { message = "Bu demirbas zaten baska bir personele zimmetlidir." });

                var targetPersonel = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == model.SicilNo);
                if (targetPersonel == null) return BadRequest(new { message = "Zimmetlenecek personel bulunamadi." });

                // Update Asset
                aygit.ZimmetliSicil = model.SicilNo;
                aygit.Durum = false; // Mapped to false meaning Debited (Zimmetli)
                
                // KullanimSekli is limited to Max 5 chars in DB
                string kullanimSekli = !string.IsNullOrEmpty(model.KullanimSekli) ? model.KullanimSekli.Trim() : "??AHSI";
                aygit.KullanimSekli = kullanimSekli.Length > 5 ? kullanimSekli.Substring(0, 5) : kullanimSekli;

                // Add to history
                var apLog = new tb_AygitPersonel
                {
                    AygitID = model.AygitId,
                    PersonelSicil = model.SicilNo,
                    PersonelAdSoyad = !string.IsNullOrEmpty(targetPersonel.AdSoyad) && targetPersonel.AdSoyad.Length > 100 
                        ? targetPersonel.AdSoyad.Substring(0, 100) 
                        : (targetPersonel.AdSoyad ?? ""),
                    TeslimEtTar = DateTime.Now,
                    TeslimEdenSicil = currentUser.SicilNo,
                    // tb_AygitPersonel.Aciklama is limited to Max 200 chars in DB
                    Aciklama = !string.IsNullOrEmpty(model.Aciklama) && model.Aciklama.Length > 200 
                        ? model.Aciklama.Substring(0, 200) 
                        : (model.Aciklama ?? "")
                };
                _context.tb_AygitPersonel.Add(apLog);

                _context.SaveChanges();
                _ = _pushNotificationService.NotifyAssetAssignedAsync(apLog.AygitPersonelID);
                return Ok(new { success = true, message = $"Zimmet basariyla atandi ({targetPersonel.AdSoyad})." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AssignAsset error: {ex}");
                return BadRequest(new { message = $"Zimmet atanamadi: {ex.Message} | Inner: {ex.InnerException?.Message}" });
            }
        }

        public class ReleaseModel
        {
            public int AygitId { get; set; }
            public string Aciklama { get; set; }
        }

        [HttpPost("release")]
        public IActionResult ReleaseAsset([FromBody] ReleaseModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == model.AygitId);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });
                if (aygit.Durum == true) return BadRequest(new { message = "Bu demirbas zaten bosta." });

                string oldSicil = aygit.ZimmetliSicil;

                // Update active history item
                var activeHistory = _context.tb_AygitPersonel
                    .FirstOrDefault(ap => ap.AygitID == model.AygitId && ap.PersonelSicil == oldSicil && ap.TeslimAlTar == null);
                if (activeHistory != null)
                {
                    activeHistory.TeslimAlTar = DateTime.Now;
                    activeHistory.TeslimAlanSicil = currentUser.SicilNo;
                    if (!string.IsNullOrEmpty(model.Aciklama))
                    {
                        activeHistory.Aciklama += $" | Iade Notu: {model.Aciklama}";
                    }
                }

                // Update Asset to free
                aygit.ZimmetliSicil = "";
                aygit.Durum = true; // Bosta (Zimmetlenebilir)
                aygit.HataBildir = false;

                _context.SaveChanges();
                if (activeHistory != null)
                {
                    _ = _pushNotificationService.NotifyAssetReturnedAsync(activeHistory.AygitPersonelID, currentUser.KullaniciID);
                }
                return Ok(new { success = true, message = "Zimmet basariyla iade alindi ve bosa çıkarildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Zimmet iade alinamadi: {ex.Message}" });
            }
        }

        [HttpPost("barcode-onay/{id}")]
        public IActionResult ConfirmBarcode(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == id);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });

                aygit.BarkodOnay = true;
                _context.SaveChanges();

                return Ok(new { success = true, message = "Barkod onaylandi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Barkod onaylanirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("dropdowns")]
        public IActionResult GetDropdowns()
        {
            try
            {
                var kategoriler = _context.tb_AygitKategori.AsNoTracking()
                    .Select(k => new { id = k.AygitKategoriID, name = k.Tanim, parentId = k.KategoriID })
                    .ToList();

                var markalar = _context.tb_Marka.AsNoTracking()
                    .Select(m => new { id = m.MarkaID, name = m.MarkaAdi })
                    .ToList();

                var departmanlar = _context.tb_Departman.AsNoTracking()
                    .Select(d => new { id = d.Kod, name = d.DepartmanAdi })
                    .ToList();

                var personeller = _context.tb_Personel.AsNoTracking()
                    .Where(p => p.Durum == true)
                    .Select(p => new { sicilNo = p.SicilNo, name = p.AdSoyad })
                    .ToList();

                return Ok(new { kategoriler, markalar, departmanlar, personeller });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Dropdown verileri alinamadi: {ex.Message}" });
            }
        }

        public class CreateAssetModel
        {
            public string Tanim { get; set; }
            public string SeriNo { get; set; }
            public string Aciklama { get; set; }
            public int? AygitKategoriID { get; set; }
            public int? MarkaID { get; set; }
            public int? Miktar { get; set; }
            public string SorumluDepKod { get; set; }
            public string DemirbasKodu { get; set; }
            public string Konum { get; set; }
            public string MasrafMerkezi { get; set; }
        }

        [HttpPost("create")]
        public IActionResult CreateAsset([FromBody] CreateAssetModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (string.IsNullOrEmpty(model.Tanim))
                {
                    return BadRequest(new { message = "Demirbas tanimi bos olamaz." });
                }

                if (!string.IsNullOrEmpty(model.DemirbasKodu))
                {
                    var exists = _context.tb_Aygit.Any(a => a.DemirbasKodu == model.DemirbasKodu);
                    if (exists)
                    {
                        return BadRequest(new { message = $"'{model.DemirbasKodu}' barkodlu demirbas zaten kayitli." });
                    }
                }

                var asset = new tb_Aygit
                {
                    Tanim = model.Tanim,
                    SeriNo = model.SeriNo,
                    Aciklama = model.Aciklama,
                    AygitKategoriID = model.AygitKategoriID,
                    MarkaID = model.MarkaID,
                    Miktar = model.Miktar ?? 1,
                    SorumluDepKod = model.SorumluDepKod,
                    DemirbasKodu = model.DemirbasKodu ?? "",
                    Konum = model.Konum,
                    MasrafMerkezi = model.MasrafMerkezi,
                    AktifAygit = true,
                    BarkodOnay = false,
                    Durum = true, // Bosta (Zimmetlenebilir)
                    HataBildir = false,
                    ZimmetliSicil = "",
                    KullanimSekli = "GENEL",
                    KayitTar = DateTime.Now
                };

                _context.tb_Aygit.Add(asset);
                _context.SaveChanges();

                // Demirbas kodu bos ise sistem uretir (DMB-YYYYAA-ID) — kullanici
                // formdan girmiyor; kod otomatik atanir.
                if (string.IsNullOrWhiteSpace(asset.DemirbasKodu))
                {
                    asset.DemirbasKodu = $"DMB-{DateTime.Now.Year}{DateTime.Now.Month:00}-{asset.AygitID}";
                    _context.SaveChanges();
                }

                return Ok(new { success = true, message = "Demirbas basariyla kaydedildi.", aygitID = asset.AygitID, demirbasKodu = asset.DemirbasKodu });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Demirbas kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        [HttpPost("update/{id}")]
        public IActionResult UpdateAsset(int id, [FromBody] CreateAssetModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                if (string.IsNullOrEmpty(model.Tanim))
                {
                    return BadRequest(new { message = "Demirbas tanimi bos olamaz." });
                }

                var asset = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == id);
                if (asset == null) return NotFound(new { message = "Demirbas bulunamadi." });

                if (!string.IsNullOrEmpty(model.DemirbasKodu) && model.DemirbasKodu != asset.DemirbasKodu)
                {
                    var exists = _context.tb_Aygit.Any(a => a.DemirbasKodu == model.DemirbasKodu && a.AygitID != id);
                    if (exists)
                    {
                        return BadRequest(new { message = $"'{model.DemirbasKodu}' barkodlu demirbas zaten kayitli." });
                    }
                }

                asset.Tanim = model.Tanim;
                asset.SeriNo = model.SeriNo;
                asset.Aciklama = model.Aciklama;
                asset.AygitKategoriID = model.AygitKategoriID;
                asset.MarkaID = model.MarkaID;
                asset.Miktar = model.Miktar ?? 1;
                asset.SorumluDepKod = model.SorumluDepKod;
                asset.DemirbasKodu = model.DemirbasKodu;
                asset.Konum = model.Konum;
                asset.MasrafMerkezi = model.MasrafMerkezi;

                _context.SaveChanges();
                return Ok(new { success = true, message = "Demirbas basariyla güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Demirbas güncellenirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("sayim-list")]
        public IActionResult GetSayimList([FromQuery] string search = "", [FromQuery] string categoryId = "", [FromQuery] string brandId = "")
        {
            try
            {
                var currentUser = GetCurrentUser();
                var query = _context.viewSayimAygit.AsNoTracking().Where(s => s.SicilNo == currentUser.SicilNo);

                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower().Replace(" ", "");
                    query = query.Where(o => 
                        (o.Tanim != null && o.Tanim.ToLower().Contains(searchLower)) ||
                        (o.SeriNo != null && o.SeriNo.ToLower().Contains(searchLower)) ||
                        (o.DemirbasKodu != null && o.DemirbasKodu.ToLower().Contains(searchLower))
                    );
                }

                if (!string.IsNullOrEmpty(categoryId) && categoryId != "0")
                {
                    if (int.TryParse(categoryId, out int catId))
                        query = query.Where(o => o.AygitKategoriID == catId || o.UstKatID == catId);
                }

                if (!string.IsNullOrEmpty(brandId) && brandId != "0")
                {
                    if (int.TryParse(brandId, out int bId))
                        query = query.Where(o => o.MarkaID == bId);
                }

                var list = query.OrderByDescending(o => o.IslemTar).ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayim listesi alinamadi: {ex.Message}" });
            }
        }

        public class AddSayimModel
        {
            public string Code { get; set; }
        }

        [HttpPost("sayim-add")]
        public IActionResult AddSayim([FromBody] AddSayimModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (string.IsNullOrEmpty(model.Code))
                {
                    return BadRequest(new { message = "Lütfen barkod veya demirbas kodu giriniz." });
                }

                var asset = _context.tb_Aygit.FirstOrDefault(a => 
                    a.DemirbasKodu == model.Code || 
                    a.SeriNo == model.Code || 
                    a.AygitID.ToString() == model.Code
                );

                if (asset == null)
                {
                    return NotFound(new { message = $"'{model.Code}' kodlu demirbas bulunamadi." });
                }

                var exists = _context.tb_SayimAygit.Any(s => s.AygitID == asset.AygitID);
                if (exists)
                {
                    // Aynı demirbaş 2. kez okutuldu — frontend "listeden çıkarılsın mı?"
                    // diye sorar; evet ise removeSayim çağrılır.
                    return Ok(new { success = true, alreadyExists = true, aygitID = asset.AygitID, tanim = asset.Tanim, message = "Bu demirbaş sayım listesinde zaten var." });
                }

                var sayim = new tb_SayimAygit
                {
                    AygitID = asset.AygitID,
                    SicilNo = currentUser.SicilNo,
                    IslemTar = DateTime.Now
                };
                _context.tb_SayimAygit.Add(sayim);
                _context.SaveChanges();

                return Ok(new { success = true, alreadyExists = false, message = "Demirbas sayima eklendi.", aygitID = asset.AygitID });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayima eklenirken hata olustu: {ex.Message}" });
            }
        }

        public class RemoveSayimModel
        {
            public int AygitId { get; set; }
        }

        [HttpPost("sayim-remove")]
        public IActionResult RemoveSayim([FromBody] RemoveSayimModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var sayim = _context.tb_SayimAygit.FirstOrDefault(s => s.AygitID == model.AygitId && s.SicilNo == currentUser.SicilNo);
                if (sayim != null)
                {
                    _context.tb_SayimAygit.Remove(sayim);
                    _context.SaveChanges();
                    return Ok(new { success = true, message = "Demirbas sayimdan çıkarildi." });
                }
                return NotFound(new { message = "Sayilan demirbas kaydi bulunamadi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayimdan çıkarilirken hata olustu: {ex.Message}" });
            }
        }

        // Dosya yukleme icin ayri bir uc nokta gerekmiyor — hurda kaniti ve bakim/tamir ekleri
        // mevcut genel /talep/upload-file uc noktasini (TalepController.UploadFile) module=ZIMMET
        // ile kullanir (bkz. TenantService.GetModulPath "ZIMMET" -> "Zimmet/Docs"; mobil tarafta
        // pickAndUploadFile('ZIMMET', source) ile cagrilir).

        // ====================================================================
        // PERSONEL DEMIRBAS GECMISI
        // ====================================================================

        [HttpGet("person-history/{sicilNo}")]
        public IActionResult GetPersonAssetHistory(string sicilNo)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var history = (from ap in _context.tb_AygitPersonel
                               join a in _context.tb_Aygit on ap.AygitID equals a.AygitID
                               where ap.PersonelSicil == sicilNo
                               orderby ap.TeslimEtTar descending
                               select new
                               {
                                   ap.AygitPersonelID,
                                   ap.AygitID,
                                   a.Tanim,
                                   a.DemirbasKodu,
                                   AygitKategoriID = a.AygitKategoriID,
                                   a.HurdaDurum,
                                   a.AktifAygit,
                                   TeslimEtTar = ap.TeslimEtTar,
                                   TeslimAlTar = ap.TeslimAlTar,
                                   ap.Aciklama
                               }).ToList();

                var kategoriler = _context.tb_AygitKategori.AsNoTracking().ToDictionary(k => k.AygitKategoriID, k => k.Tanim);

                var result = history.Select(h =>
                {
                    DateTime? bitis = h.TeslimAlTar;
                    TimeSpan kullanimSuresi = (bitis ?? DateTime.Now) - (h.TeslimEtTar ?? DateTime.Now);
                    string satirDurumu = h.HurdaDurum == true ? "Demirbas Hurdaya Ayrilmis"
                        : (h.TeslimAlTar == null ? "Hala Kullanimda" : "Iade Edildi");

                    return new
                    {
                        h.AygitPersonelID,
                        h.AygitID,
                        h.Tanim,
                        h.DemirbasKodu,
                        Kategori = kategoriler.TryGetValue(h.AygitKategoriID ?? 0, out var kat) ? kat : "",
                        TeslimEtTarStr = h.TeslimEtTar?.ToString("dd.MM.yyyy HH:mm") ?? "",
                        TeslimAlTarStr = h.TeslimAlTar?.ToString("dd.MM.yyyy HH:mm") ?? "",
                        KullanimGunSayisi = (int)kullanimSuresi.TotalDays,
                        SatirDurumu = satirDurumu,
                        h.Aciklama
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Personel demirbas gecmisi alinirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // ADMIN: HURDA ONAYLAYICILARI + BAKIM TURLERI
        // ====================================================================

        private bool IsYonetici(tb_Kullanici user) => user.Yonetici == true || user.KullaniciAdi == "admin";

        [HttpGet("hurda-onaylayicilar")]
        public IActionResult GetHurdaOnaylayicilar()
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsYonetici(currentUser)) return Forbid();

                var list = (from o in _context.tb_DemirbasHurdaOnaylayici.AsNoTracking()
                            where o.AktifMi
                            join p in _context.tb_Personel.AsNoTracking() on o.SicilNo equals p.SicilNo into ps
                            from p in ps.DefaultIfEmpty()
                            select new { o.OnaylayiciID, o.SicilNo, AdSoyad = p != null ? p.AdSoyad : o.SicilNo })
                            .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Onaylayici listesi alinirken hata olustu: {ex.Message}" });
            }
        }

        public class HurdaOnaylayiciKaydetModel
        {
            public List<string> SicilNolar { get; set; }
        }

        [HttpPost("hurda-onaylayicilar")]
        public IActionResult SaveHurdaOnaylayicilar([FromBody] HurdaOnaylayiciKaydetModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsYonetici(currentUser)) return Forbid();

                var sicilNolar = (model?.SicilNolar ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                if (sicilNolar.Count > 3)
                {
                    return BadRequest(new { message = "En fazla 3 onaylayici secilebilir." });
                }

                // Mevcut aktif listeyi pasiflestir, yeni seciimi ekle — basit "replace" deseni.
                var mevcut = _context.tb_DemirbasHurdaOnaylayici.Where(o => o.AktifMi).ToList();
                foreach (var m in mevcut) m.AktifMi = false;

                foreach (var sicil in sicilNolar)
                {
                    _context.tb_DemirbasHurdaOnaylayici.Add(new tb_DemirbasHurdaOnaylayici
                    {
                        SicilNo = sicil,
                        AktifMi = true,
                        EklenmeTarihi = DateTime.Now
                    });
                }

                _context.SaveChanges();
                return Ok(new { success = true, message = "Hurda onaylayicilari kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Onaylayicilar kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("bakim-turleri")]
        public IActionResult GetBakimTurleri()
        {
            try
            {
                var list = _context.tb_AygitBakimTuru.AsNoTracking()
                    .Where(t => t.AktifMi)
                    .OrderBy(t => t.Sira ?? int.MaxValue).ThenBy(t => t.Tanim)
                    .Select(t => new { t.TuruID, t.Tanim })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Bakim turleri alinirken hata olustu: {ex.Message}" });
            }
        }

        public class BakimTuruKaydetModel
        {
            public string Tanim { get; set; }
        }

        [HttpPost("bakim-turleri")]
        public IActionResult SaveBakimTuru([FromBody] BakimTuruKaydetModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsYonetici(currentUser)) return Forbid();
                if (string.IsNullOrWhiteSpace(model?.Tanim)) return BadRequest(new { message = "Islem turu adi bos olamaz." });

                int maxSira = _context.tb_AygitBakimTuru.Select(t => (int?)t.Sira).Max() ?? 0;
                var turu = new tb_AygitBakimTuru { Tanim = model.Tanim.Trim(), AktifMi = true, Sira = maxSira + 1 };
                _context.tb_AygitBakimTuru.Add(turu);
                _context.SaveChanges();
                return Ok(new { success = true, turuID = turu.TuruID });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Islem turu kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        [HttpPost("bakim-turleri/{id}/pasiflestir")]
        public IActionResult DeactivateBakimTuru(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsYonetici(currentUser)) return Forbid();

                var turu = _context.tb_AygitBakimTuru.FirstOrDefault(t => t.TuruID == id);
                if (turu == null) return NotFound(new { message = "Islem turu bulunamadi." });
                turu.AktifMi = false;
                _context.SaveChanges();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Islem turu pasiflestirilirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // HURDA SURECI — COK ONAYLI, PARALEL KARAR
        // ====================================================================

        public class HurdaTalepModel
        {
            public string Sebep { get; set; }
            public List<string> DosyaUrls { get; set; }
        }

        [HttpPost("asset/{id}/hurda-talep")]
        public IActionResult CreateHurdaTalep(int id, [FromBody] HurdaTalepModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                if (string.IsNullOrWhiteSpace(model?.Sebep)) return BadRequest(new { message = "Hurdaya ayirma sebebi zorunludur." });
                var dosyaUrls = (model.DosyaUrls ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Take(3).ToList();

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == id);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });
                if (aygit.HurdaDurum == true) return BadRequest(new { message = "Bu demirbas zaten hurdaya ayrilmis." });

                var mevcutBekleyen = _context.tb_DemirbasHurdaTalep.Any(t => t.AygitID == id && t.Durum == "BEKLEMEDE");
                if (mevcutBekleyen) return BadRequest(new { message = "Bu demirbas icin zaten bekleyen bir hurda talebi var." });

                var onaylayicilar = _context.tb_DemirbasHurdaOnaylayici.Where(o => o.AktifMi).Select(o => o.SicilNo).ToList();
                if (onaylayicilar.Count == 0) return BadRequest(new { message = "Hurda onaylayicisi tanimli degil — once Admin > Demirbas Ayarlari'ndan onaylayici secilmeli." });

                var talep = new tb_DemirbasHurdaTalep
                {
                    AygitID = id,
                    TalepEdenSicil = currentUser.SicilNo,
                    Sebep = model.Sebep.Trim(),
                    TalepTarihi = DateTime.Now,
                    Durum = "BEKLEMEDE"
                };
                _context.tb_DemirbasHurdaTalep.Add(talep);
                _context.SaveChanges();

                foreach (var url in dosyaUrls)
                {
                    _context.tb_DemirbasHurdaDosya.Add(new tb_DemirbasHurdaDosya
                    {
                        HurdaTalepID = talep.HurdaTalepID,
                        DosyaUrl = url,
                        DosyaAdi = System.IO.Path.GetFileName(url)
                    });
                }

                // Snapshot — o anki aktif onaylayicilar bu talebe sabitlenir.
                foreach (var sicil in onaylayicilar)
                {
                    _context.tb_DemirbasHurdaOnay.Add(new tb_DemirbasHurdaOnay
                    {
                        HurdaTalepID = talep.HurdaTalepID,
                        OnaylayanSicil = sicil,
                        Durum = "BEKLEMEDE"
                    });
                }
                _context.SaveChanges();

                string demirbasAd = string.IsNullOrEmpty(aygit.DemirbasKodu) ? aygit.Tanim : $"{aygit.Tanim} ({aygit.DemirbasKodu})";
                foreach (var sicil in onaylayicilar)
                {
                    _ = _pushNotificationService.SendToUserBySicilNoAsync(
                        sicil,
                        "Hurda Onayi Bekleniyor",
                        $"{currentUser.AdSoyad} tarafindan '{demirbasAd}' icin hurda talebi acildi, onayiniz bekleniyor.",
                        new { type = "demirbasHurda", screen = "HurdaOnaylarimScreen", id = talep.HurdaTalepID }
                    );
                }

                return Ok(new { success = true, hurdaTalepID = talep.HurdaTalepID, message = "Hurda talebi olusturuldu, onaylayicilara bildirim gonderildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hurda talebi olusturulurken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("hurda-onaylarim")]
        public IActionResult GetHurdaOnaylarim()
        {
            try
            {
                var currentUser = GetCurrentUser();

                var list = (from onay in _context.tb_DemirbasHurdaOnay.AsNoTracking()
                            join talep in _context.tb_DemirbasHurdaTalep.AsNoTracking() on onay.HurdaTalepID equals talep.HurdaTalepID
                            join aygit in _context.tb_Aygit.AsNoTracking() on talep.AygitID equals aygit.AygitID
                            where onay.OnaylayanSicil == currentUser.SicilNo && onay.Durum == "BEKLEMEDE" && talep.Durum == "BEKLEMEDE"
                            orderby talep.TalepTarihi descending
                            select new
                            {
                                onay.OnayID,
                                talep.HurdaTalepID,
                                talep.AygitID,
                                aygit.Tanim,
                                aygit.DemirbasKodu,
                                talep.Sebep,
                                talep.TalepEdenSicil,
                                TalepEdenAdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == talep.TalepEdenSicil).AdSoyad,
                                talep.TalepTarihi
                            }).ToList();

                var talepIDs = list.Select(l => l.HurdaTalepID).Distinct().ToList();
                var dosyalar = _context.tb_DemirbasHurdaDosya.AsNoTracking()
                    .Where(d => talepIDs.Contains(d.HurdaTalepID))
                    .ToList()
                    .GroupBy(d => d.HurdaTalepID)
                    .ToDictionary(g => g.Key, g => g.Select(d => new { d.DosyaUrl, d.DosyaAdi }).ToList());

                var result = list.Select(l => new
                {
                    l.OnayID,
                    l.HurdaTalepID,
                    l.AygitID,
                    l.Tanim,
                    l.DemirbasKodu,
                    l.Sebep,
                    l.TalepEdenAdSoyad,
                    TalepTarihiStr = l.TalepTarihi.ToString("dd.MM.yyyy HH:mm"),
                    Dosyalar = dosyalar.TryGetValue(l.HurdaTalepID, out var dl) ? (object)dl : new List<object>()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Bekleyen hurda onaylari alinirken hata olustu: {ex.Message}" });
            }
        }

        public class HurdaKararModel
        {
            public bool Onay { get; set; }
            public string RedSebebi { get; set; }
        }

        [HttpPost("hurda-onay/{onayId}/karar")]
        public IActionResult DecideHurdaOnay(int onayId, [FromBody] HurdaKararModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();

                var onay = _context.tb_DemirbasHurdaOnay.FirstOrDefault(o => o.OnayID == onayId);
                if (onay == null) return NotFound(new { message = "Onay kaydi bulunamadi." });
                if (onay.OnaylayanSicil != currentUser.SicilNo) return Forbid();
                if (onay.Durum != "BEKLEMEDE") return BadRequest(new { message = "Bu onay zaten karara baglanmis." });

                var talep = _context.tb_DemirbasHurdaTalep.FirstOrDefault(t => t.HurdaTalepID == onay.HurdaTalepID);
                if (talep == null) return NotFound(new { message = "Hurda talebi bulunamadi." });
                if (talep.Durum != "BEKLEMEDE") return BadRequest(new { message = "Bu talep zaten sonuclanmis." });

                if (!model.Onay && string.IsNullOrWhiteSpace(model.RedSebebi))
                {
                    return BadRequest(new { message = "Red icin aciklama zorunludur." });
                }

                onay.Durum = model.Onay ? "ONAY" : "RET";
                onay.RedSebebi = model.Onay ? null : model.RedSebebi.Trim();
                onay.KararTarihi = DateTime.Now;
                _context.SaveChanges();

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == talep.AygitID);
                string demirbasAd = aygit != null ? (string.IsNullOrEmpty(aygit.DemirbasKodu) ? aygit.Tanim : $"{aygit.Tanim} ({aygit.DemirbasKodu})") : "";

                if (!model.Onay)
                {
                    talep.Durum = "REDDEDILDI";
                    talep.TamamlanmaTarihi = DateTime.Now;
                    _context.SaveChanges();

                    _ = _pushNotificationService.SendToUserBySicilNoAsync(
                        talep.TalepEdenSicil,
                        "Hurda Talebi Reddedildi",
                        $"'{demirbasAd}' icin hurda talebiniz {currentUser.AdSoyad} tarafindan reddedildi: {onay.RedSebebi}",
                        new { type = "demirbasHurda", screen = "DemirbasYonetimScreen", id = talep.AygitID }
                    );

                    return Ok(new { success = true, sonuc = "REDDEDILDI", message = "Talep reddedildi." });
                }

                bool hepsiOnayladi = !_context.tb_DemirbasHurdaOnay.Any(o => o.HurdaTalepID == talep.HurdaTalepID && o.Durum != "ONAY");
                if (hepsiOnayladi)
                {
                    talep.Durum = "ONAYLANDI";
                    talep.TamamlanmaTarihi = DateTime.Now;
                    if (aygit != null) aygit.HurdaDurum = true;
                    _context.SaveChanges();

                    _ = _pushNotificationService.SendToUserBySicilNoAsync(
                        talep.TalepEdenSicil,
                        "Hurda Talebi Onaylandi",
                        $"'{demirbasAd}' tum onaylayicilar tarafindan onaylandi ve hurdaya ayrildi.",
                        new { type = "demirbasHurda", screen = "DemirbasYonetimScreen", id = talep.AygitID }
                    );

                    return Ok(new { success = true, sonuc = "ONAYLANDI", message = "Tum onaylar tamamlandi, demirbas hurdaya ayrildi." });
                }

                return Ok(new { success = true, sonuc = "BEKLEMEDE", message = "Karariniz kaydedildi, diger onaylayicilar bekleniyor." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Karar kaydedilirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("asset/{id}/hurda-gecmisi")]
        public IActionResult GetHurdaGecmisi(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var talepler = _context.tb_DemirbasHurdaTalep.AsNoTracking()
                    .Where(t => t.AygitID == id)
                    .OrderByDescending(t => t.TalepTarihi)
                    .ToList();

                var talepIDs = talepler.Select(t => t.HurdaTalepID).ToList();
                var onaylar = _context.tb_DemirbasHurdaOnay.AsNoTracking()
                    .Where(o => talepIDs.Contains(o.HurdaTalepID)).ToList()
                    .GroupBy(o => o.HurdaTalepID)
                    .ToDictionary(g => g.Key, g => g.Select(o => new
                    {
                        o.OnaylayanSicil,
                        AdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == o.OnaylayanSicil).AdSoyad,
                        o.Durum,
                        o.RedSebebi
                    }).ToList());

                var result = talepler.Select(t => new
                {
                    t.HurdaTalepID,
                    t.Sebep,
                    TalepEdenAdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == t.TalepEdenSicil).AdSoyad,
                    TalepTarihiStr = t.TalepTarihi.ToString("dd.MM.yyyy HH:mm"),
                    t.Durum,
                    Onaylar = onaylar.TryGetValue(t.HurdaTalepID, out var ol) ? (object)ol : new List<object>()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Hurda gecmisi alinirken hata olustu: {ex.Message}" });
            }
        }

        // ====================================================================
        // BAKIM & TAMIR TAKIBI
        // ====================================================================

        public class BakimaGonderModel
        {
            public int TuruID { get; set; }
            public string Aciklama { get; set; }
            public string ServisFirma { get; set; }
            public List<string> DosyaUrls { get; set; }
        }

        [HttpPost("asset/{id}/bakima-gonder")]
        public IActionResult SendAssetToMaintenance(int id, [FromBody] BakimaGonderModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == id);
                if (aygit == null) return NotFound(new { message = "Demirbas bulunamadi." });
                if (aygit.HurdaDurum == true) return BadRequest(new { message = "Bu demirbas hurda durumundadir." });
                if (aygit.BakimDurumu == true) return BadRequest(new { message = "Bu demirbas zaten bakim/tamir surecinde." });

                var turu = _context.tb_AygitBakimTuru.AsNoTracking().FirstOrDefault(t => t.TuruID == model.TuruID);
                if (turu == null) return BadRequest(new { message = "Gecersiz islem turu." });

                // Zimmetliyse once iade alinir (hurda akisindaki gibi) — gecmis korunur. Kimde oldugu
                // (OncekiZimmetliSicil) hatirlanir ki tamamlaninca "ayni kullaniciya geri zimmetle"
                // secenegi sunulabilsin (ör. yillik bakim/format sonrasi notebook sahibine geri verilir).
                string oncekiZimmetliSicil = null;
                if (aygit.Durum == false)
                {
                    oncekiZimmetliSicil = aygit.ZimmetliSicil;
                    var activeHistory = _context.tb_AygitPersonel
                        .FirstOrDefault(ap => ap.AygitID == id && ap.PersonelSicil == oncekiZimmetliSicil && ap.TeslimAlTar == null);
                    if (activeHistory != null)
                    {
                        activeHistory.TeslimAlTar = DateTime.Now;
                        activeHistory.TeslimAlanSicil = currentUser.SicilNo;
                        activeHistory.Aciklama += " | Bakim/tamir icin iade alindi";
                    }
                    aygit.ZimmetliSicil = "";
                    aygit.Durum = true;
                }

                aygit.BakimDurumu = true;

                var bakim = new tb_AygitBakim
                {
                    AygitID = id,
                    TuruID = model.TuruID,
                    BaslangicTar = DateTime.Now,
                    Durum = "SERVISTE",
                    Aciklama = model.Aciklama,
                    ServisFirma = model.ServisFirma,
                    IslemYapanSicil = currentUser.SicilNo,
                    KayitTar = DateTime.Now,
                    OncekiZimmetliSicil = oncekiZimmetliSicil
                };
                _context.tb_AygitBakim.Add(bakim);
                _context.SaveChanges();

                foreach (var url in (model.DosyaUrls ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    _context.tb_AygitBakimDosya.Add(new tb_AygitBakimDosya
                    {
                        BakimID = bakim.BakimID,
                        DosyaUrl = url,
                        DosyaAdi = System.IO.Path.GetFileName(url)
                    });
                }
                _context.SaveChanges();

                return Ok(new { success = true, bakimID = bakim.BakimID, message = $"Demirbas '{turu.Tanim}' icin servise gonderildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Bakima gonderilirken hata olustu: {ex.Message}" });
            }
        }

        public class BakimTamamlaModel
        {
            public string SonucAciklama { get; set; }
            public decimal? Maliyet { get; set; }
            public bool GeriZimmetle { get; set; } // true ise OncekiZimmetliSicil'e otomatik tekrar zimmetlenir
        }

        [HttpPost("bakim/{id}/tamamla")]
        public IActionResult CompleteMaintenance(int id, [FromBody] BakimTamamlaModel model)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var bakim = _context.tb_AygitBakim.FirstOrDefault(b => b.BakimID == id);
                if (bakim == null) return NotFound(new { message = "Bakim kaydi bulunamadi." });
                if (bakim.Durum != "SERVISTE") return BadRequest(new { message = "Bu bakim kaydi zaten sonuclanmis." });

                bakim.Durum = "TAMAMLANDI";
                bakim.BitisTar = DateTime.Now;
                bakim.SonucAciklama = model.SonucAciklama;
                bakim.Maliyet = model.Maliyet;

                var aygit = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == bakim.AygitID);
                if (aygit != null) aygit.BakimDurumu = false;

                string mesaj = "Bakim/tamir tamamlandi, demirbas tekrar kullanilabilir.";

                // Ayni kullaniciya geri zimmetle — IT'nin cihazi alip (yillik bakim/format vb.) SAHIBINE
                // geri verdigi, "boşta" havuzuna dusmeden dogrudan onceki kullaniciya donmesi gereken
                // en yaygin senaryo. AssignAsset ile AYNI desen (gecmis satiri + push).
                if (model.GeriZimmetle && aygit != null && !string.IsNullOrEmpty(bakim.OncekiZimmetliSicil))
                {
                    var targetPersonel = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == bakim.OncekiZimmetliSicil);
                    if (targetPersonel != null)
                    {
                        aygit.ZimmetliSicil = bakim.OncekiZimmetliSicil;
                        aygit.Durum = false;

                        var apLog = new tb_AygitPersonel
                        {
                            AygitID = aygit.AygitID,
                            PersonelSicil = bakim.OncekiZimmetliSicil,
                            PersonelAdSoyad = !string.IsNullOrEmpty(targetPersonel.AdSoyad) && targetPersonel.AdSoyad.Length > 100
                                ? targetPersonel.AdSoyad.Substring(0, 100)
                                : (targetPersonel.AdSoyad ?? ""),
                            TeslimEtTar = DateTime.Now,
                            TeslimEdenSicil = currentUser.SicilNo,
                            Aciklama = "Bakim/tamir sonrasi ayni kullaniciya geri zimmetlendi."
                        };
                        _context.tb_AygitPersonel.Add(apLog);
                        _context.SaveChanges();

                        _ = _pushNotificationService.NotifyAssetAssignedAsync(apLog.AygitPersonelID);
                        mesaj = $"Bakim/tamir tamamlandi, demirbas {targetPersonel.AdSoyad} kullanicisina geri zimmetlendi.";
                    }
                }

                _context.SaveChanges();
                return Ok(new { success = true, message = mesaj });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Bakim tamamlanirken hata olustu: {ex.Message}" });
            }
        }

        [HttpGet("asset/{id}/bakim-gecmisi")]
        public IActionResult GetMaintenanceHistory(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (!IsZimmetManager(currentUser)) return Forbid();

                var turler = _context.tb_AygitBakimTuru.AsNoTracking().ToDictionary(t => t.TuruID, t => t.Tanim);

                var bakimlar = _context.tb_AygitBakim.AsNoTracking()
                    .Where(b => b.AygitID == id)
                    .OrderByDescending(b => b.BaslangicTar)
                    .ToList();

                var bakimIDs = bakimlar.Select(b => b.BakimID).ToList();
                var dosyalar = _context.tb_AygitBakimDosya.AsNoTracking()
                    .Where(d => bakimIDs.Contains(d.BakimID)).ToList()
                    .GroupBy(d => d.BakimID)
                    .ToDictionary(g => g.Key, g => g.Select(d => new { d.DosyaUrl, d.DosyaAdi }).ToList());

                var result = bakimlar.Select(b => new
                {
                    b.BakimID,
                    IslemTuru = turler.TryGetValue(b.TuruID, out var tn) ? tn : "",
                    BaslangicTarStr = b.BaslangicTar.ToString("dd.MM.yyyy HH:mm"),
                    BitisTarStr = b.BitisTar?.ToString("dd.MM.yyyy HH:mm") ?? "",
                    b.Durum,
                    b.Aciklama,
                    b.ServisFirma,
                    b.Maliyet,
                    b.SonucAciklama,
                    IslemYapanAdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == b.IslemYapanSicil).AdSoyad,
                    b.OncekiZimmetliSicil,
                    OncekiZimmetliAdSoyad = string.IsNullOrEmpty(b.OncekiZimmetliSicil) ? null : _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == b.OncekiZimmetliSicil).AdSoyad,
                    Dosyalar = dosyalar.TryGetValue(b.BakimID, out var dl) ? (object)dl : new List<object>()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Bakim gecmisi alinirken hata olustu: {ex.Message}" });
            }
        }
    }
}
