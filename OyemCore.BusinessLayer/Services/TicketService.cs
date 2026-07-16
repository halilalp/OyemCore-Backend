using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class TicketService : ITicketService
    {
        private readonly IYbsDbContext _context;
        private readonly ILogger<TicketService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IPushNotificationService _pushNotificationService;

        public TicketService(IYbsDbContext context, ILogger<TicketService> logger, IConfiguration configuration, IPushNotificationService pushNotificationService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _pushNotificationService = pushNotificationService;
        }

        private bool HasTicketAuthority(string adminBelgeTur)
        {
            if (string.IsNullOrEmpty(adminBelgeTur)) return false;
            var tokens = adminBelgeTur.Split('*', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpper());
            return tokens.Contains("ADMIN") || tokens.Contains("TICKET");
        }

        public (bool Success, object Data, string Message) InitConfig(int kullaniciID)
        {
            var usr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (usr == null) return (false, null, "Kullanici bulunamadi.");

            var per = _context.tb_Personel
                .AsNoTracking()
                .FirstOrDefault(p => p.SicilNo == usr.SicilNo);

            bool isAdmin = HasTicketAuthority(usr.AdminBelgeTur);

            return (true, new
            {
                IsAdmin = isAdmin,
                SirketKodu = per != null ? per.SirketKodu : "0",
                AdSoyad = usr.AdSoyad
            }, "Init config successful");
        }

        public (IEnumerable<object> Tickets, Dictionary<string, int> Counts) GetTicketList(int kullaniciID, string sirketKodu, string aramaText, int pageIndex, int pageSize)
        {
            var currentUsr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (currentUsr == null) return (Enumerable.Empty<object>(), new Dictionary<string, int>());

            var per = _context.tb_Personel
                .AsNoTracking()
                .FirstOrDefault(p => p.SicilNo == currentUsr.SicilNo);

            bool isAdmin = HasTicketAuthority(currentUsr.AdminBelgeTur);

            var query = from t in _context.tb_Ticket
                        join p1 in _context.tb_Personel on t.KayitSicilNo equals p1.SicilNo into p1s
                        from p1 in p1s.DefaultIfEmpty()
                        join p2 in _context.tb_Personel on t.SorumluSicilNo equals p2.SicilNo into p2s
                        from p2 in p2s.DefaultIfEmpty()
                        join tk in _context.tb_TicketKategori on t.KategoriID equals tk.ID into tks
                        from tk in tks.DefaultIfEmpty()
                        select new
                        {
                            t.ID,
                            t.TakipKodu,
                            t.SirketKodu,
                            t.Baslik,
                            t.Aciklama,
                            t.IslemTuru,
                            t.SurecDurumu,
                            t.Oncelik,
                            t.BitisTarihi,
                            t.KayitTarihi,
                            t.KayitSicilNo,
                            KayitPersonelAd = p1 != null ? p1.AdSoyad : null,
                            t.SorumluSicilNo,
                            SorumluPersonelAd = p2 != null ? p2.AdSoyad : null,
                            t.KategoriID,
                            KategoriAd = tk != null ? tk.Tanim : null,
                            t.Sira
                        };

            // Filters
            if (!isAdmin)
            {
                string userSirket = per?.SirketKodu ?? "0";
                query = query.Where(x => x.SirketKodu == userSirket || x.KayitSicilNo == currentUsr.SicilNo);
            }
            else if (!string.IsNullOrEmpty(sirketKodu))
            {
                query = query.Where(x => x.SirketKodu == sirketKodu);
            }

            if (!string.IsNullOrEmpty(aramaText))
            {
                string searchLower = aramaText.ToLower();
                query = query.Where(x => x.Baslik.ToLower().Contains(searchLower) || x.TakipKodu.ToLower().Contains(searchLower));
            }

            string[] statuses = { "HAVUZ", "ISLEM", "TEST", "TAMAM" };
            var allTickets = new List<object>();

            // Convert to list for counts grouping
            var filteredQueryList = query.ToList();

            foreach (var status in statuses)
            {
                var tickets = filteredQueryList
                    .Where(x => x.SurecDurumu == status)
                    .OrderBy(x => x.Sira ?? 9999)
                    .ThenByDescending(x => x.KayitTarihi)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(o => new
                    {
                        o.ID,
                        o.TakipKodu,
                        o.SirketKodu,
                        o.Baslik,
                        o.Aciklama,
                        o.IslemTuru,
                        o.SurecDurumu,
                        o.Oncelik,
                        BitisTarihiStr = o.BitisTarihi != null ? o.BitisTarihi.Value.ToString("dd.MM.yyyy") : "",
                        KayitTarihiStr = o.KayitTarihi != null ? o.KayitTarihi.Value.ToString("dd.MM.yyyy HH:mm") : "",
                        o.KayitSicilNo,
                        KayitYapan = o.KayitPersonelAd ?? o.KayitSicilNo,
                        o.SorumluSicilNo,
                        SorumluAd = o.SorumluPersonelAd ?? "Atanmamis",
                        IsMine = o.SorumluSicilNo == currentUsr.SicilNo,
                        DosyaSayisi = _context.tb_TicketDosya.Count(d => d.TicketID == o.ID),
                        YorumSayisi = _context.tb_TicketAciklama.Count(c => c.TicketID == o.ID),
                        o.KategoriID,
                        KategoriAd = o.KategoriAd ?? ""
                    }).ToList();

                allTickets.AddRange(tickets);
            }

            // Counts grouping
            var counts = filteredQueryList
                .GroupBy(x => x.SurecDurumu)
                .ToDictionary(g => g.Key ?? "", g => g.Count());

            return (allTickets, counts);
        }

        public string SaveTicket(int kullaniciID, tb_Ticket ticket)
        {
            var usr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (usr == null) throw new InvalidOperationException("Kullanici bulunamadi.");

            // Ticket yetkisi olmayan kullanici yalnizca kendi sirketi adina kayit acabilir.
            // Guvenlik: sirket kodu istemciden gelen degere degil, kullanicinin kendi sirketine sabitlenir.
            if (!HasTicketAuthority(usr.AdminBelgeTur))
            {
                var ownSirket = _context.tb_Personel.AsNoTracking()
                    .Where(p => p.SicilNo == usr.SicilNo)
                    .Select(p => p.SirketKodu)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(ownSirket))
                {
                    ticket.SirketKodu = ownSirket;
                }
            }

            if (ticket.ID == 0)
            {
                ticket.KayitTarihi = DateTime.Now;
                ticket.KayitSicilNo = usr.SicilNo;
                ticket.SurecDurumu = string.IsNullOrEmpty(ticket.SurecDurumu) ? "HAVUZ" : ticket.SurecDurumu;

                int initialCount = _context.tb_Ticket.Count();
                ticket.TakipKodu = $"TKT-{DateTime.Now:yyyyMMdd}-{initialCount + 1:000}";

                _context.tb_Ticket.Add(ticket);
                _context.SaveChanges(); // ID generated here

                // Update unique sirket index code
                int sirketAdet = _context.tb_Ticket.Count(t => t.SirketKodu == ticket.SirketKodu);
                string code = $"{ticket.SirketKodu}-{sirketAdet}";

                while (_context.tb_Ticket.Any(t => t.TakipKodu == code))
                {
                    sirketAdet++;
                    code = $"{ticket.SirketKodu}-{sirketAdet}";
                }

                ticket.TakipKodu = code;

                int maxSira = _context.tb_Ticket
                    .Where(t => t.SurecDurumu == ticket.SurecDurumu)
                    .Max(t => (int?)t.Sira) ?? 0;

                ticket.Sira = maxSira + 1;

                _context.SaveChanges();

                BelgeTarihceKaydet(ticket.TakipKodu, "Ticket Olusturuldu", $"Yeni kayit a?ildi. (Yapan: {usr.AdSoyad})");
                _ = _pushNotificationService.NotifyNewTicketAsync(ticket.ID);
            }
            else
            {
                var existing = _context.tb_Ticket.FirstOrDefault(t => t.ID == ticket.ID);
                if (existing == null) throw new InvalidOperationException("G?ncellenecek ticket bulunamadi.");

                string eskiDurum = existing.SurecDurumu;

                existing.SirketKodu = ticket.SirketKodu;
                existing.Baslik = ticket.Baslik;
                existing.Aciklama = ticket.Aciklama;
                existing.IslemTuru = ticket.IslemTuru;
                existing.SurecDurumu = ticket.SurecDurumu;
                existing.Oncelik = ticket.Oncelik;
                existing.BitisTarihi = ticket.BitisTarihi;
                existing.SorumluSicilNo = ticket.SorumluSicilNo;
                existing.KategoriID = ticket.KategoriID;

                _context.SaveChanges();

                if (eskiDurum != ticket.SurecDurumu)
                {
                    BelgeTarihceKaydet(existing.TakipKodu, "Durum G?ncellendi", $"Durum '{ticket.SurecDurumu}' olarak g?ncellendi. (Yapan: {usr.AdSoyad})");
                }
            }

            return ticket.ID.ToString();
        }

        public bool UpdateTicketStatus(int kullaniciID, int ticketID, string yeniDurum, int? draggedID)
        {
            var usr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (usr == null) return false;

            var t = _context.tb_Ticket.FirstOrDefault(x => x.ID == ticketID);

            if (t != null && t.SurecDurumu != yeniDurum)
            {
                int maxSira = _context.tb_Ticket
                    .Where(x => x.SurecDurumu == yeniDurum)
                    .Max(x => (int?)x.Sira) ?? 0;

                t.SurecDurumu = yeniDurum;
                t.Sira = maxSira + 1;
                _context.SaveChanges();

                BelgeTarihceKaydet(t.TakipKodu, "S?re? Degisikligi", $"'{yeniDurum}' asamasina ge?ildi. (Yapan: {usr.AdSoyad})");
                _ = _pushNotificationService.NotifyTicketStatusChangedAsync(t.ID, "", yeniDurum, usr.KullaniciID);

                return true;
            }
            return false;
        }

        public bool AssignTicket(int kullaniciID, int ticketID, string sicilNo)
        {
            var usr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (usr == null) return false;

            var t = _context.tb_Ticket.FirstOrDefault(x => x.ID == ticketID);

            if (t != null)
            {
                t.SorumluSicilNo = sicilNo;
                _context.SaveChanges();

                string adSoyad = _context.tb_Personel
                    .AsNoTracking()
                    .FirstOrDefault(p => p.SicilNo == sicilNo)?.AdSoyad ?? sicilNo;

                BelgeTarihceKaydet(t.TakipKodu, "Atama Islemi", $"Sorumlu: {adSoyad} (Atayan: {usr.AdSoyad})");
                _ = _pushNotificationService.NotifyTicketSorumluAtandiAsync(t.ID);

                return true;
            }
            return false;
        }

        public object GetTicketDetail(int ticketID)
        {
            var t = _context.tb_Ticket
                .AsNoTracking()
                .FirstOrDefault(x => x.ID == ticketID);

            if (t == null) return null;

            var sirketAdi = _context.tb_Sirket
                .AsNoTracking()
                .FirstOrDefault(s => s.SirketKodu == t.SirketKodu)?.SirketAdi ?? t.SirketKodu;

            var olusturanAd = _context.tb_Personel
                .AsNoTracking()
                .FirstOrDefault(p => p.SicilNo == t.KayitSicilNo)?.AdSoyad ?? t.KayitSicilNo;

            var sorumluAd = _context.tb_Personel
                .AsNoTracking()
                .FirstOrDefault(p => p.SicilNo == t.SorumluSicilNo)?.AdSoyad ?? "Atanmamis";

            var kategoriAd = _context.tb_TicketKategori
                .AsNoTracking()
                .FirstOrDefault(k => k.ID == t.KategoriID)?.Tanim ?? "";

            var ticketResolved = new
            {
                t.ID,
                t.TakipKodu,
                t.SirketKodu,
                SirketAdi = sirketAdi,
                t.Baslik,
                t.Aciklama,
                t.IslemTuru,
                t.SurecDurumu,
                t.Oncelik,
                t.BitisTarihi,
                t.KayitTarihi,
                KayitTarihiStr = t.KayitTarihi != null ? t.KayitTarihi.Value.ToString("dd.MM.yyyy HH:mm") : "",
                t.KayitSicilNo,
                KayitYapanAd = olusturanAd,
                t.SorumluSicilNo,
                SorumluAd = sorumluAd,
                t.KategoriID,
                KategoriAd = kategoriAd
            };

            var yorumlar = _context.tb_TicketAciklama
                .AsNoTracking()
                .Where(y => y.TicketID == ticketID)
                .OrderByDescending(y => y.KayitTarihi)
                .ToList()
                .Select(y => new
                {
                    y.ID,
                    y.Aciklama,
                    KayitTarihiStr = y.KayitTarihi != null ? y.KayitTarihi.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    YorumYapan = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == y.SicilNo)?.AdSoyad ?? y.SicilNo,
                    y.SicilNo
                }).ToList();

            var dosyalar = _context.tb_TicketDosya
                .AsNoTracking()
                .Where(d => d.TicketID == ticketID)
                .ToList();

            var tarihce = _context.tb_BelgeTarihce
                .AsNoTracking()
                .Where(h => h.BelgeKodu == t.TakipKodu)
                .OrderByDescending(h => h.BelgeTarihceID)
                .ToList()
                .Select(h => new
                {
                    Tarih = h.KayitTar != null ? h.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    h.Konu,
                    h.Aciklama
                }).ToList();

            return new
            {
                Ticket = ticketResolved,
                Yorumlar = yorumlar,
                Dosyalar = dosyalar,
                Tarihce = tarihce
            };
        }

        public IEnumerable<Company> GetCompanies()
        {
            return _context.tb_Sirket
                .AsNoTracking()
                .OrderBy(s => s.SirketKodu)
                .Select(s => new Company
                {
                    SirketKodu = s.SirketKodu,
                    SirketAdi = s.SirketAdi
                })
                .ToList();
        }

        // Belirtilen sirkete bagli aktif ticket kategorilerini getirir (yeni kayit formu icin).
        public IEnumerable<object> GetCategories(string sirketKodu)
        {
            if (string.IsNullOrEmpty(sirketKodu)) return new List<object>();

            return _context.tb_TicketKategori
                .AsNoTracking()
                .Where(k => k.SirketKodu == sirketKodu && k.Durum != false)
                .OrderBy(k => k.Tanim)
                .Select(k => new { id = k.ID, tanim = k.Tanim })
                .ToList();
        }

        public IEnumerable<Personel> GetPersonels()
        {
            var users = _context.tb_Kullanici
                .AsNoTracking()
                .Where(k => k.AdminBelgeTur != null && (k.AdminBelgeTur.ToUpper().Contains("TICKET") || k.AdminBelgeTur.ToUpper().Contains("ADMIN")))
                .Select(k => k.SicilNo)
                .ToList();

            return _context.tb_Personel
                .AsNoTracking()
                .Where(p => p.Durum == true && users.Contains(p.SicilNo))
                .OrderBy(p => p.AdSoyad)
                .Select(p => new Personel
                {
                    SicilNo = p.SicilNo,
                    AdSoyad = p.AdSoyad
                })
                .ToList();
        }

        public bool SaveComment(int kullaniciID, int ticketID, string aciklama)
        {
            var usr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (usr == null) return false;

            var t = _context.tb_Ticket.FirstOrDefault(x => x.ID == ticketID);

            if (t != null)
            {
                var yorum = new tb_TicketAciklama
                {
                    TicketID = ticketID,
                    SicilNo = usr.SicilNo,
                    Aciklama = aciklama,
                    KayitTarihi = DateTime.Now
                };
                _context.tb_TicketAciklama.Add(yorum);
                _context.SaveChanges();

                BelgeTarihceKaydet(t.TakipKodu, "Yeni Yorum", $"Ticketa yeni gelisme eklendi. (Yapan: {usr.AdSoyad})");
                _ = _pushNotificationService.NotifyTicketGelismeAsync(t.ID, usr.KullaniciID, aciklama);

                return true;
            }
            return false;
        }

        public bool SaveFile(int ticketID, string dosyaAdi, string dosyaYolu, string dosyaTipi)
        {
            var d = new tb_TicketDosya
            {
                TicketID = ticketID,
                DosyaAdi = dosyaAdi,
                DosyaYolu = dosyaYolu,
                DosyaTipi = dosyaTipi,
                KayitTarihi = DateTime.Now
            };
            _context.tb_TicketDosya.Add(d);
            return _context.SaveChanges() > 0;
        }

        public bool UpdateTicketSira(int kullaniciID, List<int> ticketIDs, string yeniDurum, int? draggedID)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var usr = _context.tb_Kullanici
                        .AsNoTracking()
                        .FirstOrDefault(u => u.KullaniciID == kullaniciID);

                    if (usr == null) return false;

                    if (draggedID.HasValue && !string.IsNullOrEmpty(yeniDurum))
                    {
                        var dt = _context.tb_Ticket.FirstOrDefault(t => t.ID == draggedID.Value);
                        if (dt != null && dt.SurecDurumu != yeniDurum)
                        {
                            dt.SurecDurumu = yeniDurum;
                            _context.SaveChanges();
                            BelgeTarihceKaydet(dt.TakipKodu, "S?re? Degisikligi", $"'{yeniDurum}' asamasina tasindi. (Yapan: {usr.AdSoyad})");
                        }
                    }

                    if (ticketIDs != null)
                    {
                        for (int i = 0; i < ticketIDs.Count; i++)
                        {
                            var targetTicket = _context.tb_Ticket.FirstOrDefault(t => t.ID == ticketIDs[i]);
                            if (targetTicket != null)
                            {
                                targetTicket.Sira = i + 1;
                            }
                        }
                        _context.SaveChanges();
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        public string DeleteTicket(int kullaniciID, int id, string webRootPath)
        {
            var currentUsr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (currentUsr == null) return "Yetkiniz yok.";

            bool isAdmin = HasTicketAuthority(currentUsr.AdminBelgeTur);
            if (!isAdmin) return "Yetkiniz yok.";

            var t = _context.tb_Ticket.FirstOrDefault(x => x.ID == id);
            if (t == null) return "Kayit bulunamadi.";

            // 1. Delete comments
            var comments = _context.tb_TicketAciklama.Where(c => c.TicketID == id).ToList();
            _context.tb_TicketAciklama.RemoveRange(comments);

            // 2. Delete files
            var files = _context.tb_TicketDosya.Where(d => d.TicketID == id).ToList();
            foreach (var file in files)
            {
                try
                {
                    string filePath = Path.Combine(webRootPath, "Ticket", "Docs", file.DosyaYolu);
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
                catch { }
            }
            _context.tb_TicketDosya.RemoveRange(files);

            // 3. Save silindi history
            BelgeTarihceKaydet(t.TakipKodu, "Ticket Silindi", $"Ticket silindi. (Yapan: {currentUsr.AdSoyad})");

            // Silmeden önce bildirim için gerekli bilgileri yakala
            string kayitSicil = t.KayitSicilNo;
            string sorumluSicil = t.SorumluSicilNo;
            string takipKodu = t.TakipKodu;
            string baslik = t.Baslik;

            // 4. Delete ticket
            _context.tb_Ticket.Remove(t);
            _context.SaveChanges();

            // 5. Kayıt eden ve sorumluya silme bildirimi (fire-and-forget)
            _ = _pushNotificationService.NotifyTicketDeletedAsync(kayitSicil, sorumluSicil, takipKodu, baslik, currentUsr.AdSoyad, currentUsr.SicilNo);

            return "1";
        }

        public object GetDashboardStats(int kullaniciID, string sirketKodu, int ay, int fltYil, int fltAy)
        {
            var usr = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (usr == null) return null;

            var per = _context.tb_Personel
                .AsNoTracking()
                .FirstOrDefault(p => p.SicilNo == usr.SicilNo);

            bool isAdmin = HasTicketAuthority(usr.AdminBelgeTur);

            var query = _context.tb_Ticket.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                string userSirket = per?.SirketKodu ?? "0";
                query = query.Where(o => o.SirketKodu == userSirket || o.KayitSicilNo == usr.SicilNo);
            }
            else if (!string.IsNullOrEmpty(sirketKodu))
            {
                query = query.Where(o => o.SirketKodu == sirketKodu);
            }

            if (fltYil > 0)
            {
                query = query.Where(o => o.KayitTarihi != null && o.KayitTarihi.Value.Year == fltYil);
            }

            if (fltAy > 0)
            {
                query = query.Where(o => o.KayitTarihi != null && o.KayitTarihi.Value.Month == fltAy);
            }

            if (ay > 0 && fltYil == 0 && fltAy == 0)
            {
                var threshold = DateTime.Now.Date.AddMonths(-ay);
                query = query.Where(o => o.KayitTarihi >= threshold);
            }

            var list = query.ToList();

            var trendDate = DateTime.Now.Date.AddDays(-14);
            var weeklyTrend = list
                .Where(o => o.KayitTarihi >= trendDate)
                .GroupBy(o => o.KayitTarihi.Value.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key.ToString("dd.MM"), Count = g.Count() })
                .ToList();

            // Sayımlar — legacy WebServiceTicket.GetDashboardStats ile birebir aynı
            int totalCount = list.Count;
            int inProgressCount = list.Count(o => o.SurecDurumu == "ISLEM");
            int inTestCount = list.Count(o => o.SurecDurumu == "TEST");
            int completedCount = list.Count(o => o.SurecDurumu == "TAMAM");
            int havuzCount = list.Count(o => o.SurecDurumu == "HAVUZ");
            int openCount = havuzCount + inProgressCount + inTestCount;
            int highPrioCount = list.Count(o => (o.Oncelik == "Yüksek" || o.Oncelik == "Kritik") && o.SurecDurumu != "TAMAM");
            int todayCount = list.Count(o => o.KayitTarihi.HasValue && o.KayitTarihi.Value.Date == DateTime.Today);
            int unassignedCount = list.Count(o => string.IsNullOrEmpty(o.SorumluSicilNo) && o.SurecDurumu != "TAMAM");

            // İsim eşleme sözlükleri (legacy'de her grupta ayrı sorgu; burada tek seferde)
            var sirketDict = _context.tb_Sirket.AsNoTracking().GroupBy(s => s.SirketKodu).ToDictionary(g => g.Key, g => g.First().SirketAdi);
            var katDict = _context.tb_TicketKategori.AsNoTracking().GroupBy(k => k.ID).ToDictionary(g => g.Key, g => g.First().Tanim);
            var persDict = _context.tb_Personel.AsNoTracking().GroupBy(p => p.SicilNo).ToDictionary(g => g.Key, g => g.First().AdSoyad);
            Func<string, string> SirketAd = k => (k != null && sirketDict.TryGetValue(k, out var v) && v != null) ? v : (k ?? "");
            Func<int?, string> KatAd = id => (id.HasValue && katDict.TryGetValue(id.Value, out var v) && v != null) ? v : "Genel";
            Func<string, string> PersAd = s => (s != null && persDict.TryGetValue(s, out var v) && v != null) ? v : (s ?? "");

            // --- AI Insights (kural tabanlı) — legacy ile birebir ---
            var insights = new List<object>();
            double completionRate = totalCount > 0 ? (double)completedCount / totalCount * 100 : 0;

            if (completionRate < 40 && totalCount > 5)
                insights.Add(new { type = "warning", icon = "ki-chart-line-down", text = "Tamamlanma oranı " + ((int)completionRate) + "% — Bekleyen bilet hacmi kritik, öncelikli kapatma planı önerilir." });
            else if (completionRate >= 80)
                insights.Add(new { type = "success", icon = "ki-check-circle", text = "Tamamlanma oranı " + ((int)completionRate) + "% — Ekip performansı çok iyi, bu tempo sürdürülebilir." });

            if (highPrioCount > 0)
                insights.Add(new { type = "danger", icon = "ki-shield-cross", text = highPrioCount + " adet yüksek/kritik öncelikli bilet bekliyor — Acil müdahale gerekebilir." });

            if (unassignedCount > 0)
                insights.Add(new { type = "warning", icon = "ki-user-cross", text = unassignedCount + " bilet henüz sorumluya atanmamış — Talep havuzunu kontrol edin." });

            if (inTestCount > 3)
                insights.Add(new { type = "info", icon = "ki-magnifier", text = inTestCount + " bilet test aşamasında bekliyor — Onay süreçleri yavaşlamış olabilir." });

            if (todayCount > 5)
                insights.Add(new { type = "info", icon = "ki-calendar", text = "Bugün " + todayCount + " yeni bilet açıldı — Günlük yoğunluk ortalamanın üzerinde." });

            if (inProgressCount > 10)
                insights.Add(new { type = "warning", icon = "ki-timer", text = inProgressCount + " bilet aynı anda işlemde — Paralel iş yükü fazla, dağılım gözden geçirilmeli." });

            // Şirket bazlı iş yükü yoğunluğu
            if (openCount > 10)
            {
                var topCompanyLoad = list.Where(o => o.SurecDurumu != "TAMAM").GroupBy(o => o.SirketKodu)
                    .Select(g => new { Kodu = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).FirstOrDefault();
                if (topCompanyLoad != null && topCompanyLoad.Count > (openCount * 0.4))
                    insights.Add(new { type = "info", icon = "ki-bank", text = "Yük Dağılımı: Açık biletlerin " + ((int)((double)topCompanyLoad.Count / openCount * 100)) + "% kadarı '" + SirketAd(topCompanyLoad.Kodu) + "' şirketine ait. Kaynak planlaması bu tarafa kaydırılabilir." });
            }

            // Acil/kritik yoğunluk alarmı
            if (openCount > 0)
            {
                double urgentRatio = (double)highPrioCount / openCount * 100;
                if (urgentRatio > 20)
                    insights.Add(new { type = "danger", icon = "ki-shield-tick", text = "Kritik Yoğunluk: Açık biletlerin " + ((int)urgentRatio) + "% kadarı Yüksek/Kritik seviyede — Genel sistem sağlığı risk altında olabilir." });
            }

            // Şirket bazlı sahipsiz biletler
            var unassignedByCompany = list.Where(o => string.IsNullOrEmpty(o.SorumluSicilNo) && o.SurecDurumu != "TAMAM")
                .GroupBy(o => o.SirketKodu).Select(g => new { Kodu = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).FirstOrDefault();
            if (unassignedByCompany != null && unassignedByCompany.Count > 3)
                insights.Add(new { type = "warning", icon = "ki-user-cross", text = "'" + SirketAd(unassignedByCompany.Kodu) + "' şirketine ait " + unassignedByCompany.Count + " talep henüz sahipsiz — Atama bekliyor." });

            // Global kategori lideri
            var globalTopCat = list.GroupBy(o => o.KategoriID).Select(g => new { ID = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).FirstOrDefault();
            if (globalTopCat != null && globalTopCat.Count > (totalCount * 0.3))
                insights.Add(new { type = "info", icon = "ki-chart-pie-4", text = "Global Trend: Tüm taleplerin " + ((int)((double)globalTopCat.Count / totalCount * 100)) + "% kadarı '" + KatAd(globalTopCat.ID) + "' odaklı — Bu alan için geliştirme/eğitim gerekebilir." });

            // Yıllanmış biletler
            var staleCount = list.Count(o => o.SurecDurumu != "TAMAM" && o.KayitTarihi.HasValue && o.KayitTarihi.Value < DateTime.Now.AddDays(-14));
            if (staleCount > 0)
                insights.Add(new { type = "danger", icon = "ki-time", text = staleCount + " bilet 2 haftadan uzun süredir sonuçlanmamış — 'Yıllanmış' talepler için takip gerekebilir." });

            // Darboğaz analizi (tek personelde aşırı yük)
            if (openCount > 5)
            {
                var staffLoad = list.Where(o => !string.IsNullOrEmpty(o.SorumluSicilNo) && o.SurecDurumu != "TAMAM")
                    .GroupBy(o => o.SorumluSicilNo).Select(g => new { Name = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).FirstOrDefault();
                if (staffLoad != null && staffLoad.Count > (openCount * 0.4))
                    insights.Add(new { type = "warning", icon = "ki-user-square", text = "İş yükü darboğazı: " + PersAd(staffLoad.Name) + " üzerinde " + staffLoad.Count + " açık bilet var — Görev paylaşımı önerilir." });
            }

            // Havuzda unutulanlar
            var waitingInPool = list.Count(o => o.SurecDurumu == "HAVUZ" && o.KayitTarihi.HasValue && o.KayitTarihi.Value < DateTime.Now.AddDays(-3));
            if (waitingInPool > 0)
                insights.Add(new { type = "danger", icon = "ki-loading", text = waitingInPool + " bilet 3 gündür havuzda atanmayı bekliyor — Müdahale edilmezse SLA riskine girebilir." });

            // Kategori yoğunluk alarmı
            var topCategory = list.Where(o => o.SurecDurumu != "TAMAM").GroupBy(o => o.KategoriID)
                .Select(g => new { ID = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).FirstOrDefault();
            if (topCategory != null && topCategory.Count > 5)
                insights.Add(new { type = "info", icon = "ki-category", text = "Yoğunluk tespiti: Açık biletlerin çoğu '" + KatAd(topCategory.ID) + "' kategorisinde toplanmış — Kronik bir sorun olabilir mi?" });

            if (insights.Count == 0)
                insights.Add(new { type = "success", icon = "ki-badge", text = "Tüm göstergeler normal sınırlar içinde — Sistem sağlıklı çalışıyor!" });

            string StatusLabel(string s)
            {
                switch (s)
                {
                    case "HAVUZ": return "Talep Havuzuna aktarıldı";
                    case "ISLEM": return "İşleme Alındı";
                    case "TEST": return "Test aşamasına alındı";
                    case "TAMAM": return "Tamamlandı";
                    default: return s;
                }
            }

            // Dönüş — legacy alan adları ve yapısıyla birebir
            return new
            {
                Total = totalCount,
                Open = openCount,
                Havuz = havuzCount,
                Completed = completedCount,
                InProgress = inProgressCount,
                InTest = inTestCount,
                HighPriority = highPrioCount,
                Today = todayCount,
                Unassigned = unassignedCount,

                ByStatus = list.GroupBy(o => o.SurecDurumu).Select(g => new { Status = StatusLabel(g.Key), Count = g.Count() }).ToList(),

                ByPriority = list.GroupBy(o => o.Oncelik).Select(g => new { Priority = g.Key, Count = g.Count() }).ToList(),

                ByCompany = list.GroupBy(o => o.SirketKodu).Select(g => new
                {
                    SirketKodu = g.Key,
                    SirketAdi = SirketAd(g.Key),
                    Count = g.Count()
                }).ToList(),

                ByCategory = list.GroupBy(o => o.KategoriID).Select(g =>
                {
                    string katAd = KatAd(g.Key);
                    int total = g.Count();
                    int havuz = g.Count(x => x.SurecDurumu == "HAVUZ");
                    int islem = g.Count(x => x.SurecDurumu == "ISLEM");
                    int test = g.Count(x => x.SurecDurumu == "TEST");
                    int tamam = g.Count(x => x.SurecDurumu == "TAMAM");
                    string aiYorum = string.Format("'{0}' kategorisinde toplam {1} talep açılmış. Bunların {2} kadarı havuzda bekliyor, {3} kadarı işlemde/testte ve {4} tanesi başarıyla tamamlanmış. Başarı oranı %{5}.",
                        katAd, total, havuz, islem + test, tamam, total > 0 ? (int)((double)tamam / total * 100) : 0);
                    return new { KategoriAd = katAd, Count = total, HavuzCount = havuz, IslemCount = islem, TestCount = test, TamamCount = tamam, AiInsight = aiYorum };
                }).OrderByDescending(x => x.Count).Take(5).ToList(),

                ByStaff = list.Where(o => !string.IsNullOrEmpty(o.SorumluSicilNo)).GroupBy(o => o.SorumluSicilNo).Select(g => new
                {
                    StaffName = PersAd(g.Key),
                    HavuzCount = g.Count(x => x.SurecDurumu == "HAVUZ"),
                    IslemCount = g.Count(x => x.SurecDurumu == "ISLEM"),
                    TestCount = g.Count(x => x.SurecDurumu == "TEST"),
                    CompletedCount = g.Count(x => x.SurecDurumu == "TAMAM")
                }).OrderByDescending(x => x.IslemCount + x.HavuzCount + x.TestCount).Take(10).ToList(),

                Trend = weeklyTrend,
                AiInsights = insights
            };
        }

        private void BelgeTarihceKaydet(string code, string konu, string aciklama)
        {
            try
            {
                var history = new tb_BelgeTarihce
                {
                    BelgeKodu = code,
                    Konu = konu + " [Mobil]",
                    Aciklama = aciklama,
                    KayitTar = DateTime.Now
                };
                _context.tb_BelgeTarihce.Add(history);
                _context.SaveChanges();
            }
            catch { }
        }
    }
}
