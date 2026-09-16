using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Common;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;
using OyemCore.BusinessLayer.Dtos;

namespace OyemCore.BusinessLayer.Services
{
    public class TalepService : ITalepService
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IBildirimService _bildirim;
        private readonly INotificationService _notificationService;

        public TalepService(IYbsDbContext context, IPushNotificationService pushNotificationService, IBildirimService bildirim, INotificationService notificationService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _bildirim = bildirim;
            _notificationService = notificationService;
        }

        private string GetUserEmailBySicil(string sicilNo)
        {
            if (string.IsNullOrEmpty(sicilNo)) return null;
            return _context.tb_Kullanici
                .AsNoTracking()
                .Where(u => u.SicilNo == sicilNo && !string.IsNullOrEmpty(u.Eposta))
                .Select(u => u.Eposta)
                .FirstOrDefault();
        }

        private string GetUserPhoneBySicil(string sicilNo)
        {
            if (string.IsNullOrEmpty(sicilNo)) return null;
            return _context.tb_Kullanici
                .AsNoTracking()
                .Where(u => u.SicilNo == sicilNo && !string.IsNullOrEmpty(u.Tel1))
                .Select(u => u.Tel1)
                .FirstOrDefault();
        }

        private bool HasAuthority(string adminBelgeTur, string turKodu)
        {
            if (string.IsNullOrEmpty(adminBelgeTur)) return false;
            var tokens = adminBelgeTur.Split('*', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpper());
            return tokens.Contains("ADMIN") || tokens.Contains("TICKET") || tokens.Contains(turKodu.ToUpper());
        }

        // Referans: WebPortal WebServiceBakim.cs TalepGelismeVeKapama/AkademiAta tarzı yetki deseni —
        // sorumlu olmayan bir kullanıcının, talebin kategorisinde (ve mümkünse şirketinde)
        // tb_TalepAyar.YoneticiMi=true olarak tanımlı olup olmadığını kontrol eder. Sadece BAKIM
        // talepleri için kullanılır (WebPortal tarafında da bu mantık BAKIM'a özeldi).
        private bool IsKategoriYoneticisi(tb_Talep t, tb_Kullanici user)
        {
            if (t.TalepTurKodu != "BAKIM") return false;

            string sirketKodu = _context.tb_TalepBakim
                .Where(o => o.TalepKodu == t.TalepKodu)
                .Select(o => o.SirketKodu)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(sirketKodu))
                sirketKodu = _context.tb_Personel
                    .Where(o => o.SicilNo == t.KayitSicil)
                    .Select(o => o.SirketKodu)
                    .FirstOrDefault();

            return _context.tb_TalepAyar.Any(o => o.KategoriID == t.KategoriID
                && o.SicilNo == user.SicilNo
                && o.YoneticiMi == true
                && (string.IsNullOrEmpty(sirketKodu) || o.SirketKodu == sirketKodu));
        }

        // Referans: WebPortal WebServiceBakim.cs/WebServiceHelpDesk.cs TalepKaydet — YetkiBelgeTur ve
        // TLPACIL kontrolleri burada ADMIN muafiyeti KULLANMAZ (referansta da yok, kasıtlı olarak
        // HasAuthority'den ayrı tutuluyor).
        private bool HasBelgeTur(string adminBelgeTur, string tur)
        {
            if (string.IsNullOrEmpty(adminBelgeTur) || string.IsNullOrEmpty(tur)) return false;
            var tokens = adminBelgeTur.Split('*', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpper());
            return tokens.Contains(tur.Trim().ToUpper());
        }

        // Liste ekranı için: her talebin gelişme (tb_TalepGelisme) ve ekli dosya (DosyaUrl'i dolu gelişme) adedi.
        // N+1 olmasın diye tek sorguda gruplanır.
        private Dictionary<string, (int Gelisme, int Dosya)> GelismeDosyaSayaclari(IEnumerable<string> talepKodlari)
        {
            var kodlar = talepKodlari.Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
            if (kodlar.Count == 0) return new Dictionary<string, (int, int)>();
            return _context.tb_TalepGelisme.AsNoTracking()
                .Where(g => kodlar.Contains(g.TalepKodu))
                .GroupBy(g => g.TalepKodu)
                .Select(g => new { Kodu = g.Key, Gelisme = g.Count(), Dosya = g.Count(x => x.DosyaUrl != null && x.DosyaUrl != "") })
                .ToList()
                .ToDictionary(x => x.Kodu, x => (x.Gelisme, x.Dosya));
        }

        public IEnumerable<object> GetRequests(int kullaniciID, string turKodu)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return Enumerable.Empty<object>();

            bool isAdmin = HasAuthority(user.AdminBelgeTur, turKodu);

            if (turKodu == "BAKIM")
            {
                var queryBakim = from t in _context.tb_Talep
                                 join tk in _context.tb_TalepKategori on t.KategoriID equals tk.TalepKategoriID into tks
                                 from tk in tks.DefaultIfEmpty()
                                 join p1 in _context.tb_Personel on t.KayitSicil equals p1.SicilNo into p1s
                                 from p1 in p1s.DefaultIfEmpty()
                                 join p2 in _context.tb_Personel on t.SorumluSicil equals p2.SicilNo into p2s
                                 from p2 in p2s.DefaultIfEmpty()
                                 join tb in _context.tb_TalepBakim on t.TalepKodu equals tb.TalepKodu into tbs
                                 from tb in tbs.DefaultIfEmpty()
                                 join ts in _context.tb_Sirket on (tb != null ? tb.SirketKodu : null) equals ts.SirketKodu into tss
                                 from ts in tss.DefaultIfEmpty()
                                 join tb_blm in _context.tb_Bolum on (tb != null ? tb.BolumKodu : null) equals tb_blm.BolumKodu into tb_blms
                                 from tb_blm in tb_blms.DefaultIfEmpty()
                                 join tm in _context.tb_Makine on (tb != null ? tb.MakineKodu : null) equals tm.MakineKodu into tms
                                 from tm in tms.DefaultIfEmpty()
                                 where t.TalepTurKodu == "BAKIM"
                                 select new { t, tk, p1, p2, tb, ts, tb_blm, tm, HasOnay = _context.tb_TalepAmir.Any(a => a.TalepKodu == t.TalepKodu && a.Durum == null) };

                if (!isAdmin)
                {
                    queryBakim = queryBakim.Where(x => x.t.KayitSicil == user.SicilNo || x.t.SorumluSicil == user.SicilNo);
                }

                var list = queryBakim.OrderByDescending(x => x.t.KayitTar).ToList();
                var sayac = GelismeDosyaSayaclari(list.Select(x => x.t.TalepKodu));

                return list.Select(o => new
                {
                    o.t.TalepID,
                    o.t.TalepTurKodu,
                    o.t.TalepKodu,
                    o.t.KategoriID,
                    o.t.AltKategoriID,
                    KategoriAdi = o.tk != null ? o.tk.Tanim : "Genel",
                    o.t.Konu,
                    o.t.Aciklama,
                    o.t.OnemSeviye,
                    o.t.KayitSicil,
                    o.t.KayitEposta,
                    KayitYapanAd = o.p1 != null ? o.p1.AdSoyad : o.t.KayitSicil,
                    KayitTarStr = o.t.KayitTar != null ? o.t.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    o.t.DosyaUrl,
                    o.t.SorumluSicil,
                    o.t.SorumluEposta,
                    SorumluAd = o.p2 != null ? o.p2.AdSoyad : "Atanmamis",
                    Durum = o.t.Durum == true ? "Kapalı" : (o.HasOnay ? "ONAY BEKLİYOR" : "BEKLEMEDE"),
                    KapanmaTarStr = o.t.KapanmaTar != null ? o.t.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    IsMine = o.t.SorumluSicil == user.SicilNo,
                    SirketAdi = o.ts != null ? o.ts.SirketAdi : null,
                    BolumAdi = o.tb_blm != null ? o.tb_blm.BolumAdi : null,
                    MakineAdi = o.tm != null ? o.tm.MakineAdi : null,
                    UretimDurusu = o.tb != null ? o.tb.UretimDurusu : null,
                    TalepPuan = o.t.TalepPuan,
                    PuanRenk = ClsYardim.BakimPuanRenk(o.t.TalepPuan),
                    GelismeAdet = (o.t.TalepKodu != null && sayac.ContainsKey(o.t.TalepKodu)) ? sayac[o.t.TalepKodu].Gelisme : 0,
                    DosyaAdet = ((o.t.TalepKodu != null && sayac.ContainsKey(o.t.TalepKodu)) ? sayac[o.t.TalepKodu].Dosya : 0) + (!string.IsNullOrEmpty(o.t.DosyaUrl) ? 1 : 0)
                }).ToList();
            }
            else
            {
                var queryGeneric = from t in _context.tb_Talep
                                   join tk in _context.tb_TalepKategori on t.KategoriID equals tk.TalepKategoriID into tks
                                   from tk in tks.DefaultIfEmpty()
                                   join p1 in _context.tb_Personel on t.KayitSicil equals p1.SicilNo into p1s
                                   from p1 in p1s.DefaultIfEmpty()
                                   join p2 in _context.tb_Personel on t.SorumluSicil equals p2.SicilNo into p2s
                                   from p2 in p2s.DefaultIfEmpty()
                                   where t.TalepTurKodu == turKodu
                                   select new { t, tk, p1, p2, HasOnay = _context.tb_TalepAmir.Any(a => a.TalepKodu == t.TalepKodu && a.Durum == null) };

                if (!isAdmin)
                {
                    // Kendi açtığı + sorumlusu olduğu + onayına gönderilen (bekleyen amir).
                    // Onaya gönderilen talep listede görünmezse onaylayan kişi talebe
                    // ulaşıp onay/ret veremiyordu (webportal onaylayana talebi gösterir).
                    queryGeneric = queryGeneric.Where(x =>
                        x.t.KayitSicil == user.SicilNo ||
                        x.t.SorumluSicil == user.SicilNo ||
                        _context.tb_TalepAmir.Any(a => a.TalepKodu == x.t.TalepKodu && a.AmirSicil == user.SicilNo && a.Durum == null));
                }

                var list = queryGeneric.OrderByDescending(x => x.t.KayitTar).ToList();
                var sayac = GelismeDosyaSayaclari(list.Select(x => x.t.TalepKodu));

                return list.Select(o => new
                {
                    o.t.TalepID,
                    o.t.TalepTurKodu,
                    o.t.TalepKodu,
                    o.t.KategoriID,
                    o.t.AltKategoriID,
                    KategoriAdi = o.tk != null ? o.tk.Tanim : "Genel",
                    o.t.Konu,
                    o.t.Aciklama,
                    o.t.OnemSeviye,
                    o.t.KayitSicil,
                    o.t.KayitEposta,
                    KayitYapanAd = o.p1 != null ? o.p1.AdSoyad : o.t.KayitSicil,
                    KayitTarStr = o.t.KayitTar != null ? o.t.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    o.t.DosyaUrl,
                    o.t.SorumluSicil,
                    o.t.SorumluEposta,
                    SorumluAd = o.p2 != null ? o.p2.AdSoyad : "Atanmamis",
                    Durum = o.t.Durum == true ? "Kapalı" : (o.HasOnay ? "ONAY BEKLİYOR" : "BEKLEMEDE"),
                    KapanmaTarStr = o.t.KapanmaTar != null ? o.t.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    IsMine = o.t.SorumluSicil == user.SicilNo,
                    SirketAdi = (string)null,
                    BolumAdi = (string)null,
                    MakineAdi = (string)null,
                    UretimDurusu = (string)null,
                    TalepPuan = (int?)null,
                    PuanRenk = (string)null,
                    GelismeAdet = (o.t.TalepKodu != null && sayac.ContainsKey(o.t.TalepKodu)) ? sayac[o.t.TalepKodu].Gelisme : 0,
                    DosyaAdet = ((o.t.TalepKodu != null && sayac.ContainsKey(o.t.TalepKodu)) ? sayac[o.t.TalepKodu].Dosya : 0) + (!string.IsNullOrEmpty(o.t.DosyaUrl) ? 1 : 0)
                }).ToList();
            }
        }

        public IEnumerable<tb_TalepKategori> GetCategories(string turKodu)
        {
            return _context.tb_TalepKategori
                .AsNoTracking()
                .Where(c => c.TalepTurKodu == turKodu && c.Durum == true)
                .OrderBy(c => c.Tanim)
                .ToList();
        }

        public object GetRequestDetail(int kullaniciID, int talepID)
        {
            var query = from t in _context.tb_Talep
                        join tk in _context.tb_TalepKategori on t.KategoriID equals tk.TalepKategoriID into tks
                        from tk in tks.DefaultIfEmpty()
                        join p1 in _context.tb_Personel on t.KayitSicil equals p1.SicilNo into p1s
                        from p1 in p1s.DefaultIfEmpty()
                        join p2 in _context.tb_Personel on t.SorumluSicil equals p2.SicilNo into p2s
                        from p2 in p2s.DefaultIfEmpty()
                        join tb in _context.tb_TalepBakim on t.TalepKodu equals tb.TalepKodu into tbs
                        from tb in tbs.DefaultIfEmpty()
                        join ts in _context.tb_Sirket on (tb != null ? tb.SirketKodu : null) equals ts.SirketKodu into tss
                        from ts in tss.DefaultIfEmpty()
                        join tb_blm in _context.tb_Bolum on (tb != null ? tb.BolumKodu : null) equals tb_blm.BolumKodu into tb_blms
                        from tb_blm in tb_blms.DefaultIfEmpty()
                        join tm in _context.tb_Makine on (tb != null ? tb.MakineKodu : null) equals tm.MakineKodu into tms
                        from tm in tms.DefaultIfEmpty()
                        where t.TalepID == talepID
                        select new { t, tk, p1, p2, tb, ts, tb_blm, tm };

            var item = query.FirstOrDefault();
            if (item == null) return null;

            string code = item.t.TalepKodu;

            var gelismeler = (from g in _context.tb_TalepGelisme
                              join p in _context.tb_Personel on g.Sicil equals p.SicilNo into ps
                              from p in ps.DefaultIfEmpty()
                              where g.TalepKodu == code
                              orderby g.KayitTar descending
                              select new
                              {
                                  g.TalepGelismeID,
                                  g.TalepKodu,
                                  g.Aciklama,
                                  g.Sicil,
                                  g.Eposta,
                                  g.DosyaUrl,
                                  KayitTarStr = g.KayitTar != null ? g.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                  AdSoyad = p != null ? p.AdSoyad : g.Sicil
                              }).ToList();

            var history = _context.tb_BelgeTarihce
                .AsNoTracking()
                .Where(h => h.BelgeKodu == code)
                .OrderByDescending(h => h.BelgeTarihceID)
                .Select(h => new
                {
                    Tarih = h.KayitTar != null ? h.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    h.Konu,
                    h.Aciklama
                })
                .ToList();

            // Giris yapan kullanicinin rol?
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            string girisTur = "HATA";
            if (user != null)
            {
                if (user.Eposta == item.t.KayitEposta)
                    girisTur = "SAHIP";
                else if (user.Eposta == item.t.SorumluEposta)
                    girisTur = "SORUMLU";
                else if (IsKategoriYoneticisi(item.t, user))
                    girisTur = "YONETICI";
                else if (_context.tb_TalepAmir.Any(o => o.TalepKodu == code && o.AmirSicil == user.SicilNo && o.Durum == null))
                    girisTur = "ONAY";
                else if (HasAuthority(user.AdminBelgeTur, item.t.TalepTurKodu))
                    girisTur = "HAVUZ";
                else if (_context.tb_TalepAmir.Any(o => o.TalepKodu == code && o.AmirSicil == user.SicilNo))
                    girisTur = "BILGI";
                else if (_context.tb_TalepBilgi.Any(o => o.TalepKodu == code && o.BilgiSicil == user.SicilNo))
                    girisTur = "BILGI";
            }

            // Yardimci Personel Listesi
            var bilgiPersonelleri = (from b in _context.tb_TalepBilgi
                                     join p in _context.tb_Personel on b.BilgiSicil equals p.SicilNo
                                     where b.TalepKodu == code
                                     select new
                                     {
                                         b.TalepBilgiID,
                                         b.BilgiSicil,
                                         p.AdSoyad,
                                         p.Eposta
                                     }).ToList();

            // Onay Geçmişi ve Aktif Onayci
            var onayList = (from a in _context.tb_TalepAmir
                            join p in _context.tb_Personel on a.AmirSicil equals p.SicilNo
                            where a.TalepKodu == code
                            orderby a.KayitTar descending
                            select new
                            {
                                a.TalepAmirID,
                                a.AmirSicil,
                                AdSoyad = p.AdSoyad,
                                a.Durum,
                                KayitTarStr = a.KayitTar != null ? a.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                IslemTarStr = a.IslemTar != null ? a.IslemTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                            }).ToList();

            var activeOnay = onayList.FirstOrDefault(o => o.Durum == null);

            // Soru Geçmişi ve Aktif Cevaplanmamis Soru
            var soruList = (from sc in _context.tb_TalepSoruCevap
                            join p in _context.tb_Personel on sc.Sicil equals p.SicilNo
                            join gSoru in _context.tb_TalepGelisme on sc.SoruTalepGelismeID equals gSoru.TalepGelismeID into gSoruLeft
                            from gSoru in gSoruLeft.DefaultIfEmpty()
                            join gCevap in _context.tb_TalepGelisme on sc.CevapTalepGelismeID equals gCevap.TalepGelismeID into gCevapLeft
                            from gCevap in gCevapLeft.DefaultIfEmpty()
                            where sc.TalepKodu == code
                            orderby sc.TalepSoruCevapID descending
                            select new
                            {
                                sc.TalepSoruCevapID,
                                sc.Sicil,
                                p.AdSoyad,
                                Soru = gSoru != null ? gSoru.Aciklama : "",
                                Cevap = gCevap != null ? gCevap.Aciklama : "",
                                SoruTarStr = (gSoru != null && gSoru.KayitTar != null) ? gSoru.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                CevapTarStr = (gCevap != null && gCevap.KayitTar != null) ? gCevap.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                            }).ToList();

            var isEmriList = (from i in _context.tb_TalepIsEmri
                              join tur in _context.tb_IsEmriTur on i.IsEmriTurID equals tur.IsEmriTurID into turLeft
                              from tur in turLeft.DefaultIfEmpty()
                              join p in _context.tb_Personel on i.Sicil equals p.SicilNo into pLeft
                              from p in pLeft.DefaultIfEmpty()
                              where i.TalepKodu == code
                              orderby i.TalepIsEmriID descending
                              select new
                              {
                                  i.TalepIsEmriID,
                                  i.IsEmriTurID,
                                  IsEmriTuru = tur != null ? tur.Tanim : "Bilinmiyor",
                                  i.Aciklama,
                                  TerminTarStr = i.TerminTar != null ? i.TerminTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                  i.Sicil,
                                  AdSoyad = p != null ? p.AdSoyad : "Atanmamış",
                                  KayitTarStr = i.KayitTar != null ? i.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                  i.DosyaUrl,
                                  KapanmaTarStr = i.KapanmaTar != null ? i.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                  i.SonAciklama,
                                  i.Durum
                              }).ToList();

            // Talep kapandıysa detaylı süre kırılımı (brüt süre, kesintiler, net MTTR).
            object sureDetay = null;
            if (item.t.Durum == true && item.t.KapanmaTar != null && item.t.KayitTar != null)
            {
                var kr = HesaplaSureKirilim(item.t.TalepKodu, item.t.KayitTar.Value, item.t.KapanmaTar.Value);
                sureDetay = new
                {
                    ToplamSure = (int)kr.toplam,          // brüt mesai süresi (DK)
                    OnaySure = (int)kr.onay,              // onay bekleme (DK)
                    SoruCevapSure = (int)kr.soru,         // soru-cevap bekleme (DK)
                    IsEmriSure = (int)kr.isEmri,          // iş emri bekleme (DK)
                    NetKesinti = (int)kr.netKesinti,      // çakışmalar birleştirilmiş net kesinti (DK)
                    NetMttr = kr.netMttr                  // net işlem süresi = ToplamSure - NetKesinti (DK)
                };
            }

            return new
            {
                Talep = new
                {
                    item.t.TalepID,
                    item.t.TalepTurKodu,
                    item.t.TalepKodu,
                    item.t.KategoriID,
                    item.t.AltKategoriID,
                    KategoriAdi = item.tk != null ? item.tk.Tanim : "Genel",
                    item.t.Konu,
                    item.t.Aciklama,
                    item.t.OnemSeviye,
                    item.t.KayitSicil,
                    item.t.KayitEposta,
                    KayitYapanAd = item.p1 != null ? item.p1.AdSoyad : item.t.KayitSicil,
                    KayitTarStr = item.t.KayitTar != null ? item.t.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    item.t.DosyaUrl,
                    item.t.SorumluSicil,
                    item.t.SorumluEposta,
                    SorumluAd = item.p2 != null ? item.p2.AdSoyad : "Atanmamis",
                    Durum = item.t.Durum == true ? "Kapali" : "Açık",
                    KapanmaTarStr = item.t.KapanmaTar != null ? (item.t.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") + (item.t.MttrTamamSure != null ? " (" + item.t.MttrTamamSure.Value.ToString("N0") + " DK)" : "")) : "",
                    IsMine = item.t.SorumluSicil == user?.SicilNo,
                    Kilitli = item.t.Kilitli,
                    KilitTarStr = item.t.KilitTarihi != null ? (item.t.KilitTarihi.Value.ToString("dd.MM.yyyy HH:mm") + " (" + (item.t.KilitSure ?? 0).ToString("N0") + " DK)") : "",
                    SirketKodu = item.tb != null ? item.tb.SirketKodu : null,
                    SirketAdi = item.ts != null ? item.ts.SirketAdi : null,
                    BolumKodu = item.tb != null ? item.tb.BolumKodu : null,
                    BolumAdi = item.tb_blm != null ? item.tb_blm.BolumAdi : null,
                    MakineKodu = item.tb != null ? item.tb.MakineKodu : null,
                    MakineAdi = item.tm != null ? item.tm.MakineAdi : null,
                    UretimDurusu = item.tb != null ? item.tb.UretimDurusu : null,
                    GidaGuvOncelik = item.tb != null ? item.tb.GidaGuvOncelik : null,
                    IsGuvOncelik = item.tb != null ? item.tb.IsGuvOncelik : null,
                    TalepPuan = item.t.TalepPuan,
                    PuanRenk = ClsYardim.BakimPuanRenk(item.t.TalepPuan)
                },
                Bakim = item.tb,
                IsEmriList = isEmriList,
                GirisTur = girisTur,
                Gelismeler = gelismeler,
                Tarihce = history,
                BilgiPersonelleri = bilgiPersonelleri,
                OnayList = onayList,
                // Frontend bu alanı `onayBilgisi` olarak okur (aktif/bekleyen onay).
                // Eskiden `ActiveOnay` gönderiliyordu → detailData.onayBilgisi hep
                // undefined kalıyor, onay/ret ve Bakım kontrol-formu butonları hiç
                // görünmüyordu (IT/ERP onaylayan + Bakım talep sahibi).
                OnayBilgisi = activeOnay,
                SoruList = soruList,
                SureDetay = sureDetay
            };
        }

        public string SaveRequest(int kullaniciID, tb_Talep request, tb_TalepBakim bakim = null)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) throw new InvalidOperationException("Kullanici bulunamadi.");

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (!string.IsNullOrEmpty(request.SorumluSicil) && request.SorumluSicil != "0")
                    {
                        var sorm = _context.tb_Kullanici
                            .AsNoTracking()
                            .FirstOrDefault(u => u.SicilNo == request.SorumluSicil);
                        if (sorm != null)
                        {
                            request.SorumluEposta = sorm.Eposta;
                        }
                    }
                    else
                    {
                        request.SorumluSicil = null;
                        request.SorumluEposta = null;
                    }

                    if (request.TalepID == 0)
                    {
                        // Referans (WebServiceHelpDesk) sırası: önce talep insert edilip TalepID alınır,
                        // sonra TalepKodu = TalepTurKodu-yyyyMM-TalepID set edilir. Bu nedenle kod, ilk
                        // SaveChanges'ten SONRA (TalepID bilindikten sonra) üretilir.
                        string code = null;

                        request.TalepKodu = null;
                        request.KayitSicil = user.SicilNo;
                        request.KayitEposta = user.Eposta;
                        request.KayitTar = DateTime.Now;
                        request.Durum = false;

                        if (request.TalepTurKodu == "BAKIM" && bakim != null)
                        {
                            var secList = ClsYardim.TumListe();
                            var onemSec = secList.FirstOrDefault(o => o.Tur == "ONEM" && o.Kod == request.OnemSeviye);
                            var durusSec = secList.FirstOrDefault(o => o.Tur == "DURUS" && o.Kod == bakim.UretimDurusu);
                            var gidaSec = secList.FirstOrDefault(o => o.Tur == "GIDA" && o.Kod == bakim.GidaGuvOncelik);
                            var isgSec = secList.FirstOrDefault(o => o.Tur == "ISG" && o.Kod == bakim.IsGuvOncelik);

                            if (onemSec != null && durusSec != null && gidaSec != null && isgSec != null)
                            {
                                request.TalepPuan = onemSec.Deger * durusSec.Deger * gidaSec.Deger * isgSec.Deger;
                            }

                            var oncekiTalep = _context.tb_Talep
                                .AsNoTracking()
                                .Where(t => t.KayitTar < request.KayitTar && t.TalepTurKodu == "BAKIM")
                                .OrderByDescending(t => t.KayitTar)
                                .FirstOrDefault();

                            if (oncekiTalep != null && oncekiTalep.KayitTar.HasValue)
                            {
                                request.MtbfAralikSure = SureHesaplaBakim(oncekiTalep.KayitTar.Value, request.KayitTar.Value);
                            }
                            else
                            {
                                request.MtbfAralikSure = 0;
                            }
                        }

                        _context.tb_Talep.Add(request);
                        _context.SaveChanges();

                        // TalepID artık atandı → referans formatında kodu üret ve kaydet.
                        code = $"{request.TalepTurKodu}-{DateTime.Now:yyyyMM}-{request.TalepID}";
                        request.TalepKodu = code;
                        _context.SaveChanges();

                        if (request.TalepTurKodu == "BAKIM" && bakim != null)
                        {
                            bakim.TalepKodu = code;
                            _context.tb_TalepBakim.Add(bakim);
                            _context.SaveChanges();
                        }

                        BelgeTarihceKaydet(code, "Talep Oluşturuldu", $"Yeni talep kaydı açıldı. (Yapan: {user.AdSoyad})");
                        _ = _pushNotificationService.NotifyNewTalepAsync(request.TalepID);

                        // Mail gönderimi (Talebi Açan Kişiye)
                        if (!string.IsNullOrEmpty(user.Eposta))
                        {
                            _ = _notificationService.SendMailAsync($"OyemCore {request.TalepTurKodu}", $"{code} Nolu Talebiniz Alındı", 
                                $"Merhaba {user.AdSoyad},<br/><br/>{code} nolu yardım masası talebiniz başarıyla oluşturulmuştur.<br/><b>Konu:</b> {request.Konu}<br/><b>Açıklama:</b> {request.Aciklama}<br/><br/>İyi çalışmalar dileriz.", user.Eposta);
                        }

                        // Mail gönderimi (Sorumlu Atandıysa Sorumluya)
                        if (!string.IsNullOrEmpty(request.SorumluSicil))
                        {
                            var sorumluEposta = GetUserEmailBySicil(request.SorumluSicil);
                            if (!string.IsNullOrEmpty(sorumluEposta))
                            {
                                _ = _notificationService.SendMailAsync($"OyemCore {request.TalepTurKodu}", $"{code} Nolu Talep Size Atandı", 
                                    $"Merhaba,<br/><br/>{code} nolu talep üzerinize atanmıştır.<br/><b>Açan:</b> {user.AdSoyad}<br/><b>Konu:</b> {request.Konu}<br/><b>Açıklama:</b> {request.Aciklama}<br/><br/>İyi çalışmalar dileriz.", sorumluEposta);
                            }
                        }
                    }
                    else
                    {
                        var existing = _context.tb_Talep.FirstOrDefault(t => t.TalepID == request.TalepID);
                        if (existing == null) throw new InvalidOperationException("Güncellenecek talep bulunamadı.");

                        existing.KategoriID = request.KategoriID;
                        existing.AltKategoriID = request.AltKategoriID;
                        existing.Konu = request.Konu;
                        existing.Aciklama = request.Aciklama;
                        existing.OnemSeviye = request.OnemSeviye;
                        existing.DosyaUrl = request.DosyaUrl;
                        existing.SorumluSicil = request.SorumluSicil;
                        existing.SorumluEposta = request.SorumluEposta;

                        _context.SaveChanges();

                        if (request.TalepTurKodu == "BAKIM" && bakim != null)
                        {
                            var existingBakim = _context.tb_TalepBakim.FirstOrDefault(b => b.TalepKodu == existing.TalepKodu);
                            if (existingBakim != null)
                            {
                                existingBakim.SirketKodu = bakim.SirketKodu;
                                existingBakim.BolumKodu = bakim.BolumKodu;
                                existingBakim.MakineKodu = bakim.MakineKodu;
                                existingBakim.UretimDurusu = bakim.UretimDurusu;
                                existingBakim.GidaGuvOncelik = bakim.GidaGuvOncelik;
                                existingBakim.IsGuvOncelik = bakim.IsGuvOncelik;
                                _context.SaveChanges();
                            }
                        }

                        BelgeTarihceKaydet(existing.TalepKodu, "Talep Güncellendi", $"Talep bilgileri güncellendi. (Düzenleyen: {user.AdSoyad})");
                    }

                    transaction.Commit();
                    return request.TalepKodu;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new InvalidOperationException("Talep kaydedilirken hata oluştu: " + ex.Message, ex);
                }
            }
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
                    Cihaz = "mobil",
                    KayitTar = DateTime.Now
                };
                _context.tb_BelgeTarihce.Add(history);
                _context.SaveChanges();
            }
            catch { }
        }

        public IEnumerable<Personel> GetPersonels(string tur, int? kategoriId = null, string sirketKodu = null)
        {
            // Sorumlular, referansta (WebServiceHelpDesk.PersonelGetirTur / PersonelGetirKat) AdminBelgeTur'dan
            // DEĞİL, tb_TalepAyar (talep kategori sorumlu tanımları: KategoriID + SicilNo + SirketKodu) tablosundan gelir.
            List<string> sicils;

            if (kategoriId.HasValue && kategoriId.Value > 0)
            {
                // Belirli kategori sorumluları (+ opsiyonel şirket) — referans PersonelGetirKat
                var q = _context.tb_TalepAyar.AsNoTracking()
                    .Where(a => a.KategoriID == kategoriId.Value);
                if (!string.IsNullOrEmpty(sirketKodu))
                {
                    q = q.Where(a => a.SirketKodu == sirketKodu);
                }
                sicils = q.Select(a => a.SicilNo).Distinct().ToList();
            }
            else
            {
                // Tür bazlı — referans PersonelGetirTur: tb_TalepAyar x tb_TalepKategori, TalepTurKodu filtresi
                string turUpper = (tur ?? "").ToUpper();
                var q = from a in _context.tb_TalepAyar.AsNoTracking()
                        join tk in _context.tb_TalepKategori.AsNoTracking() on a.KategoriID equals tk.TalepKategoriID
                        where string.IsNullOrEmpty(turUpper)
                                ? (tk.TalepTurKodu == "IT" || tk.TalepTurKodu == "ERP")
                                : tk.TalepTurKodu == turUpper
                        select a;
                if (!string.IsNullOrEmpty(sirketKodu))
                {
                    q = q.Where(a => a.SirketKodu == sirketKodu);
                }
                sicils = q.Select(a => a.SicilNo).Distinct().ToList();
            }

            return _context.tb_Personel
                .AsNoTracking()
                .Where(p => p.Durum == true && sicils.Contains(p.SicilNo))
                .OrderBy(p => p.AdSoyad)
                .Select(p => new Personel
                {
                    SicilNo = p.SicilNo,
                    AdSoyad = p.AdSoyad
                })
                .ToList();
        }

        public bool ToggleRequestLock(int kullaniciID, int talepID)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            if (t.SorumluSicil != user.SicilNo) return false;

            string konu;
            string detailMsg;
            if (t.Kilitli == true)
            {
                t.Kilitli = false;
                t.KilitTarihi = null;
                konu = "Talep Kilidi Kaldirildi";
                detailMsg = "Talebin kilidi kaldirildi.";
            }
            else
            {
                t.Kilitli = true;
                t.KilitTarihi = DateTime.Now;
                t.KilitSure = (t.KayitTar.HasValue ? (DateTime.Now - t.KayitTar.Value).TotalMinutes : 0);
                konu = "Talep Kilitlendi";
                detailMsg = "Talep kilitlendi.";
            }

            _context.SaveChanges();
            BelgeTarihceKaydet(t.TalepKodu, konu, $"{detailMsg} (Yapan: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SISTEM] {detailMsg}",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            return true;
        }

        public bool SendRequestForApproval(int kullaniciID, int talepID, string amirSicil)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            if (t.SorumluSicil != user.SicilNo) return false;

            var amir = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == amirSicil);
            if (amir == null) return false;

            if (_context.tb_TalepAmir.Any(a => a.TalepKodu == t.TalepKodu && a.Durum == null))
                return false;

            var ta = new tb_TalepAmir
            {
                TalepKodu = t.TalepKodu,
                KayitSicil = user.SicilNo,
                AmirSicil = amirSicil,
                KayitTar = DateTime.Now,
                IslemTur = "ONAY"
            };

            _context.tb_TalepAmir.Add(ta);
            _context.SaveChanges();

            BelgeTarihceKaydet(t.TalepKodu, "Talep İşlem Onayına Gönderildi", $"Onaya Gönderilen: {amir.AdSoyad} (Gönderen: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SISTEM] Talep, {amir.AdSoyad} onayına gönderildi.",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            _ = _pushNotificationService.SendToUserBySicilNoAsync(
                amirSicil,
                $"{(t.TalepTurKodu == "BAKIM" ? "Bakim" : t.TalepTurKodu)} Talebi Onay Istegi",
                $"'{t.Konu}' konulu talep ({t.TalepKodu}) onayınıza gönderildi.",
                new { type = t.TalepTurKodu, screen = "TalepScreen", code = t.TalepKodu, id = t.TalepID }
            );

            return true;
        }

        public bool RetractRequestApproval(int kullaniciID, int talepID)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            if (t.SorumluSicil != user.SicilNo) return false;

            var ta = _context.tb_TalepAmir.FirstOrDefault(a => a.TalepKodu == t.TalepKodu && a.Durum == null);
            if (ta == null) return false;

            string amirAd = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == ta.AmirSicil)?.AdSoyad ?? ta.AmirSicil;

            _context.tb_TalepAmir.Remove(ta);
            _context.SaveChanges();

            BelgeTarihceKaydet(t.TalepKodu, "İşlem Onayı Geri Çekildi", $"Onayı Geri Çekilen: {amirAd} (Yapan: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SISTEM] İşlem onayı geri çekildi (Onay bekleyen kişi: {amirAd}).",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            return true;
        }

        public bool ApproveOrRejectRequest(int kullaniciID, int talepID, bool approve, string comment)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            var ta = _context.tb_TalepAmir.FirstOrDefault(a => a.TalepKodu == t.TalepKodu && a.AmirSicil == user.SicilNo && a.Durum == null);
            if (ta == null) return false;

            ta.Durum = approve;
            ta.IslemTar = DateTime.Now;
            ta.Sure = (ta.KayitTar.HasValue ? (DateTime.Now - ta.KayitTar.Value).TotalMinutes : 0);
            _context.SaveChanges();

            string statusText = approve ? "İşlem Onayı Verildi" : "İşlem Onayı Reddedildi";
            string aciklamaMsg = string.IsNullOrEmpty(comment) ? $"{statusText}." : $"{statusText} ({comment}).";

            BelgeTarihceKaydet(t.TalepKodu, statusText, $"Amir: {user.AdSoyad}, Açıklama: {aciklamaMsg}");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[{statusText.ToUpper()}] {aciklamaMsg}",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            if (!string.IsNullOrEmpty(t.SorumluSicil))
            {
                _ = _pushNotificationService.SendToUserBySicilNoAsync(
                    t.SorumluSicil,
                    approve ? $"{(t.TalepTurKodu == "BAKIM" ? "Bakim" : t.TalepTurKodu)} Talebi Onaylandi" : $"{(t.TalepTurKodu == "BAKIM" ? "Bakim" : t.TalepTurKodu)} Talebi Reddedildi",
                    $"'{t.Konu}' konulu talebinize ({t.TalepKodu}) amiriniz tarafindan {(approve ? "onay" : "ret")} yaniti verildi.",
                    new { type = t.TalepTurKodu, screen = "TalepScreen", code = t.TalepKodu, id = t.TalepID }
                );
            }

            // Mail gönderimi (Talebi açan kişiye ve sorumluya)
            string onayDurumu = approve ? "onaylanmıştır" : "reddedilmiştir";
            string subject = approve ? "Talep Onaylandı" : "Talep Reddedildi";
            string body = $"Merhaba,<br/><br/>{t.TalepKodu} takip kodlu talebinize amir {user.AdSoyad} tarafından <b>{(approve ? "ONAY" : "RET")}</b> yanıtı verilmiştir.<br/><b>Açıklama:</b> {aciklamaMsg}<br/><br/>İyi çalışmalar dileriz.";

            var ownerEmail = GetUserEmailBySicil(t.KayitSicil);
            if (!string.IsNullOrEmpty(ownerEmail))
            {
                _ = _notificationService.SendMailAsync($"OyemCore {t.TalepTurKodu}", $"{t.TalepKodu} Nolu Talep {subject}", body, ownerEmail);
            }

            if (!string.IsNullOrEmpty(t.SorumluSicil) && t.SorumluSicil != t.KayitSicil)
            {
                var sorumluEmail = GetUserEmailBySicil(t.SorumluSicil);
                if (!string.IsNullOrEmpty(sorumluEmail))
                {
                    _ = _notificationService.SendMailAsync($"OyemCore {t.TalepTurKodu}", $"{t.TalepKodu} Nolu Talep {subject}", body, sorumluEmail);
                }
            }

            return true;
        }

        public bool AskQuestionToPersonnel(int kullaniciID, int talepID, string targetSicil, string questionText)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            if (t.SorumluSicil != user.SicilNo) return false;

            if (_context.tb_TalepSoruCevap.Any(sc => sc.TalepKodu == t.TalepKodu && sc.CevapTalepGelismeID == null))
                return false;

            var target = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == targetSicil);
            if (target == null) return false;

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SORU - Hedef: {target.AdSoyad}] {questionText}",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            var sc = new tb_TalepSoruCevap
            {
                TalepKodu = t.TalepKodu,
                SoruTalepGelismeID = gelisme.TalepGelismeID,
                Sicil = targetSicil,
                Eposta = target.Eposta
            };
            _context.tb_TalepSoruCevap.Add(sc);
            _context.SaveChanges();

            BelgeTarihceKaydet(t.TalepKodu, "Soru Kaydı Oluşturuldu", $"Soru sorulan: {target.AdSoyad} (Soran: {user.AdSoyad})");

            _ = _pushNotificationService.SendToUserBySicilNoAsync(
                targetSicil,
                $"{(t.TalepTurKodu == "BAKIM" ? "Bakim" : t.TalepTurKodu)} Talebi Hakkinda Soru",
                $"'{t.Konu}' konulu talep ({t.TalepKodu}) hakkinda sorumlu uzman size soru sordu.",
                new { type = t.TalepTurKodu, screen = "TalepScreen", code = t.TalepKodu, id = t.TalepID }
            );

            return true;
        }

        public bool AddHelperPersonnel(int kullaniciID, int talepID, string helperSicil)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            if (t.SorumluSicil != user.SicilNo && !IsKategoriYoneticisi(t, user)) return false;

            var helper = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == helperSicil);
            if (helper == null) return false;

            if (t.KayitSicil == helperSicil || t.SorumluSicil == helperSicil) return false;

            if (_context.tb_TalepBilgi.Any(b => b.TalepKodu == t.TalepKodu && b.BilgiSicil == helperSicil))
                return false;

            var tb = new tb_TalepBilgi
            {
                TalepKodu = t.TalepKodu,
                KayitSicil = user.SicilNo,
                BilgiSicil = helperSicil,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepBilgi.Add(tb);
            _context.SaveChanges();

            BelgeTarihceKaydet(t.TalepKodu, "Bilgi Personeli Eklendi", $"Yardımcı Eklenen: {helper.AdSoyad} (Yapan: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SISTEM] {helper.AdSoyad} yardimci personel (bilgi) olarak atandi.",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            return true;
        }

        public bool DeleteHelperPersonnel(int kullaniciID, int talepID, string helperSicil)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;
            if (t.Durum == true) return false;

            if (t.SorumluSicil != user.SicilNo && !IsKategoriYoneticisi(t, user)) return false;

            var tb = _context.tb_TalepBilgi.FirstOrDefault(b => b.TalepKodu == t.TalepKodu && b.BilgiSicil == helperSicil);
            if (tb == null) return false;

            var helperName = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == helperSicil)?.AdSoyad ?? helperSicil;

            _context.tb_TalepBilgi.Remove(tb);
            _context.SaveChanges();

            BelgeTarihceKaydet(t.TalepKodu, "Bilgi Personeli Silindi", $"Yardımcı Silinen: {helperName} (Yapan: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SISTEM] {helperName} yardımcı personel listesinden çıkarıldı.",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            return true;
        }

        public IEnumerable<Personel> GetAllActivePersonel()
        {
            return _context.tb_Personel
                .AsNoTracking()
                .Where(p => p.Durum == true)
                .OrderBy(p => p.AdSoyad)
                .Select(p => new Personel
                {
                    SicilNo = p.SicilNo,
                    AdSoyad = p.AdSoyad
                })
                .ToList();
        }

        private int SureHesapla(DateTime bas, DateTime bit)
        {
            // Mesai başlangıç ve bitiş saatleri
            TimeSpan mesaiBaslangic = new TimeSpan(8, 0, 0);
            TimeSpan mesaiBitis = new TimeSpan(17, 0, 0);

            // Geçen toplam dakika
            int toplamDakika = 0;

            // Başlangıç ve bitiş tarihlerinin yerlerini karşılaştır
            if (bas > bit)
            {
                // Tarih sıralaması ters ise yer değiştir
                DateTime temp = bas;
                bas = bit;
                bit = temp;
            }

            DateTime suankiTarih = bas;

            while (suankiTarih <= bit)
            {
                // Hafta içi olup olmadığını kontrol et
                if (suankiTarih.DayOfWeek != DayOfWeek.Saturday && suankiTarih.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Mesai saatleri içinde olup olmadığını kontrol et
                    DateTime mesaiBaslangicTarihi = suankiTarih.Date.Add(mesaiBaslangic);
                    DateTime mesaiBitisTarihi = suankiTarih.Date.Add(mesaiBitis);

                    // Başlangıç ve bitiş tarihleri arasında mesai saatine göre dakikayı hesapla
                    if (suankiTarih < mesaiBaslangicTarihi)
                        suankiTarih = mesaiBaslangicTarihi;

                    DateTime mesaiSonu = bit < mesaiBitisTarihi ? bit : mesaiBitisTarihi;

                    if (suankiTarih <= mesaiSonu)
                        toplamDakika += ((int)(mesaiSonu - suankiTarih).TotalMinutes);

                    // Mesai sonrasına geç
                    suankiTarih = suankiTarih.Date.AddDays(1);
                }
                else
                {
                    // Hafta sonu ise, bir sonraki güne geç
                    suankiTarih = suankiTarih.AddDays(1);
                }
            }

            return toplamDakika;
        }

        private int SureHesaplaBakim(DateTime bas, DateTime bit)
        {
            // Mesai başlangıç ve bitiş saatleri
            TimeSpan mesaiBaslangic = new TimeSpan(8, 0, 0);
            TimeSpan mesaiBitis = new TimeSpan(24, 0, 0);

            // Geçen toplam dakika
            int toplamDakika = 0;

            // Başlangıç ve bitiş tarihlerinin yerlerini karşılaştır
            if (bas > bit)
            {
                // Tarih sıralaması ters ise yer değiştir
                DateTime temp = bas;
                bas = bit;
                bit = temp;
            }

            DateTime suankiTarih = bas;

            while (suankiTarih <= bit)
            {
                // Hafta içi olup olmadığını kontrol et
                if (suankiTarih.DayOfWeek != DayOfWeek.Saturday && suankiTarih.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Mesai saatleri içinde olup olmadığını kontrol et
                    DateTime mesaiBaslangicTarihi = suankiTarih.Date.Add(mesaiBaslangic);
                    DateTime mesaiBitisTarihi = suankiTarih.Date.Add(mesaiBitis);

                    // Başlangıç ve bitiş tarihleri arasında mesai saatine göre dakikayı hesapla
                    if (suankiTarih < mesaiBaslangicTarihi)
                        suankiTarih = mesaiBaslangicTarihi;

                    DateTime mesaiSonu = bit < mesaiBitisTarihi ? bit : mesaiBitisTarihi;

                    if (suankiTarih <= mesaiSonu)
                        toplamDakika += ((int)(mesaiSonu - suankiTarih).TotalMinutes);

                    // Mesai sonrasına geç
                    suankiTarih = suankiTarih.Date.AddDays(1);
                }
                else
                {
                    // Hafta sonu ise, bir sonraki güne geç
                    suankiTarih = suankiTarih.AddDays(1);
                }
            }

            return toplamDakika;
        }

        // ── MTTR / Süre düşümü hesaplama (referans: WebServiceBakim.TalepSureHesaplaInternal) ──
        private class Interval { public DateTime Start; public DateTime End; }

        // Çakışan aralıkları mutlak zamana göre birleştirir (referans: ClsBelgeIslemleri.BirlesmisSureHesapla)
        private List<Interval> BirlesmisSureHesapla(List<Interval> intervals)
        {
            if (intervals == null || intervals.Count == 0) return new List<Interval>();
            var sorted = intervals.Where(i => i.Start < i.End).OrderBy(i => i.Start).ToList();
            if (sorted.Count == 0) return new List<Interval>();

            var merged = new List<Interval>();
            var current = new Interval { Start = sorted[0].Start, End = sorted[0].End };
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].Start <= current.End)
                {
                    if (sorted[i].End > current.End) current.End = sorted[i].End;
                }
                else
                {
                    merged.Add(current);
                    current = new Interval { Start = sorted[i].Start, End = sorted[i].End };
                }
            }
            merged.Add(current);
            return merged;
        }

        // Bir talebin süre kırılımını hesaplar: brüt çalışma süresi, onay/soru-cevap/iş emri
        // kesintileri (bilgi amaçlı ham toplamlar), birleştirilmiş net kesinti ve net MTTR.
        // Kapanmış talepler için kullanılır; hem TalepSureHesapla hem detay ekranı bunu çağırır.
        private (double toplam, double onay, double soru, double isEmri, double netKesinti, int netMttr)
            HesaplaSureKirilim(string talepKodu, DateTime kayitTar, DateTime kapanmaTar)
        {
            var onaylar = _context.tb_TalepAmir
                .Where(o => o.TalepKodu == talepKodu && o.Durum == true && o.IslemTar != null && o.KayitTar != null)
                .ToList();
            var sorular = _context.tb_TalepSoruCevap
                .Where(o => o.TalepKodu == talepKodu && o.Sure != null && o.SoruTalepGelismeID != null && o.CevapTalepGelismeID != null)
                .ToList();
            var gelismeler = _context.tb_TalepGelisme
                .Where(o => o.TalepKodu == talepKodu)
                .ToList()
                .GroupBy(g => g.TalepGelismeID)
                .ToDictionary(k => k.Key, v => v.First());
            var isEmirleri = _context.tb_TalepIsEmri
                .Where(o => o.TalepKodu == talepKodu && o.KapanmaTar != null && o.KayitTar != null)
                .ToList();

            var raw = new List<Interval>();
            double onaySure = 0, soruSure = 0, isEmriSure = 0;

            foreach (var o in onaylar)
            {
                raw.Add(new Interval { Start = o.KayitTar.Value, End = o.IslemTar.Value });
                onaySure += SureHesaplaBakim(o.KayitTar.Value, o.IslemTar.Value);
            }
            foreach (var s in sorular)
            {
                if (gelismeler.TryGetValue(s.SoruTalepGelismeID.Value, out var gs) && gs.KayitTar != null &&
                    gelismeler.TryGetValue(s.CevapTalepGelismeID.Value, out var gc) && gc.KayitTar != null)
                {
                    raw.Add(new Interval { Start = gs.KayitTar.Value, End = gc.KayitTar.Value });
                    soruSure += SureHesaplaBakim(gs.KayitTar.Value, gc.KayitTar.Value);
                }
            }
            foreach (var i in isEmirleri)
            {
                raw.Add(new Interval { Start = i.KayitTar.Value, End = i.KapanmaTar.Value });
                isEmriSure += SureHesaplaBakim(i.KayitTar.Value, i.KapanmaTar.Value);
            }

            var merged = BirlesmisSureHesapla(raw);
            double netKesinti = 0;
            foreach (var m in merged) netKesinti += SureHesaplaBakim(m.Start, m.End);

            double toplam = SureHesaplaBakim(kayitTar, kapanmaTar);
            int netMttr = (int)(toplam - netKesinti);
            if (netMttr < 0) netMttr = 0;

            return (toplam, onaySure, soruSure, isEmriSure, netKesinti, netMttr);
        }

        // Talep kapatıldığında net süreleri (kesintiler düşülmüş) hesaplar; hem tb_Talep
        // (MttrTamamSure/IslemSure/DurusSure) hem tb_TalepSureHesap (audit log) tablolarına yazar.
        // referans: WebServiceBakim.TalepSureHesaplaInternal
        private void TalepSureHesapla(string talepKodu)
        {
            var t = _context.tb_Talep.SingleOrDefault(o => o.TalepKodu == talepKodu);
            if (t == null || t.Durum != true || t.KapanmaTar == null || t.KayitTar == null) return;

            // 1. Bu talebe ait eski süre-hesap loglarını temizle
            var eskiLoglar = _context.tb_TalepSureHesap.Where(o => o.TalepKodu == talepKodu);
            _context.tb_TalepSureHesap.RemoveRange(eskiLoglar);
            _context.SaveChanges();

            // 2. Gerekli verileri çek (onay + soru-cevap + iş emri)
            var onaylar = _context.tb_TalepAmir
                .Where(o => o.TalepKodu == talepKodu && o.Durum == true && o.IslemTar != null && o.KayitTar != null)
                .ToList();
            var sorular = _context.tb_TalepSoruCevap
                .Where(o => o.TalepKodu == talepKodu && o.Sure != null && o.SoruTalepGelismeID != null && o.CevapTalepGelismeID != null)
                .ToList();
            var gelismeler = _context.tb_TalepGelisme
                .Where(o => o.TalepKodu == talepKodu).ToList()
                .GroupBy(g => g.TalepGelismeID).ToDictionary(k => k.Key, v => v.First());
            var isEmirleri = _context.tb_TalepIsEmri
                .Where(o => o.TalepKodu == talepKodu && o.KapanmaTar != null && o.KayitTar != null)
                .ToList();

            // 3. Düşülecek ham aralıkları topla
            var raw = new List<Interval>();
            foreach (var o in onaylar)
                raw.Add(new Interval { Start = o.KayitTar.Value, End = o.IslemTar.Value });
            foreach (var s in sorular)
                if (gelismeler.TryGetValue(s.SoruTalepGelismeID.Value, out var gs) && gs.KayitTar != null &&
                    gelismeler.TryGetValue(s.CevapTalepGelismeID.Value, out var gc) && gc.KayitTar != null)
                    raw.Add(new Interval { Start = gs.KayitTar.Value, End = gc.KayitTar.Value });
            foreach (var i in isEmirleri)
                raw.Add(new Interval { Start = i.KayitTar.Value, End = i.KapanmaTar.Value });

            // 4. Çakışanları birleştir; her grup için özet (SONUC) + ham detay satırlarını yaz
            var merged = BirlesmisSureHesapla(raw);
            double totalDeductible = 0;
            int grupID = 1;

            foreach (var m in merged)
            {
                double net = SureHesaplaBakim(m.Start, m.End);
                totalDeductible += net;

                _context.tb_TalepSureHesap.Add(new tb_TalepSureHesap
                {
                    TalepKodu = talepKodu,
                    BasTar = m.Start,
                    BitTar = m.End,
                    Tur = "SONUC",
                    Aciklama = "Birleştirilmiş Net Kesinti Süresi",
                    NetSure = (int)net,
                    SatirTipi = 1,
                    GrupID = grupID
                });

                foreach (var o in onaylar)
                    if (o.KayitTar.Value < m.End && o.IslemTar.Value > m.Start)
                        _context.tb_TalepSureHesap.Add(new tb_TalepSureHesap
                        {
                            TalepKodu = talepKodu,
                            BasTar = o.KayitTar.Value,
                            BitTar = o.IslemTar.Value,
                            Tur = "ONAY",
                            Aciklama = "Talep Onay Süresi",
                            RefID = o.TalepAmirID,
                            NetSure = (int)SureHesaplaBakim(o.KayitTar.Value, o.IslemTar.Value),
                            SatirTipi = 0,
                            GrupID = grupID
                        });

                foreach (var s in sorular)
                    if (gelismeler.TryGetValue(s.SoruTalepGelismeID.Value, out var gs) && gs.KayitTar != null &&
                        gelismeler.TryGetValue(s.CevapTalepGelismeID.Value, out var gc) && gc.KayitTar != null &&
                        gs.KayitTar.Value < m.End && gc.KayitTar.Value > m.Start)
                        _context.tb_TalepSureHesap.Add(new tb_TalepSureHesap
                        {
                            TalepKodu = talepKodu,
                            BasTar = gs.KayitTar.Value,
                            BitTar = gc.KayitTar.Value,
                            Tur = "SORUCEVAP",
                            Aciklama = "Soru-Cevap Süresi",
                            RefID = s.TalepSoruCevapID,
                            NetSure = (int)SureHesaplaBakim(gs.KayitTar.Value, gc.KayitTar.Value),
                            SatirTipi = 0,
                            GrupID = grupID
                        });

                foreach (var i in isEmirleri)
                    if (i.KayitTar.Value < m.End && i.KapanmaTar.Value > m.Start)
                        _context.tb_TalepSureHesap.Add(new tb_TalepSureHesap
                        {
                            TalepKodu = talepKodu,
                            BasTar = i.KayitTar.Value,
                            BitTar = i.KapanmaTar.Value,
                            Tur = "ISEMRI",
                            Aciklama = i.Aciklama,
                            RefID = i.TalepIsEmriID,
                            NetSure = (int)SureHesaplaBakim(i.KayitTar.Value, i.KapanmaTar.Value),
                            SatirTipi = 0,
                            GrupID = grupID
                        });

                grupID++;
            }

            // 5. Net MTTR = brüt mesai süresi − birleştirilmiş net kesinti
            double totalWorking = SureHesaplaBakim(t.KayitTar.Value, t.KapanmaTar.Value);
            int finalMttr = (int)(totalWorking - totalDeductible);
            if (finalMttr < 0) finalMttr = 0;

            t.MttrTamamSure = finalMttr;
            t.IslemSure = finalMttr;

            var tb = _context.tb_TalepBakim.SingleOrDefault(o => o.TalepKodu == talepKodu);
            t.DurusSure = (tb != null && tb.UretimDurusu != "H") ? finalMttr : 0;
        }

        public bool TalepKontrolKaydet(int kullaniciID, string talepKodu, string eksikSomun, string yag, string miknatis, string fazlaParca, string guvenlik, string makine, string temizlik, string gida)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new InvalidOperationException("Kullanici bulunamadi.");

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var t = _context.tb_Talep.SingleOrDefault(o => o.TalepKodu == talepKodu);
                    if (t == null) throw new InvalidOperationException("Talep bulunamadı.");

                    var tb = _context.tb_TalepBakim.SingleOrDefault(o => o.TalepKodu == talepKodu);
                    if (tb != null)
                    {
                        tb.EksikSomunDurum = eksikSomun;
                        tb.YagDurum = yag;
                        tb.MiknatisDurum = miknatis;
                        tb.FazlaParcaDurum = fazlaParca;
                        tb.GuvRiskDurum = guvenlik;
                        tb.MakineDurum = makine;
                        tb.TemizlikDurum = temizlik;
                        tb.GidaRiskDurum = gida;
                    }

                    var ta = _context.tb_TalepAmir.SingleOrDefault(o => o.TalepKodu == talepKodu && o.AmirSicil == user.SicilNo && o.Durum == null && o.IslemTur == "ONAY");
                    if (ta != null)
                    {
                        ta.Durum = true;
                        ta.IslemTar = DateTime.Now;
                        if (ta.KayitTar.HasValue)
                            ta.Sure = SureHesaplaBakim(ta.KayitTar.Value, ta.IslemTar.Value);
                    }
                    else
                    {
                        throw new InvalidOperationException("Onay kaydı bulunamadı.");
                    }

                    t.Durum = true;
                    t.KapanmaTar = DateTime.Now;

                    // Önce kaydet ki TalepSureHesapla, onay/iş emri kayıtlarını (Durum=true,
                    // IslemTar) DB'den okuyup kesintileri doğru düşebilsin.
                    _context.SaveChanges();

                    // Süre düşümleri: onay + soru-cevap + iş emri süreleri işlem süresinden
                    // düşülerek net MTTR / IslemSure / DurusSure hesaplanır.
                    TalepSureHesapla(talepKodu);
                    _context.SaveChanges();

                    // Referans: WebPortal WebServiceBakim.TalepKontrolKaydet — form dolduruldu/kapatıldı tarihçe kaydı.
                    BelgeTarihceKaydet(t.TalepKodu, "Talep Kontrol Formu Dolduruldu ve Kapatıldı.", $"İşlem Yapan: {user.AdSoyad}");

                    // Temizlik formu (son onay) sonrası talep sorumlusuna bildir (onaylayan hariç).
                    if (!string.IsNullOrEmpty(t.SorumluSicil) && t.SorumluSicil != user.SicilNo)
                    {
                        _ = _pushNotificationService.SendToUserBySicilNoAsync(
                            t.SorumluSicil,
                            "Bakım Talebi Form Onaylandı",
                            $"{t.TalepKodu} talebinin kapanış formu {user.AdSoyad} tarafından onaylandı.",
                            new { type = "BAKIM", screen = "TalepScreen", code = t.TalepKodu, id = t.TalepID }
                        );
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

        public IEnumerable<tb_IsEmriTur> GetIsEmriTurleri()
        {
            return _context.tb_IsEmriTur.Where(o => o.Durum == true).ToList();
        }

        public bool IsEmriKaydet(int kullaniciID, string talepKodu, int isEmriTurID, DateTime terminTar, string aciklama, string dosyaUrl)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var isEmri = new tb_TalepIsEmri
            {
                TalepKodu = talepKodu,
                IsEmriTurID = isEmriTurID,
                Aciklama = aciklama,
                TerminTar = terminTar,
                DosyaUrl = dosyaUrl,
                Sicil = user.SicilNo,
                KayitTar = DateTime.Now,
                Durum = false
            };

            _context.tb_TalepIsEmri.Add(isEmri);
            _context.SaveChanges();
            return true;
        }

        public bool IsEmriKapat(int kullaniciID, int isEmriID, string aciklama)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var isEmri = _context.tb_TalepIsEmri.SingleOrDefault(o => o.TalepIsEmriID == isEmriID);
            if (isEmri == null) return false;

            isEmri.KapanmaTar = DateTime.Now;
            isEmri.SonAciklama = aciklama;
            isEmri.Durum = true;
            
            if (isEmri.KayitTar.HasValue)
                isEmri.IslemSure = SureHesaplaBakim(isEmri.KayitTar.Value, isEmri.KapanmaTar.Value);

            _context.SaveChanges();
            return true;
        }

        public bool IsEmriAksiyonGonder(int kullaniciID, int isEmriID, string sicil)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var isEmri = _context.tb_TalepIsEmri.SingleOrDefault(o => o.TalepIsEmriID == isEmriID);
            if (isEmri == null) return false;

            isEmri.Sicil = sicil;
            _context.SaveChanges();

            // İş emri atanan kişiye bildirim (atayan hariç).
            if (!string.IsNullOrEmpty(sicil) && sicil != user.SicilNo)
            {
                _ = _pushNotificationService.SendToUserBySicilNoAsync(
                    sicil,
                    "İş Emri Size Atandı",
                    $"{isEmri.TalepKodu} talebine bağlı bir iş emri size atandı.",
                    new { type = "BAKIM", screen = "TalepScreen", code = isEmri.TalepKodu }
                );
            }
            return true;
        }

        public (bool Success, bool PendingApproval, string PendingApprovalAdSoyad) UpdateRequestStatus(int kullaniciID, int talepID, string status)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return (false, false, null);

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return (false, false, null);

            // Istemci "KAPATILDI" gonderiyor. Onceki karsilastirma "Kapali" bekledigi icin
            // kapatma hic calismiyor, talep sessizce "yeniden acildi" dalina dusuyordu.
            // Tanimsiz bir deger artik sessizce gecmez, hata firlatir.
            var durumKodu = (status ?? "").Trim().ToUpperInvariant();
            bool kapatiliyor = durumKodu == "KAPATILDI" || durumKodu == "KAPALI";
            bool yenidenAciliyor = durumKodu == "ACIK" || durumKodu == "ACILDI";

            if (!kapatiliyor && !yenidenAciliyor)
                throw new InvalidOperationException($"Geçersiz talep durumu: {status}");

            bool pendingApproval = false;
            string pendingAdSoyad = null;

            if (kapatiliyor)
            {
                // Referans (WebServiceBakim.TalepIslem): kapatilmis talep uzerinden
                // islem yapilamaz.
                if (t.Durum == true)
                    throw new InvalidOperationException("Kapatılan talep üzerinden işlem yapılamaz.");

                // Referans: bekleyen onay sureci varsa islem menusunden kapatilamaz,
                // once onaylanmasi gerekir.
                if (_context.tb_TalepAmir.Any(a => a.TalepKodu == t.TalepKodu && a.Durum == null))
                    throw new InvalidOperationException("Bu talep için bekleyen onay süreci var. Önce onaylanması gerekir.");

                if (string.IsNullOrEmpty(t.SorumluSicil))
                    throw new InvalidOperationException("Sorumlu atanmamış bir talep kapatılamaz.");

                // Referans: WebPortal WebServiceBakim.TalepGelismeVeKapama — BAKIM talepleri için,
                // sorumlu değilse, o talep kategorisinde (ve şirketinde) tb_TalepAyar.YoneticiMi=true
                // olan bir kategori yöneticisi de kapatabilir. Diğer talep türleri (IT vb.) için
                // önceki davranış (sadece sorumlu) korunur.
                bool kapatanYonetici = false;
                if (t.SorumluSicil != user.SicilNo)
                {
                    if (!IsKategoriYoneticisi(t, user))
                        throw new InvalidOperationException("Talebi yalnızca sorumlu kişi kapatabilir.");

                    kapatanYonetici = true;
                }

                // Acik is emri varsa kapatilamaz (tum turler icin gecerli)
                var acikIsEmri = _context.tb_TalepIsEmri.Any(i => i.TalepKodu == t.TalepKodu && i.KapanmaTar == null);
                if (acikIsEmri)
                    throw new InvalidOperationException("Kapatılmamış iş emirleri varken talep kapatılamaz.");

                if (t.TalepTurKodu == "BAKIM")
                {

                    t.Durum = false;
                    var formOnayi = new tb_TalepAmir
                    {
                        TalepKodu = t.TalepKodu,
                        AmirSicil = t.KayitSicil,
                        IslemTur = "ONAY",
                        KayitTar = DateTime.Now
                    };
                    _context.tb_TalepAmir.Add(formOnayi);
                    BelgeTarihceKaydet(t.TalepKodu, "Form Onayına Gönderildi" + (kapatanYonetici ? " (Talep Yöneticisi)" : ""), $"Talep sahibi {t.KayitSicil} form onayı bekleniyor.");

                    // Client'a "talep tamamlandı" değil, gerçek durumu (kime onaya gittiği)
                    // göstermesi için — referans: WebPortal "kapatılması için onaya gönderildi".
                    pendingApproval = true;
                    pendingAdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == t.KayitSicil)?.AdSoyad ?? t.KayitSicil;

                    // Talep sahibine "onayınıza gönderildi" bildirimi (zil). Push + mail,
                    // aşağıdaki genel "talep sahibine durum bildirimi" bloğunda tek yerden
                    // gönderiliyor (burada ayrıca göndermek aynı olay için çift push'a yol açıyordu).
                    _bildirim.AddNotification(t.KayitSicil, "Bakım Talebi Onayınızda",
                        $"'{t.Konu}' konulu talep ({t.TalepKodu}) form onayınıza gönderildi.",
                        "", "Bakim", t.TalepID.ToString(), kullaniciID);
                }
                else
                {
                    t.Durum = true;
                    t.KapanmaTar = DateTime.Now;
                    if (t.KayitTar.HasValue)
                        t.MttrTamamSure = SureHesapla(t.KayitTar.Value, t.KapanmaTar.Value);
                    
                    BelgeTarihceKaydet(t.TalepKodu, "Talep Kapatıldı", $"Talep {user.AdSoyad} tarafından kapatıldı.");
                }
            }
            else
            {
                t.Durum = false;
                t.KapanmaTar = null;
                t.MttrTamamSure = null;
                BelgeTarihceKaydet(t.TalepKodu, "Talep Yeniden Açıldı", $"Talep {user.AdSoyad} tarafından yeniden açıldı.");

                // Mail gönderimi (Yeniden Açıldı)
                var ownerEmail = GetUserEmailBySicil(t.KayitSicil);
                if (!string.IsNullOrEmpty(ownerEmail))
                {
                    _ = _notificationService.SendMailAsync($"OyemCore {t.TalepTurKodu}", $"{t.TalepKodu} Nolu Talebiniz Yeniden Açıldı", 
                        $"Merhaba,<br/><br/>{t.TalepKodu} nolu talebiniz yeniden açılmıştır.<br/><b>İşlem Yapan:</b> {user.AdSoyad}<br/><br/>İyi çalışmalar dileriz.", ownerEmail);
                }
            }

            _context.SaveChanges();

            // Talep sahibine durum bildirimi (işlemi yapan hariç).
            if (kapatiliyor && !string.IsNullOrEmpty(t.KayitSicil) && t.KayitSicil != user.SicilNo)
            {
                var mesaj = t.TalepTurKodu == "BAKIM"
                    ? $"{t.TalepKodu} talebiniz form onayınıza sunuldu."
                    : $"{t.TalepKodu} talebiniz kapatıldı.";
                _ = _pushNotificationService.SendToUserBySicilNoAsync(
                    t.KayitSicil,
                    $"{(t.TalepTurKodu == "BAKIM" ? "Bakım" : t.TalepTurKodu)} Talebi Güncellendi",
                    mesaj,
                    new { type = t.TalepTurKodu, screen = "TalepScreen", code = t.TalepKodu, id = t.TalepID }
                );

                // Mail gönderimi (Talep Sahibine)
                var ownerEmail = GetUserEmailBySicil(t.KayitSicil);
                string smsIcerik;
                if (t.TalepTurKodu == "BAKIM")
                {
                    smsIcerik = $"{t.TalepKodu} nolu bakım talebiniz tamamlanmış ve form onayınıza sunulmuştur.";
                    if (!string.IsNullOrEmpty(ownerEmail))
                    {
                        _ = _notificationService.SendMailAsync("OyemCore Bakım", $"{t.TalepKodu} Bakım Talebi Form Onayınızda",
                            $"Merhaba,<br/><br/>{smsIcerik}<br/><b>Kapayan Sorumlu:</b> {user.AdSoyad}<br/><br/>İyi çalışmalar dileriz.", ownerEmail);
                    }
                }
                else
                {
                    smsIcerik = $"{t.TalepKodu} nolu talebiniz kapatılmıştır.";
                    if (!string.IsNullOrEmpty(ownerEmail))
                    {
                        _ = _notificationService.SendMailAsync($"OyemCore {t.TalepTurKodu}", $"{t.TalepKodu} Nolu Talebiniz Kapatıldı",
                            $"Merhaba,<br/><br/>{smsIcerik}<br/><b>Kapayan Sorumlu:</b> {user.AdSoyad}<br/><br/>İyi çalışmalar dileriz.", ownerEmail);
                    }
                }

                // SMS gönderimi (Talep Sahibine) — tb_Sms kuyruğuna kayıt atar, mevcut
                // Windows Servis gönderir (referans: WebPortal ClsMail.SmsGonder ile aynı desen).
                var ownerPhone = GetUserPhoneBySicil(t.KayitSicil);
                if (!string.IsNullOrEmpty(ownerPhone))
                {
                    var ownerAdSoyad = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == t.KayitSicil)?.AdSoyad ?? t.KayitSicil;
                    _ = _notificationService.SendSmsAsync($"OyemCore {t.TalepTurKodu}", $"{t.TalepKodu} Talebi Güncellendi", smsIcerik, ownerPhone, ownerAdSoyad);
                }
            }
            return (true, pendingApproval, pendingAdSoyad);
        }

        public bool AssignRequest(int kullaniciID, int talepID, string sicilNo)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var request = _context.tb_Talep.FirstOrDefault(t => t.TalepID == talepID);
            if (request == null) return false;

            var assignee = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.SicilNo == sicilNo);
            string name = assignee != null ? assignee.AdSoyad : sicilNo;
            string email = assignee?.Eposta ?? "";

            request.SorumluSicil = sicilNo;
            request.SorumluEposta = email;
            _context.SaveChanges();

            BelgeTarihceKaydet(request.TalepKodu, "Talep Ataması Yapıldı", $"Sorumlu: {name} (Atayan: {user.AdSoyad})");

            // Sorumlu olarak atanan kişiye push (kendine atamada gönderme).
            if (!string.IsNullOrEmpty(sicilNo) && sicilNo != user.SicilNo)
            {
                _ = _pushNotificationService.SendToUserBySicilNoAsync(
                    sicilNo,
                    $"{(request.TalepTurKodu == "BAKIM" ? "Bakım" : request.TalepTurKodu)} Talebi Size Atandı",
                    $"'{request.Konu}' konulu talebe ({request.TalepKodu}) sorumlu olarak atandınız.",
                    new { type = request.TalepTurKodu, screen = "TalepScreen", code = request.TalepKodu, id = request.TalepID }
                );
            }

            // Talep sahibine de bildir (atayan ya da sorumlu değilse). Referans: atama → sorumlu + talep sahibi.
            if (!string.IsNullOrEmpty(request.KayitSicil) && request.KayitSicil != user.SicilNo && request.KayitSicil != sicilNo)
            {
                _ = _pushNotificationService.SendToUserBySicilNoAsync(
                    request.KayitSicil,
                    $"{(request.TalepTurKodu == "BAKIM" ? "Bakım" : request.TalepTurKodu)} Talebinize Sorumlu Atandı",
                    $"'{request.Konu}' konulu talebinize ({request.TalepKodu}) sorumlu olarak {name} atandı.",
                    new { type = request.TalepTurKodu, screen = "TalepScreen", code = request.TalepKodu, id = request.TalepID }
                );
            }

            // Mail gönderimi (Sorumlu Atandıysa Sorumluya)
            if (!string.IsNullOrEmpty(sicilNo))
            {
                var sorumluEposta = GetUserEmailBySicil(sicilNo);
                if (!string.IsNullOrEmpty(sorumluEposta))
                {
                    _ = _notificationService.SendMailAsync($"OyemCore {request.TalepTurKodu}", $"{request.TalepKodu} Nolu Talep Size Atandı", 
                        $"Merhaba,<br/><br/>{request.TalepKodu} nolu talep üzerinize atanmıştır.<br/><b>Atayan:</b> {user.AdSoyad}<br/><b>Konu:</b> {request.Konu}<br/><b>Açıklama:</b> {request.Aciklama}<br/><br/>İyi çalışmalar dileriz.", sorumluEposta);
                }
            }

            return true;
        }

        public bool AddRequestGelisme(int kullaniciID, int talepID, string aciklama, string dosyaUrl = null)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var request = _context.tb_Talep.AsNoTracking().FirstOrDefault(t => t.TalepID == talepID);
            if (request == null) return false;

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = request.TalepKodu,
                Aciklama = aciklama,
                DosyaUrl = dosyaUrl,
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };

            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            BelgeTarihceKaydet(request.TalepKodu, "Gelişme Eklendi", $"Gelişme Eklendi. (Ekleyen: {user.AdSoyad})");

            // Talep sahibi ve sorumlusuna gelişme bildirimi (ekleyen hariç).
            var hedefSiciller = new List<string>();
            if (!string.IsNullOrEmpty(request.KayitSicil) && request.KayitSicil != user.SicilNo)
                hedefSiciller.Add(request.KayitSicil);
            if (!string.IsNullOrEmpty(request.SorumluSicil) && request.SorumluSicil != user.SicilNo && request.SorumluSicil != request.KayitSicil)
                hedefSiciller.Add(request.SorumluSicil);
            foreach (var hedef in hedefSiciller)
            {
                _ = _pushNotificationService.SendToUserBySicilNoAsync(
                    hedef,
                    $"{(request.TalepTurKodu == "BAKIM" ? "Bakım" : request.TalepTurKodu)} Talebine Gelişme",
                    $"{user.AdSoyad}, {request.TalepKodu} talebine bir gelişme ekledi.",
                    new { type = request.TalepTurKodu, screen = "TalepScreen", code = request.TalepKodu, id = request.TalepID }
                );

                // Mail gönderimi
                var hedefEposta = GetUserEmailBySicil(hedef);
                if (!string.IsNullOrEmpty(hedefEposta))
                {
                    _ = _notificationService.SendMailAsync($"OyemCore {request.TalepTurKodu}", $"{request.TalepKodu} Talebine Gelişme Notu Eklendi", 
                        $"Merhaba,<br/><br/>{request.TalepKodu} takip kodlu talebe yeni bir gelişme notu eklenmiştir.<br/><b>Ekleyen:</b> {user.AdSoyad}<br/><b>Gelişme Notu:</b> {aciklama}<br/><br/>İyi çalışmalar dileriz.", hedefEposta);
                }
            }

            return true;
        }
    }
}
