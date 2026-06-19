using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPortalSpace.DataLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.BusinessLayer.Interfaces;

namespace WebPortalSpace.Backend.Controllers
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
        // KULLANICI İŞLEMLERİ (MY DEBITS)
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
                return BadRequest(new { message = $"Zimmetlerim listesi alınırken hata oluştu: {ex.Message}" });
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
                if (aygit == null) return NotFound(new { message = "Demirbaş bulunamadı." });

                if (aygit.ZimmetliSicil != user.SicilNo)
                {
                    return BadRequest(new { message = "Sadece kendi üzerinize zimmetli demirbaşlar için hata bildirebilirsiniz." });
                }

                aygit.HataBildir = true;
                
                // Add to log
                _context.tb_Log.Add(new tb_Log
                {
                    Eposta = user.Eposta,
                    SicilNo = user.SicilNo,
                    Konu = $"DEMIRBAS_HATA - AygitID: {model.AygitId}",
                    Aciklama = $"Zimmetli personel hata/itiraz bildirdi: {model.Aciklama}",
                    KayitTar = DateTime.Now
                });

                _context.SaveChanges();
                return Ok(new { success = true, message = "Hata/İtiraz bildirimi başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"İtiraz kaydedilirken hata oluştu: {ex.Message}" });
            }
        }

        // ====================================================================
        // YÖNETİCİ/ZİMMET SORUMLUSU İŞLEMLERİ (DEMİRBAŞ YÖNETİMİ)
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

                if (status == "1") // Boşta (Zimmetlenebilir)
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
                return BadRequest(new { message = $"Demirbaş listesi alınırken hata oluştu: {ex.Message}" });
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
                if (aygit == null) return NotFound(new { message = "Demirbaş bulunamadı." });

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
                return BadRequest(new { message = $"Detay bilgisi alınırken hata oluştu: {ex.Message}" });
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
                                   TeslimEden = te != null ? te.AdSoyad : (ap.TeslimEdenSicil ?? "Belirtilmemiş"),
                                   ap.Aciklama,
                                   TeslimAlan = ta != null ? ta.AdSoyad : (ap.TeslimAlanSicil ?? ""),
                                   TeslimAlTarStr = ap.TeslimAlTar != null ? ap.TeslimAlTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                   KullanimSekli = ""
                               }).ToList();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Zimmet geçmişi alınırken hata oluştu: {ex.Message}" });
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
                if (aygit == null) return NotFound(new { message = "Demirbaş bulunamadı." });
                if (aygit.HurdaDurum == true) return BadRequest(new { message = "Bu demirbaş hurda durumundadır, zimmetlenemez." });
                if (aygit.Durum == false) return BadRequest(new { message = "Bu demirbaş zaten başka bir personele zimmetlidir." });

                var targetPersonel = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == model.SicilNo);
                if (targetPersonel == null) return BadRequest(new { message = "Zimmetlenecek personel bulunamadı." });

                // Update Asset
                aygit.ZimmetliSicil = model.SicilNo;
                aygit.Durum = false; // Mapped to false meaning Debited (Zimmetli)
                
                // KullanimSekli is limited to Max 5 chars in DB
                string kullanimSekli = !string.IsNullOrEmpty(model.KullanimSekli) ? model.KullanimSekli.Trim() : "ŞAHSİ";
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
                return Ok(new { success = true, message = $"Zimmet başarıyla atandı ({targetPersonel.AdSoyad})." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AssignAsset error: {ex}");
                return BadRequest(new { message = $"Zimmet atanamadı: {ex.Message} | Inner: {ex.InnerException?.Message}" });
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
                if (aygit == null) return NotFound(new { message = "Demirbaş bulunamadı." });
                if (aygit.Durum == true) return BadRequest(new { message = "Bu demirbaş zaten boşta." });

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
                        activeHistory.Aciklama += $" | İade Notu: {model.Aciklama}";
                    }
                }

                // Update Asset to free
                aygit.ZimmetliSicil = "";
                aygit.Durum = true; // Boşta (Zimmetlenebilir)
                aygit.HataBildir = false;

                _context.SaveChanges();
                if (activeHistory != null)
                {
                    _ = _pushNotificationService.NotifyAssetReturnedAsync(activeHistory.AygitPersonelID, currentUser.KullaniciID);
                }
                return Ok(new { success = true, message = "Zimmet başarıyla iade alındı ve boşa çıkarıldı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Zimmet iade alınamadı: {ex.Message}" });
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
                if (aygit == null) return NotFound(new { message = "Demirbaş bulunamadı." });

                aygit.BarkodOnay = true;
                _context.SaveChanges();

                return Ok(new { success = true, message = "Barkod onaylandı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Barkod onaylanırken hata oluştu: {ex.Message}" });
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
                return BadRequest(new { message = $"Dropdown verileri alınamadı: {ex.Message}" });
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
                    return BadRequest(new { message = "Demirbaş tanımı boş olamaz." });
                }

                if (!string.IsNullOrEmpty(model.DemirbasKodu))
                {
                    var exists = _context.tb_Aygit.Any(a => a.DemirbasKodu == model.DemirbasKodu);
                    if (exists)
                    {
                        return BadRequest(new { message = $"'{model.DemirbasKodu}' barkodlu demirbaş zaten kayıtlı." });
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
                    DemirbasKodu = model.DemirbasKodu,
                    Konum = model.Konum,
                    MasrafMerkezi = model.MasrafMerkezi,
                    AktifAygit = true,
                    BarkodOnay = false,
                    Durum = true, // Boşta (Zimmetlenebilir)
                    HataBildir = false,
                    ZimmetliSicil = "",
                    KullanimSekli = "GENEL",
                    KayitTar = DateTime.Now
                };

                _context.tb_Aygit.Add(asset);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Demirbaş başarıyla kaydedildi.", aygitID = asset.AygitID });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Demirbaş kaydedilirken hata oluştu: {ex.Message}" });
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
                    return BadRequest(new { message = "Demirbaş tanımı boş olamaz." });
                }

                var asset = _context.tb_Aygit.FirstOrDefault(a => a.AygitID == id);
                if (asset == null) return NotFound(new { message = "Demirbaş bulunamadı." });

                if (!string.IsNullOrEmpty(model.DemirbasKodu) && model.DemirbasKodu != asset.DemirbasKodu)
                {
                    var exists = _context.tb_Aygit.Any(a => a.DemirbasKodu == model.DemirbasKodu && a.AygitID != id);
                    if (exists)
                    {
                        return BadRequest(new { message = $"'{model.DemirbasKodu}' barkodlu demirbaş zaten kayıtlı." });
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
                return Ok(new { success = true, message = "Demirbaş başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Demirbaş güncellenirken hata oluştu: {ex.Message}" });
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
                return BadRequest(new { message = $"Sayım listesi alınamadı: {ex.Message}" });
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
                    return BadRequest(new { message = "Lütfen barkod veya demirbaş kodu giriniz." });
                }

                var asset = _context.tb_Aygit.FirstOrDefault(a => 
                    a.DemirbasKodu == model.Code || 
                    a.SeriNo == model.Code || 
                    a.AygitID.ToString() == model.Code
                );

                if (asset == null)
                {
                    return NotFound(new { message = $"'{model.Code}' kodlu demirbaş bulunamadı." });
                }

                var exists = _context.tb_SayimAygit.Any(s => s.AygitID == asset.AygitID);
                if (!exists)
                {
                    var sayim = new tb_SayimAygit
                    {
                        AygitID = asset.AygitID,
                        SicilNo = currentUser.SicilNo,
                        IslemTar = DateTime.Now
                    };
                    _context.tb_SayimAygit.Add(sayim);
                    _context.SaveChanges();
                }

                return Ok(new { success = true, message = "Demirbaş sayıma eklendi.", aygitID = asset.AygitID });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayıma eklenirken hata oluştu: {ex.Message}" });
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
                    return Ok(new { success = true, message = "Demirbaş sayımdan çıkarıldı." });
                }
                return NotFound(new { message = "Sayılan demirbaş kaydı bulunamadı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Sayımdan çıkarılırken hata oluştu: {ex.Message}" });
            }
        }
    }
}
