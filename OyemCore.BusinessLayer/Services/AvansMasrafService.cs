using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // Mağaza avans-masraf onay iş akışı. referans: WebServiceAvansMasraf (birebir).
    // Onay hiyerarşisi tb_Hiyerarsi (Amir1/2/3) → tb_BelgeOnay (Sıra) deseniyle kurulur (İzin modülüyle aynı).
    public class AvansMasrafService : IAvansMasrafService
    {
        private readonly IYbsDbContext _context;
        private readonly IBildirimService _bildirim;
        private readonly IPushNotificationService _push;

        public AvansMasrafService(IYbsDbContext context, IBildirimService bildirim, IPushNotificationService push)
        {
            _context = context;
            _bildirim = bildirim;
            _push = push;
        }

        private tb_Kullanici CurrentUser(int kullaniciID) =>
            _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);

        private void BelgeTarihceKaydet(string belgeNo, string konu, string aciklama)
        {
            _context.tb_BelgeTarihce.Add(new tb_BelgeTarihce { BelgeKodu = belgeNo, Konu = konu, Aciklama = aciklama, KayitTar = DateTime.Now });
        }

        // Onay zincirini kurar: tb_Hiyerarsi'den amirleri okur, tb_BelgeOnay satırları ekler.
        // Dönüş: (surecDurum, bekleyenOnay). Amir yoksa doğrudan ONAYLANDI.
        private (string surec, string bekleyen) OnayZinciriKur(string talepEdenSicil, string belgeNo)
        {
            var h = _context.tb_Hiyerarsi.AsQueryable().FirstOrDefault(x => x.SicilNo == talepEdenSicil);
            string amir1 = h?.Amir1;
            if (string.IsNullOrEmpty(amir1))
                return ("ONAYLANDI", null); // amir tanımlı değil → onay gerekmez

            short sira = 1;
            _context.tb_BelgeOnay.Add(new tb_BelgeOnay { BelgeNo = belgeNo, Sira = sira++, OnaySicil = amir1, OnayTur = "ONAY" });
            if (!string.IsNullOrEmpty(h.Amir2))
                _context.tb_BelgeOnay.Add(new tb_BelgeOnay { BelgeNo = belgeNo, Sira = sira++, OnaySicil = h.Amir2, OnayTur = "ONAY" });
            if (!string.IsNullOrEmpty(h.Amir3))
                _context.tb_BelgeOnay.Add(new tb_BelgeOnay { BelgeNo = belgeNo, Sira = sira++, OnaySicil = h.Amir3, OnayTur = "ONAY" });

            return ("ONAYDA", amir1);
        }

        // ── 1. Avans kaydet / güncelle ──
        public object AvansKaydet(int kullaniciID, int id, decimal tutar, string aciklama)
        {
            var usr = CurrentUser(kullaniciID);
            if (usr == null) return new { success = false, message = "Kullanıcı bulunamadı." };

            if (id == 0)
            {
                string prefix = "AVNS-" + DateTime.Now.ToString("yyyyMMdd") + "-";
                int count = _context.tb_MagazaAvans.Count(a => a.BelgeNo.StartsWith(prefix)) + 1;
                string belgeNo = prefix + count.ToString().PadLeft(4, '0');

                var (surec, bekleyen) = OnayZinciriKur(usr.SicilNo, belgeNo);

                var yeni = new tb_MagazaAvans
                {
                    BelgeNo = belgeNo,
                    TalepEdenSicil = usr.SicilNo,
                    Tutar = tutar,
                    Aciklama = aciklama,
                    SurecDurum = surec,
                    BekleyenOnay = bekleyen,
                    KalanBakiye = tutar,
                    TalepTarihi = DateTime.Now
                };
                _context.tb_MagazaAvans.Add(yeni);
                BelgeTarihceKaydet(belgeNo, "AVANS TALEBİ", $"Yeni avans talebi oluşturuldu: {tutar} TL");
                _context.SaveChanges();

                if (surec == "ONAYDA" && !string.IsNullOrEmpty(bekleyen))
                {
                    _bildirim.AddNotification(bekleyen, "Avans Onay Bekliyor", $"{usr.AdSoyad} tarafından #{belgeNo} nolu avans talebi onayınıza sunuldu.", "", "Avans", yeni.AvansID.ToString(), kullaniciID);
                    _ = _push.SendToUserBySicilNoAsync(bekleyen, "Avans Onay Bekliyor", $"{usr.AdSoyad} tarafından #{belgeNo} nolu avans talebi onayınıza sunuldu.", new { type = "avans", screen = "AvansMasraf", id = yeni.AvansID.ToString() });
                }

                return new { success = true, message = "Talep başarıyla oluşturuldu.", BelgeNo = belgeNo };
            }
            else
            {
                var avans = _context.tb_MagazaAvans.FirstOrDefault(a => a.AvansID == id);
                if (avans == null) return new { success = false, message = "Talep bulunamadı." };
                if (avans.SurecDurum == "ONAYLANDI" || avans.SurecDurum == "ODENDI" || avans.SurecDurum == "KAPATILDI")
                    return new { success = false, message = "Onaylanmış veya ödenmiş talepler güncellenemez." };

                avans.Tutar = tutar;
                avans.Aciklama = aciklama;
                avans.KalanBakiye = tutar;
                BelgeTarihceKaydet(avans.BelgeNo, "AVANS GÜNCELLEME", $"Avans talebi güncellendi. Yeni Tutar: {tutar} TL");
                _context.SaveChanges();
                return new { success = true, message = "Talep başarıyla güncellendi." };
            }
        }

        // ── 2. Masraf kaydet / güncelle (kalemli) ──
        public object MasrafKaydet(int kullaniciID, int id, decimal toplamTutar, string aciklama, int? iliskiliAvansID, IEnumerable<MasrafKalemDto> kalemler)
        {
            var usr = CurrentUser(kullaniciID);
            if (usr == null) return new { success = false, message = "Kullanıcı bulunamadı." };

            var kalemList = (kalemler ?? Enumerable.Empty<MasrafKalemDto>()).ToList();

            if (id == 0)
            {
                string prefix = "MSRF-" + DateTime.Now.ToString("yyyyMMdd") + "-";
                int count = _context.tb_MagazaMasraf.Count(m => m.BelgeNo.StartsWith(prefix)) + 1;
                string belgeNo = prefix + count.ToString().PadLeft(4, '0');

                var (surec, bekleyen) = OnayZinciriKur(usr.SicilNo, belgeNo);

                var yeni = new tb_MagazaMasraf
                {
                    BelgeNo = belgeNo,
                    TalepEdenSicil = usr.SicilNo,
                    ToplamTutar = toplamTutar,
                    Aciklama = aciklama,
                    SurecDurum = surec,
                    BekleyenOnay = bekleyen,
                    IliskiliAvansID = (iliskiliAvansID.HasValue && iliskiliAvansID > 0) ? iliskiliAvansID : null,
                    TalepTarihi = DateTime.Now
                };
                _context.tb_MagazaMasraf.Add(yeni);
                _context.SaveChanges();

                foreach (var k in kalemList)
                    _context.tb_MagazaMasrafDetay.Add(new tb_MagazaMasrafDetay
                    {
                        MasrafID = yeni.MasrafID,
                        FisNo = k.FisNo, Firma = k.Firma, Tarih = k.Tarih,
                        Tutar = k.Tutar, KdvTutar = k.KdvTutar, Aciklama = k.Aciklama, DosyaYolu = k.DosyaYolu
                    });
                BelgeTarihceKaydet(belgeNo, "MASRAF TALEBİ", $"Yeni masraf talebi oluşturuldu: {toplamTutar} TL ({kalemList.Count} kalem)");
                _context.SaveChanges();

                if (surec == "ONAYDA" && !string.IsNullOrEmpty(bekleyen))
                {
                    _bildirim.AddNotification(bekleyen, "Masraf Onay Bekliyor", $"{usr.AdSoyad} tarafından #{belgeNo} nolu masraf talebi onayınıza sunuldu.", "", "Masraf", yeni.MasrafID.ToString(), kullaniciID);
                    _ = _push.SendToUserBySicilNoAsync(bekleyen, "Masraf Onay Bekliyor", $"{usr.AdSoyad} tarafından #{belgeNo} nolu masraf talebi onayınıza sunuldu.", new { type = "masraf", screen = "AvansMasraf", id = yeni.MasrafID.ToString() });
                }

                return new { success = true, message = "Talep başarıyla oluşturuldu.", BelgeNo = belgeNo };
            }
            else
            {
                var masraf = _context.tb_MagazaMasraf.FirstOrDefault(m => m.MasrafID == id);
                if (masraf == null) return new { success = false, message = "Talep bulunamadı." };
                if (masraf.SurecDurum == "ONAYLANDI" || masraf.SurecDurum == "ODENDI" || masraf.SurecDurum == "KAPATILDI")
                    return new { success = false, message = "Onaylanmış veya ödenmiş talepler güncellenemez." };

                masraf.ToplamTutar = toplamTutar;
                masraf.Aciklama = aciklama;
                masraf.IliskiliAvansID = (iliskiliAvansID.HasValue && iliskiliAvansID > 0) ? iliskiliAvansID : null;

                var eski = _context.tb_MagazaMasrafDetay.Where(d => d.MasrafID == id).ToList();
                _context.tb_MagazaMasrafDetay.RemoveRange(eski);
                foreach (var k in kalemList)
                    _context.tb_MagazaMasrafDetay.Add(new tb_MagazaMasrafDetay
                    {
                        MasrafID = id,
                        FisNo = k.FisNo, Firma = k.Firma, Tarih = k.Tarih,
                        Tutar = k.Tutar, KdvTutar = k.KdvTutar, Aciklama = k.Aciklama, DosyaYolu = k.DosyaYolu
                    });
                BelgeTarihceKaydet(masraf.BelgeNo, "MASRAF GÜNCELLEME", $"Masraf talebi güncellendi. Yeni Tutar: {toplamTutar} TL");
                _context.SaveChanges();
                return new { success = true, message = "Talep başarıyla güncellendi." };
            }
        }

        // ── 3. Avans listesi (kendi talepleri) ──
        public IEnumerable<object> AvansListesiGetir(int kullaniciID)
        {
            var usr = CurrentUser(kullaniciID);
            if (usr == null) return new List<object>();

            return (from a in _context.tb_MagazaAvans
                    where a.TalepEdenSicil == usr.SicilNo
                    join p in _context.tb_Personel on a.BekleyenOnay equals p.SicilNo into pj
                    from p in pj.DefaultIfEmpty()
                    orderby a.TalepTarihi descending
                    select new
                    {
                        a.AvansID, a.BelgeNo, a.TalepEdenSicil, a.Tutar, a.Aciklama,
                        a.SurecDurum, a.BekleyenOnay, a.KalanBakiye, a.TalepTarihi,
                        BekleyenOnayAdSoyad = p != null ? p.AdSoyad : null
                    }).ToList();
        }

        // ── 4. Masraf listesi (kendi talepleri) ──
        public IEnumerable<object> MasrafListesiGetir(int kullaniciID)
        {
            var usr = CurrentUser(kullaniciID);
            if (usr == null) return new List<object>();

            return (from m in _context.tb_MagazaMasraf
                    where m.TalepEdenSicil == usr.SicilNo
                    join p in _context.tb_Personel on m.BekleyenOnay equals p.SicilNo into pj
                    from p in pj.DefaultIfEmpty()
                    orderby m.TalepTarihi descending
                    select new
                    {
                        m.MasrafID, m.BelgeNo, m.TalepEdenSicil, m.ToplamTutar, m.Aciklama,
                        m.SurecDurum, m.BekleyenOnay, m.IliskiliAvansID, m.TalepTarihi,
                        BekleyenOnayAdSoyad = p != null ? p.AdSoyad : null
                    }).ToList();
        }

        // ── 5. Masraf detay (kalemler) ──
        public object MasrafDetayGetir(int kullaniciID, int masrafID)
        {
            var masraf = _context.tb_MagazaMasraf.FirstOrDefault(m => m.MasrafID == masrafID);
            if (masraf == null) return new { success = false, message = "Masraf bulunamadı." };

            var kalemler = _context.tb_MagazaMasrafDetay.Where(d => d.MasrafID == masrafID)
                .Select(d => new { d.DetayID, d.FisNo, d.Firma, d.Tarih, d.Tutar, d.KdvTutar, d.Aciklama, d.DosyaYolu }).ToList();

            return new { success = true, masraf = new { masraf.MasrafID, masraf.BelgeNo, masraf.ToplamTutar, masraf.Aciklama, masraf.SurecDurum }, kalemler };
        }

        // ── 6. Onay bekleyen talepler (avans + masraf birleşik) ──
        public IEnumerable<object> OnayBekleyenTaleplerGetir(int kullaniciID)
        {
            var usr = CurrentUser(kullaniciID);
            if (usr == null) return new List<object>();

            var list = new List<object>();

            var avanslar = (from a in _context.tb_MagazaAvans
                            where a.BekleyenOnay == usr.SicilNo && a.SurecDurum == "ONAYDA"
                            join p in _context.tb_Personel on a.TalepEdenSicil equals p.SicilNo into pj
                            from p in pj.DefaultIfEmpty()
                            select new { Tip = "AVANS", ID = a.AvansID, a.BelgeNo, Tutar = a.Tutar, a.Aciklama, a.TalepTarihi, TalepEdenAdSoyad = p != null ? p.AdSoyad : a.TalepEdenSicil }).ToList();
            list.AddRange(avanslar);

            var masraflar = (from m in _context.tb_MagazaMasraf
                             where m.BekleyenOnay == usr.SicilNo && m.SurecDurum == "ONAYDA"
                             join p in _context.tb_Personel on m.TalepEdenSicil equals p.SicilNo into pj
                             from p in pj.DefaultIfEmpty()
                             select new { Tip = "MASRAF", ID = m.MasrafID, m.BelgeNo, Tutar = m.ToplamTutar, m.Aciklama, m.TalepTarihi, TalepEdenAdSoyad = p != null ? p.AdSoyad : m.TalepEdenSicil }).ToList();
            list.AddRange(masraflar);

            return list;
        }

        // ── 7. Onayla / Reddet (sıradaki amire ilerlet veya sonlandır) ──
        public object AvansMasrafOnaylaReddet(int kullaniciID, string tip, int id, bool onay, string aciklama)
        {
            var usr = CurrentUser(kullaniciID);
            if (usr == null) return new { success = false, message = "Kullanıcı bulunamadı." };

            string belgeNo = "", currentStatus = "", talepEdenSicil = "";
            tb_MagazaAvans avans = null; tb_MagazaMasraf masraf = null;

            if (tip == "AVANS")
            {
                avans = _context.tb_MagazaAvans.FirstOrDefault(a => a.AvansID == id);
                if (avans != null) { belgeNo = avans.BelgeNo; currentStatus = avans.SurecDurum; talepEdenSicil = avans.TalepEdenSicil; }
            }
            else
            {
                masraf = _context.tb_MagazaMasraf.FirstOrDefault(m => m.MasrafID == id);
                if (masraf != null) { belgeNo = masraf.BelgeNo; currentStatus = masraf.SurecDurum; talepEdenSicil = masraf.TalepEdenSicil; }
            }

            if (string.IsNullOrEmpty(belgeNo)) return new { success = false, message = "Talep bulunamadı." };
            if (currentStatus != "ONAYDA") return new { success = false, message = "Sadece onay bekleyen talepler onaylanabilir/reddedilebilir." };

            var bo = _context.tb_BelgeOnay.FirstOrDefault(o => o.BelgeNo == belgeNo && o.OnaySicil == usr.SicilNo && o.OnayTur == "ONAY" && o.Durum == null);
            if (bo == null) return new { success = false, message = "Bu belge için aktif onay yetkiniz bulunmamaktadır." };

            bo.Durum = onay;
            bo.Aciklama = aciklama;
            bo.IslemTar = DateTime.Now;

            string yeniDurum; string yeniBekleyen = null;
            if (onay)
            {
                var ust = _context.tb_BelgeOnay.FirstOrDefault(o => o.BelgeNo == belgeNo && o.Sira == (short)(bo.Sira + 1) && o.OnayTur == "ONAY");
                if (ust == null) { yeniDurum = "ONAYLANDI"; }
                else { yeniDurum = "ONAYDA"; yeniBekleyen = ust.OnaySicil; }
            }
            else { yeniDurum = "REDDEDILDI"; }

            if (tip == "AVANS") { avans.SurecDurum = yeniDurum; avans.BekleyenOnay = yeniBekleyen; }
            else { masraf.SurecDurum = yeniDurum; masraf.BekleyenOnay = yeniBekleyen; }

            BelgeTarihceKaydet(belgeNo, onay ? "Talep Onaylandı" : "Talep Reddedildi", $"{usr.AdSoyad} tarafından aksiyon alındı. Açıklama: {aciklama}");
            _context.SaveChanges();

            string t = tip == "AVANS" ? "Avans" : "Masraf";
            if (yeniDurum == "ONAYDA" && !string.IsNullOrEmpty(yeniBekleyen))
            {
                _bildirim.AddNotification(yeniBekleyen, $"{t} Onay Bekliyor", $"{usr.AdSoyad} tarafından onaylanan #{belgeNo} nolu talep onayınıza sunuldu.", "", t, id.ToString(), kullaniciID);
                _ = _push.SendToUserBySicilNoAsync(yeniBekleyen, $"{t} Onay Bekliyor", $"{usr.AdSoyad} tarafından onaylanan #{belgeNo} nolu talep onayınıza sunuldu.", new { type = t.ToLower(), screen = "AvansMasraf", id = id.ToString() });
            }
            else if (yeniDurum == "ONAYLANDI")
            {
                _bildirim.AddNotification(talepEdenSicil, $"{t} Onaylandı", $"#{belgeNo} nolu talebiniz onaylandı.", "", t, id.ToString(), kullaniciID);
                _ = _push.SendToUserBySicilNoAsync(talepEdenSicil, $"{t} Onaylandı", $"#{belgeNo} nolu talebiniz onaylandı.", new { type = t.ToLower(), screen = "AvansMasraf", id = id.ToString() });
            }
            else if (yeniDurum == "REDDEDILDI")
            {
                _bildirim.AddNotification(talepEdenSicil, $"{t} Reddedildi", $"#{belgeNo} nolu talebiniz reddedildi. Gerekçe: {aciklama}", "", t, id.ToString(), kullaniciID);
                _ = _push.SendToUserBySicilNoAsync(talepEdenSicil, $"{t} Reddedildi", $"#{belgeNo} nolu talebiniz reddedildi. Gerekçe: {aciklama}", new { type = t.ToLower(), screen = "AvansMasraf", id = id.ToString() });
            }

            return new { success = true, message = "İşlem başarıyla tamamlandı." };
        }
    }
}
