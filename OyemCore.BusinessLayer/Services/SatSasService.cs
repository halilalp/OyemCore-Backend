using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // Satın Alma (SAT talep / SAS sipariş). Referans: WebServiceSatOnay.cs + WebServiceSas.cs
    // Faz A: dashboard, talep listesi, taslak, kalem yönetimi (onay zincirine bağımlı değil).
    // Faz B (ayrı): onaya gönderme (amir zinciri), SAS sipariş, onay/red, fiyat.
    public class SatSasService : ISatSasService
    {
        private readonly IYbsDbContext _context;

        public SatSasService(IYbsDbContext context)
        {
            _context = context;
        }

        // Bir talebin kullanıcıya görünen durum etiketi (referans DurumBilgi mantığı).
        private static string DurumBilgi(bool? durum, string surecDurum)
        {
            if (durum == true) return "ONAYLANDI";
            if (durum == false) return "REDDEDİLDİ";
            return surecDurum ?? "BEKLEMEDE"; // Durum null => süreç devam ediyor
        }

        public object GetDashboard(string sicilNo, string adminBelgeTur)
        {
            var kendi = _context.tb_SatOnay.Where(o => o.KayitSicil == sicilNo);
            var siparis = _context.tb_SaSip.Count(o => o.Goster != false);

            return new
            {
                taslak = kendi.Count(o => o.SurecDurum == "TASLAK"),
                bekleyen = _context.tb_SatOnay.Count(o => o.BekleyenOnay == sicilNo && o.SurecDurum != "TASLAK" && o.Durum == null),
                onayli = kendi.Count(o => o.Durum == true),
                reddedilen = kendi.Count(o => o.Durum == false),
                siparis
            };
        }

        public IEnumerable<object> GetSatRequests(string sicilNo, string adminBelgeTur)
        {
            // Referans TalepGetir: kullanıcının kendi (taslak hariç) + onayına düşen +
            // SAT admini ise tümü. Distinct ile tekilleştirilir.
            var kendi = _context.tb_SatOnay.Where(o => o.KayitSicil == sicilNo && o.SurecDurum != "TASLAK");
            var bekleyen = _context.tb_SatOnay.Where(o => o.BekleyenOnay == sicilNo && o.SurecDurum != "TASLAK");

            IQueryable<tb_SatOnay> q = kendi;
            q = q.Union(bekleyen);

            bool isSatAdmin = !string.IsNullOrEmpty(adminBelgeTur) &&
                              (adminBelgeTur.Contains("SAT-UZ") || adminBelgeTur.Contains("SAT-MD") || adminBelgeTur.Contains("GENELMUDUR"));
            if (isSatAdmin)
                q = q.Union(_context.tb_SatOnay.Where(o => o.SurecDurum != "TASLAK"));

            var list = q.Distinct().OrderByDescending(o => o.SatOnayID).ToList();

            return list.Select(o => new
            {
                satOnayID = o.SatOnayID,
                belgeNo = o.BelgeNo,
                konu = o.Konu ?? "",
                aciklama = o.Aciklama ?? "",
                kayitSicil = o.KayitSicil ?? "",
                kayitEposta = o.KayitEposta ?? "",
                durum = o.Durum,
                surecDurum = o.SurecDurum ?? "",
                bekleyenOnay = o.BekleyenOnay ?? "",
                sonDurumBilgi = o.SonDurumBilgi ?? "",
                onayDurum = DurumBilgi(o.Durum, o.SurecDurum),
                kayitTarStr = o.KayitTar != null ? o.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
            });
        }

        // Kalem listesini malzeme adıyla birlikte döndürür (ortak projeksiyon).
        private List<object> KalemleriGetir(string belgeNo)
        {
            return (from k in _context.tb_SatKalem
                    where k.BelgeNo == belgeNo
                    join m in _context.tb_Malzeme on k.MalzemeKodu equals m.MalzemeKodu into ms
                    from m in ms.DefaultIfEmpty()
                    orderby k.KalemNo
                    select new
                    {
                        satKalemID = k.SatKalemID,
                        belgeNo = k.BelgeNo,
                        kalemNo = k.KalemNo,
                        malzemeKodu = k.MalzemeKodu,
                        malzemeAdi = m != null ? m.MalzemeAdi : k.MalzemeKodu,
                        miktar = k.Miktar,
                        birimKodu = k.BirimKodu ?? "",
                        talepNedeni = k.TalepNedeni ?? ""
                    }).ToList<object>();
        }

        public object CheckOrCreateSatDraft(string sicilNo, string eposta)
        {
            var so = _context.tb_SatOnay.FirstOrDefault(o => o.SurecDurum == "TASLAK" && o.KayitSicil == sicilNo);
            if (so == null)
            {
                so = new tb_SatOnay
                {
                    KayitSicil = sicilNo,
                    KayitEposta = eposta,
                    SurecDurum = "TASLAK",
                    KayitTar = DateTime.Now
                };
                _context.tb_SatOnay.Add(so);
                _context.SaveChanges();

                // BelgeNo, ID üretildikten sonra atanır (referans: SAT-{yıl}{ay}-{ID}).
                so.BelgeNo = $"SAT-{DateTime.Now:yyyyMM}-{so.SatOnayID}";
                _context.SaveChanges();
            }

            return new
            {
                sat = new
                {
                    satOnayID = so.SatOnayID,
                    belgeNo = so.BelgeNo,
                    konu = so.Konu ?? "",
                    aciklama = so.Aciklama ?? "",
                    dosyaUrl = so.DosyaUrl ?? "",
                    surecDurum = so.SurecDurum
                },
                kalemler = KalemleriGetir(so.BelgeNo)
            };
        }

        public object GetSatDetail(string belgeNo)
        {
            var so = _context.tb_SatOnay.FirstOrDefault(o => o.BelgeNo == belgeNo);
            if (so == null) return null;

            return new
            {
                sat = new
                {
                    satOnayID = so.SatOnayID,
                    belgeNo = so.BelgeNo,
                    konu = so.Konu ?? "",
                    aciklama = so.Aciklama ?? "",
                    dosyaUrl = so.DosyaUrl ?? "",
                    kayitSicil = so.KayitSicil ?? "",
                    kayitEposta = so.KayitEposta ?? "",
                    durum = so.Durum,
                    surecDurum = so.SurecDurum ?? "",
                    bekleyenOnay = so.BekleyenOnay ?? "",
                    sonDurumBilgi = so.SonDurumBilgi ?? "",
                    onayDurum = DurumBilgi(so.Durum, so.SurecDurum),
                    kayitTarStr = so.KayitTar != null ? so.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                },
                kalemler = KalemleriGetir(so.BelgeNo)
            };
        }

        public object AddSatItem(string belgeNo, string malzemeKodu, decimal miktar, string birimKodu, string neden)
        {
            var so = _context.tb_SatOnay.FirstOrDefault(o => o.BelgeNo == belgeNo);
            if (so == null) throw new InvalidOperationException("Talep bulunamadı.");
            if (so.SurecDurum != "TASLAK") throw new InvalidOperationException("Yalnızca taslak talebe kalem eklenebilir.");
            if (string.IsNullOrWhiteSpace(malzemeKodu)) throw new InvalidOperationException("Malzeme seçilmelidir.");
            if (miktar <= 0) throw new InvalidOperationException("Miktar sıfırdan büyük olmalıdır.");

            int sonKalemNo = _context.tb_SatKalem.Where(k => k.BelgeNo == belgeNo).Select(k => k.KalemNo ?? 0).DefaultIfEmpty(0).Max();

            var kalem = new tb_SatKalem
            {
                BelgeNo = belgeNo,
                KalemNo = sonKalemNo + 1,
                MalzemeKodu = malzemeKodu,
                Miktar = miktar,
                BirimKodu = birimKodu,
                TalepNedeni = neden
            };
            _context.tb_SatKalem.Add(kalem);
            _context.SaveChanges();

            return new { success = true, kalemler = KalemleriGetir(belgeNo) };
        }

        public bool DeleteSatItem(int satKalemID)
        {
            var kalem = _context.tb_SatKalem.FirstOrDefault(k => k.SatKalemID == satKalemID);
            if (kalem == null) return false;

            var so = _context.tb_SatOnay.FirstOrDefault(o => o.BelgeNo == kalem.BelgeNo);
            if (so != null && so.SurecDurum != "TASLAK")
                throw new InvalidOperationException("Yalnızca taslak talepten kalem silinebilir.");

            _context.tb_SatKalem.Remove(kalem);
            _context.SaveChanges();
            return true;
        }

        // Taslağın konu/açıklamasını kaydeder (kalem eklerken sık çağrılır).
        public bool SaveSatHeader(string sicilNo, string konu, string aciklama)
        {
            var so = _context.tb_SatOnay.FirstOrDefault(o => o.SurecDurum == "TASLAK" && o.KayitSicil == sicilNo);
            if (so == null) throw new InvalidOperationException("Taslak talep bulunamadı.");
            so.Konu = konu;
            so.Aciklama = aciklama;
            _context.SaveChanges();
            return true;
        }

        public bool SubmitSatRequest(string sicilNo, string konu, string aciklama)
        {
            // Onaya gönderme amir zincirine (tb_BelgeOnay) bağlı — Faz B'de tamamlanacak.
            // Yarım bir onay akışı üretmemek için burada hiçbir yazma yapılmıyor; çağrı
            // net bir mesajla reddediliyor.
            var so = _context.tb_SatOnay.FirstOrDefault(o => o.SurecDurum == "TASLAK" && o.KayitSicil == sicilNo);
            if (so == null) throw new InvalidOperationException("Gönderilecek taslak talep bulunamadı.");
            if (!_context.tb_SatKalem.Any(k => k.BelgeNo == so.BelgeNo))
                throw new InvalidOperationException("Talebe en az bir kalem eklemelisiniz.");

            throw new InvalidOperationException("Onaya gönderme henüz aktif değil (amir onay zinciri hazırlanıyor).");
        }
    }
}
