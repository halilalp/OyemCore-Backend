using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebPortalSpace.BusinessLayer.Common;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.DataLayer.Interfaces;
using WebPortalSpace.BusinessLayer.Dtos;

namespace WebPortalSpace.BusinessLayer.Services
{
    public class TalepService : ITalepService
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;

        public TalepService(IYbsDbContext context, IPushNotificationService pushNotificationService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
        }

        private bool HasAuthority(string adminBelgeTur, string turKodu)
        {
            if (string.IsNullOrEmpty(adminBelgeTur)) return false;
            var tokens = adminBelgeTur.Split('*', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpper());
            return tokens.Contains("ADMIN") || tokens.Contains("TICKET") || tokens.Contains(turKodu.ToUpper());
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
                                 select new { t, tk, p1, p2, tb, ts, tb_blm, tm };

                if (!isAdmin)
                {
                    queryBakim = queryBakim.Where(x => x.t.KayitSicil == user.SicilNo || x.t.SorumluSicil == user.SicilNo);
                }

                var list = queryBakim.OrderByDescending(x => x.t.KayitTar).ToList();

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
                    SorumluAd = o.p2 != null ? o.p2.AdSoyad : "Atanmamış",
                    Durum = o.t.Durum == true ? "Kapalı" : "Açık",
                    KapanmaTarStr = o.t.KapanmaTar != null ? o.t.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    IsMine = o.t.SorumluSicil == user.SicilNo,
                    SirketAdi = o.ts != null ? o.ts.SirketAdi : null,
                    BolumAdi = o.tb_blm != null ? o.tb_blm.BolumAdi : null,
                    MakineAdi = o.tm != null ? o.tm.MakineAdi : null,
                    UretimDurusu = o.tb != null ? o.tb.UretimDurusu : null,
                    TalepPuan = o.t.TalepPuan,
                    PuanRenk = ClsYardim.BakimPuanRenk(o.t.TalepPuan)
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
                                   select new { t, tk, p1, p2 };

                if (!isAdmin)
                {
                    queryGeneric = queryGeneric.Where(x => x.t.KayitSicil == user.SicilNo || x.t.SorumluSicil == user.SicilNo);
                }

                var list = queryGeneric.OrderByDescending(x => x.t.KayitTar).ToList();

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
                    SorumluAd = o.p2 != null ? o.p2.AdSoyad : "Atanmamış",
                    Durum = o.t.Durum == true ? "Kapalı" : "Açık",
                    KapanmaTarStr = o.t.KapanmaTar != null ? o.t.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    IsMine = o.t.SorumluSicil == user.SicilNo,
                    SirketAdi = (string)null,
                    BolumAdi = (string)null,
                    MakineAdi = (string)null,
                    UretimDurusu = (string)null,
                    TalepPuan = (int?)null,
                    PuanRenk = (string)null
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

            // Giriş yapan kullanıcının rolü
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == kullaniciID);
            string girisTur = "HATA";
            if (user != null)
            {
                if (user.Eposta == item.t.KayitEposta)
                    girisTur = "SAHIP";
                else if (user.Eposta == item.t.SorumluEposta)
                    girisTur = "SORUMLU";
                else if (_context.tb_TalepAmir.Any(o => o.TalepKodu == code && o.AmirSicil == user.SicilNo && o.Durum == null))
                    girisTur = "ONAY";
                else if (HasAuthority(user.AdminBelgeTur, item.t.TalepTurKodu))
                    girisTur = "HAVUZ";
                else if (_context.tb_TalepAmir.Any(o => o.TalepKodu == code && o.AmirSicil == user.SicilNo))
                    girisTur = "BILGI";
                else if (_context.tb_TalepBilgi.Any(o => o.TalepKodu == code && o.BilgiSicil == user.SicilNo))
                    girisTur = "BILGI";
            }

            // Yardımcı Personel Listesi
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

            // Onay Geçmişi ve Aktif Onaycı
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

            // Soru Geçmişi ve Aktif Cevaplanmamış Soru
            var soruList = (from sc in _context.tb_TalepSoruCevap
                            join p in _context.tb_Personel on sc.Sicil equals p.SicilNo
                            where sc.TalepKodu == code
                            orderby sc.TalepSoruCevapID descending
                            select new
                            {
                                sc.TalepSoruCevapID,
                                sc.Sicil,
                                AdSoyad = p.AdSoyad,
                                sc.Eposta,
                                sc.CevapTalepGelismeID,
                                IsAnswered = sc.CevapTalepGelismeID != null,
                                SoruMetni = _context.tb_TalepGelisme.FirstOrDefault(g => g.TalepGelismeID == sc.SoruTalepGelismeID) != null
                                    ? _context.tb_TalepGelisme.FirstOrDefault(g => g.TalepGelismeID == sc.SoruTalepGelismeID).Aciklama
                                    : ""
                            }).ToList();

            var activeSoru = soruList.FirstOrDefault(s => s.CevapTalepGelismeID == null);

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
                    SorumluAd = item.p2 != null ? item.p2.AdSoyad : "Atanmamış",
                    Durum = item.t.Durum == true ? "Kapalı" : "Açık",
                    KapanmaTarStr = item.t.KapanmaTar != null ? item.t.KapanmaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    // Kilit durumları
                    Kilitli = item.t.Kilitli,
                    KilitTarStr = item.t.KilitTarihi != null ? (item.t.KilitTarihi.Value.ToString("dd.MM.yyyy HH:mm") + " (" + (item.t.KilitSure ?? 0).ToString("N0") + " DK)") : "",
                    // Bakim details
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
                Gelismeler = gelismeler,
                Tarihce = history,
                GirisTur = girisTur,
                BilgiPersonelleri = bilgiPersonelleri,
                OnayBilgisi = activeOnay,
                SoruBilgisi = activeSoru
            };
        }

        public string SaveRequest(int kullaniciID, tb_Talep request, tb_TalepBakim bakim = null)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");

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
                        int totalCount = _context.tb_Talep.Count(t => t.TalepTurKodu == request.TalepTurKodu);
                        string code = $"{request.TalepTurKodu}-{DateTime.Now:yyyyMMdd}-{totalCount + 1:000}";

                        request.TalepKodu = code;
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
                                request.MtbfAralikSure = (int)(request.KayitTar.Value - oncekiTalep.KayitTar.Value).TotalMinutes;
                            }
                            else
                            {
                                request.MtbfAralikSure = 0;
                            }
                        }

                        _context.tb_Talep.Add(request);
                        _context.SaveChanges();

                        if (request.TalepTurKodu == "BAKIM" && bakim != null)
                        {
                            bakim.TalepKodu = code;
                            _context.tb_TalepBakim.Add(bakim);
                            _context.SaveChanges();
                        }

                        BelgeTarihceKaydet(code, "Talep Oluşturuldu", $"Yeni talep kaydı açıldı. (Yapan: {user.AdSoyad})");
                        _ = _pushNotificationService.NotifyNewTalepAsync(request.TalepID);
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
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool UpdateRequestStatus(int kullaniciID, int talepID, string status)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return false;

            var request = _context.tb_Talep.FirstOrDefault(t => t.TalepID == talepID);
            if (request == null) return false;

            bool close = status.ToUpper() == "KAPALI" || status.ToUpper() == "TAMAM" || status.ToUpper() == "TAMAMLANDI" || status.ToUpper() == "KAPATILDI";
            DateTime? kapanmaTar = close ? DateTime.Now : (DateTime?)null;

            request.Durum = close;
            request.KapanmaTar = kapanmaTar;
            if (close && request.KayitTar.HasValue)
            {
                request.MttrTamamSure = (int)(DateTime.Now - request.KayitTar.Value).TotalMinutes;
            }
            else if (!close)
            {
                request.MttrTamamSure = null;
            }
            _context.SaveChanges();

            string detailMsg = close ? "Talep kapatıldı." : "Talep yeniden açıldı.";
            BelgeTarihceKaydet(request.TalepKodu, "Durum Değişikliği", $"{detailMsg} (Yapan: {user.AdSoyad})");
            if (close)
            {
                _ = _pushNotificationService.NotifyTalepClosedAsync(request.TalepID);
            }

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = request.TalepKodu,
                Aciklama = $"[SİSTEM] {detailMsg}",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            return true;
        }

        public bool AssignRequest(int kullaniciID, int talepID, string sicilNo)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return false;

            var request = _context.tb_Talep.FirstOrDefault(t => t.TalepID == talepID);
            if (request == null) return false;

            var assignee = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.SicilNo == sicilNo);

            string name = assignee != null ? assignee.AdSoyad : sicilNo;
            string email = assignee?.Eposta ?? "";

            request.SorumluSicil = sicilNo;
            request.SorumluEposta = email;
            _context.SaveChanges();

            BelgeTarihceKaydet(request.TalepKodu, "Sorumlu Ataması", $"Talep sorumlu uzmanı atandı: {name} (Atayan: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = request.TalepKodu,
                Aciklama = $"[SİSTEM] Talep {name} personeline atandı.",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            _ = _pushNotificationService.NotifyTalepSorumluAtandiAsync(request.TalepID);

            return true;
        }

        public bool AddRequestGelisme(int kullaniciID, int talepID, string aciklama)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return false;

            var request = _context.tb_Talep.FirstOrDefault(t => t.TalepID == talepID);
            if (request == null) return false;

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = request.TalepKodu,
                Aciklama = aciklama,
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            BelgeTarihceKaydet(request.TalepKodu, "Gelişme Notu Eklendi", $"Gelişme kaydı eklendi. (Yapan: {user.AdSoyad})");

            // Soru-Cevap kontrolü: Eğer kullanıcının bekleyen bir sorusu varsa bunu cevapla.
            var pendingQuestion = _context.tb_TalepSoruCevap
                .FirstOrDefault(sc => sc.TalepKodu == request.TalepKodu && sc.Sicil == user.SicilNo && sc.CevapTalepGelismeID == null);

            if (pendingQuestion != null)
            {
                pendingQuestion.CevapTalepGelismeID = gelisme.TalepGelismeID;
                var questionGelisme = _context.tb_TalepGelisme.FirstOrDefault(g => g.TalepGelismeID == pendingQuestion.SoruTalepGelismeID);
                if (questionGelisme != null && questionGelisme.KayitTar.HasValue)
                {
                    pendingQuestion.Sure = (DateTime.Now - questionGelisme.KayitTar.Value).TotalMinutes;
                }
                _context.SaveChanges();

                BelgeTarihceKaydet(request.TalepKodu, "Cevap Kaydı Oluşturuldu", $"Soru soran bilgilendirildi. (Yapan: {user.AdSoyad})");
            }

            _ = _pushNotificationService.NotifyTalepGelismeAsync(request.TalepID, user.KullaniciID, aciklama);

            return true;
        }

        private void BelgeTarihceKaydet(string code, string konu, string aciklama)
        {
            try
            {
                var history = new tb_BelgeTarihce
                {
                    BelgeKodu = code,
                    Konu = konu,
                    Aciklama = aciklama,
                    KayitTar = DateTime.Now
                };
                _context.tb_BelgeTarihce.Add(history);
                _context.SaveChanges();
            }
            catch { }
        }

        public IEnumerable<Personel> GetPersonels(string tur)
        {
            var query = _context.tb_Kullanici.AsNoTracking();
            List<string> users;

            if (!string.IsNullOrEmpty(tur))
            {
                string turUpper = tur.ToUpper();
                users = query
                    .Where(k => k.AdminBelgeTur != null && (k.AdminBelgeTur.ToUpper().Contains(turUpper) || k.AdminBelgeTur.ToUpper().Contains("ADMIN")))
                    .Select(k => k.SicilNo)
                    .ToList();
            }
            else
            {
                users = query
                    .Where(k => k.AdminBelgeTur != null && (k.AdminBelgeTur.ToUpper().Contains("TICKET") || 
                                                           k.AdminBelgeTur.ToUpper().Contains("ADMIN") ||
                                                           k.AdminBelgeTur.ToUpper().Contains("IT") ||
                                                           k.AdminBelgeTur.ToUpper().Contains("ERP")))
                    .Select(k => k.SicilNo)
                    .ToList();
            }

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
                konu = "Talep Kilidi Kaldırıldı";
                detailMsg = "Talebin kilidi kaldırıldı.";
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
                Aciklama = $"[SİSTEM] {detailMsg}",
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

            BelgeTarihceKaydet(t.TalepKodu, "Talep, İşlem Onayına Gönderildi", $"Onaya Gönderilen: {amir.AdSoyad} (Gönderen: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SİSTEM] Talep, {amir.AdSoyad} onayına gönderildi.",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            _ = _pushNotificationService.SendToUserBySicilNoAsync(
                amirSicil,
                $"{(t.TalepTurKodu == "BAKIM" ? "Bakım" : t.TalepTurKodu)} Talebi Onay İsteği",
                $"'{t.Konu}' konulu talep ({t.TalepKodu}) onayınız için gönderildi.",
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
                Aciklama = $"[SİSTEM] İşlem onayı geri çekildi (Onay bekleyen kişi: {amirAd}).",
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
                    approve ? $"{(t.TalepTurKodu == "BAKIM" ? "Bakım" : t.TalepTurKodu)} Talebi Onaylandı" : $"{(t.TalepTurKodu == "BAKIM" ? "Bakım" : t.TalepTurKodu)} Talebi Reddedildi",
                    $"'{t.Konu}' konulu talebinize ({t.TalepKodu}) amiriniz tarafından {(approve ? "onay" : "ret")} yanıtı verildi.",
                    new { type = t.TalepTurKodu, screen = "TalepScreen", code = t.TalepKodu, id = t.TalepID }
                );
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
                $"{(t.TalepTurKodu == "BAKIM" ? "Bakım" : t.TalepTurKodu)} Talebi Hakkında Soru",
                $"'{t.Konu}' konulu talep ({t.TalepKodu}) hakkında sorumlu uzman size soru sordu.",
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

            if (t.SorumluSicil != user.SicilNo) return false;

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
                Aciklama = $"[SİSTEM] {helper.AdSoyad} yardımcı personel (bilgi) olarak atandı.",
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

            if (t.SorumluSicil != user.SicilNo) return false;

            var tb = _context.tb_TalepBilgi.FirstOrDefault(b => b.TalepKodu == t.TalepKodu && b.BilgiSicil == helperSicil);
            if (tb == null) return false;

            var helperName = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == helperSicil)?.AdSoyad ?? helperSicil;

            _context.tb_TalepBilgi.Remove(tb);
            _context.SaveChanges();

            BelgeTarihceKaydet(t.TalepKodu, "Bilgi Personeli Silindi", $"Yardımcı Silinen: {helperName} (Yapan: {user.AdSoyad})");

            var gelisme = new tb_TalepGelisme
            {
                TalepKodu = t.TalepKodu,
                Aciklama = $"[SİSTEM] {helperName} yardımcı personel listesinden çıkarıldı.",
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
    }
}
