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
                                 select new { t, tk, p1, p2, tb, ts, tb_blm, tm, HasOnay = _context.tb_TalepAmir.Any(a => a.TalepKodu == t.TalepKodu && a.Durum == null) };

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
                    SorumluAd = o.p2 != null ? o.p2.AdSoyad : "Atanmamis",
                    Durum = o.t.Durum == true ? "Kapalı" : (o.HasOnay ? "ONAY BEKLİYOR" : "BEKLEMEDE"),
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
                                   select new { t, tk, p1, p2, HasOnay = _context.tb_TalepAmir.Any(a => a.TalepKodu == t.TalepKodu && a.Durum == null) };

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
                    SorumluAd = o.p2 != null ? o.p2.AdSoyad : "Atanmamis",
                    Durum = o.t.Durum == true ? "Kapalı" : (o.HasOnay ? "ONAY BEKLİYOR" : "BEKLEMEDE"),
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

            // Onay Ge?misi ve Aktif Onayci
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

            // Soru Ge?misi ve Aktif Cevaplanmamis Soru
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
                ActiveOnay = activeOnay,
                SoruList = soruList
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
                        if (existing == null) throw new InvalidOperationException("G?ncellenecek talep bulunamadi.");

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
                Aciklama = $"[SISTEM] Talep, {amir.AdSoyad} onayina g?nderildi.",
                Sicil = user.SicilNo,
                Eposta = user.Eposta,
                KayitTar = DateTime.Now
            };
            _context.tb_TalepGelisme.Add(gelisme);
            _context.SaveChanges();

            _ = _pushNotificationService.SendToUserBySicilNoAsync(
                amirSicil,
                $"{(t.TalepTurKodu == "BAKIM" ? "Bakim" : t.TalepTurKodu)} Talebi Onay Istegi",
                $"'{t.Konu}' konulu talep ({t.TalepKodu}) onayiniz i?in g?nderildi.",
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
                Aciklama = $"[SISTEM] Islem onayi geri ?ekildi (Onay bekleyen kisi: {amirAd}).",
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
                Aciklama = $"[SISTEM] {helperName} yardimci personel listesinden ?ikarildi.",
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
                    
                    if (t.KayitTar.HasValue)
                        t.MttrTamamSure = SureHesaplaBakim(t.KayitTar.Value, t.KapanmaTar.Value);

                    _context.SaveChanges();
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

        public bool UpdateRequestStatus(int kullaniciID, int talepID, string status)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) return false;

            var t = _context.tb_Talep.FirstOrDefault(r => r.TalepID == talepID);
            if (t == null) return false;

            // Istemci "KAPATILDI" gonderiyor. Onceki karsilastirma "Kapali" bekledigi icin
            // kapatma hic calismiyor, talep sessizce "yeniden acildi" dalina dusuyordu.
            // Tanimsiz bir deger artik sessizce gecmez, hata firlatir.
            var durumKodu = (status ?? "").Trim().ToUpperInvariant();
            bool kapatiliyor = durumKodu == "KAPATILDI" || durumKodu == "KAPALI";
            bool yenidenAciliyor = durumKodu == "ACIK" || durumKodu == "ACILDI";

            if (!kapatiliyor && !yenidenAciliyor)
                throw new InvalidOperationException($"Geçersiz talep durumu: {status}");

            if (kapatiliyor)
            {
                if (string.IsNullOrEmpty(t.SorumluSicil))
                    throw new InvalidOperationException("Sorumlu atanmamış bir talep kapatılamaz.");

                if (t.SorumluSicil != user.SicilNo)
                    throw new InvalidOperationException("Talebi yalnızca sorumlu kişi kapatabilir.");
                
                if (t.TalepTurKodu == "BAKIM")
                {
                    var openWorkOrders = _context.tb_TalepIsEmri.Any(i => i.TalepKodu == t.TalepKodu && i.KapanmaTar == null);
                    if (openWorkOrders)
                        throw new InvalidOperationException("Kapatılmamış iş emirleri varken talep kapatılamaz.");

                    t.Durum = false;
                    var formOnayi = new tb_TalepAmir
                    {
                        TalepKodu = t.TalepKodu,
                        AmirSicil = t.KayitSicil,
                        IslemTur = "ONAY",
                        KayitTar = DateTime.Now
                    };
                    _context.tb_TalepAmir.Add(formOnayi);
                    BelgeTarihceKaydet(t.TalepKodu, "Form Onayına Gönderildi", $"Talep sahibi {t.KayitSicil} form onayı bekleniyor.");
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
            }
            return true;
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
            }

            return true;
        }
    }
}
