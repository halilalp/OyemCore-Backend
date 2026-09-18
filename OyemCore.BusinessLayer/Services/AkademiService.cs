using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OyemCore.BusinessLayer.Common;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class AkademiService : IAkademiService
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _push;

        public AkademiService(IYbsDbContext context, IPushNotificationService push)
        {
            _context = context;
            _push = push;
        }

        private string GetCurrentSicilNo(int kullaniciID)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null || string.IsNullOrEmpty(user.SicilNo))
                throw new Exception("Kullanici / SicilNo bulunamadi.");
            return user.SicilNo;
        }

        // -------------------------------------------------------------
        // İçerik (İK yönetimi)
        // -------------------------------------------------------------

        public IEnumerable<object> GetContentList(int kullaniciID)
        {
            return _context.tb_AkademiEgitim
                .Where(e => e.AktifMi)
                .OrderByDescending(e => e.KayitTarihi)
                .ToList()
                .Select(e => new
                {
                    id = e.AkademiEgitimID,
                    baslik = e.Baslik,
                    aciklama = e.Aciklama ?? "",
                    kategoriKodu = e.KategoriKodu ?? "",
                    icerikTipi = e.IcerikTipi,
                    dosyaUrl = e.DosyaUrl,
                    sureSaniye = e.SureSaniye,
                    kayitTarihi = e.KayitTarihi.ToString("dd/MM/yyyy")
                })
                .ToList();
        }

        public bool SaveContent(int kullaniciID, string baslik, string aciklama, string kategoriKodu, string icerikTipi, string dosyaUrl, int? sureSaniye)
        {
            string sicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrWhiteSpace(baslik)) throw new Exception("Baslik bos olamaz.");
            if (string.IsNullOrWhiteSpace(dosyaUrl)) throw new Exception("Dosya secilmedi.");

            _context.tb_AkademiEgitim.Add(new tb_AkademiEgitim
            {
                Baslik = baslik,
                Aciklama = aciklama,
                KategoriKodu = kategoriKodu,
                IcerikTipi = string.IsNullOrEmpty(icerikTipi) ? "Video" : icerikTipi,
                DosyaUrl = dosyaUrl,
                SureSaniye = sureSaniye,
                AktifMi = true,
                OlusturanSicil = sicilNo,
                KayitTarihi = DateTime.Now
            });
            _context.SaveChanges();
            return true;
        }

        public bool UpdateContent(int kullaniciID, int akademiEgitimID, string baslik, string aciklama, string kategoriKodu, string dosyaUrl, int? sureSaniye)
        {
            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == akademiEgitimID);
            if (egitim == null) throw new Exception("Akademi egitimi bulunamadi.");

            egitim.Baslik = baslik;
            egitim.Aciklama = aciklama;
            egitim.KategoriKodu = kategoriKodu;
            if (!string.IsNullOrEmpty(dosyaUrl)) egitim.DosyaUrl = dosyaUrl;
            egitim.SureSaniye = sureSaniye;

            _context.SaveChanges();
            return true;
        }

        public bool SetContentActive(int kullaniciID, int akademiEgitimID, bool aktifMi)
        {
            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == akademiEgitimID);
            if (egitim == null) throw new Exception("Akademi egitimi bulunamadi.");

            egitim.AktifMi = aktifMi;
            _context.SaveChanges();
            return true;
        }

        // -------------------------------------------------------------
        // Atama (İK yönetimi)
        // -------------------------------------------------------------

        public int AssignToPersonnel(int kullaniciID, int akademiEgitimID, List<string> sicilNoList, DateTime? sonTarih, bool zorunluMu, bool aktifIzlemeZorunlu)
        {
            string atayanSicil = GetCurrentSicilNo(kullaniciID);
            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == akademiEgitimID);
            if (egitim == null) throw new Exception("Akademi egitimi bulunamadi.");
            if (sicilNoList == null || sicilNoList.Count == 0) throw new Exception("Atanacak personel secilmedi.");

            int eklenen = 0;
            foreach (var raw in sicilNoList.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string sicil = (raw ?? "").Trim();
                if (string.IsNullOrEmpty(sicil)) continue;

                // Aynı eğitim aynı kişiye zaten atanmışsa (iptal edilmemiş) tekrar atama.
                bool zatenVar = _context.tb_AkademiAtama.Any(a =>
                    a.AkademiEgitimID == akademiEgitimID && a.SicilNo == sicil && !a.IptalMi);
                if (zatenVar) continue;

                var atama = new tb_AkademiAtama
                {
                    AkademiEgitimID = akademiEgitimID,
                    SicilNo = sicil,
                    AtayanSicil = atayanSicil,
                    AtamaTarihi = DateTime.Now,
                    SonTarih = sonTarih,
                    ZorunluMu = zorunluMu,
                    AktifIzlemeZorunlu = aktifIzlemeZorunlu,
                    IptalMi = false
                };
                _context.tb_AkademiAtama.Add(atama);
                _context.SaveChanges(); // AtamaID'yi almak için

                _context.tb_AkademiIlerleme.Add(new tb_AkademiIlerleme
                {
                    AtamaID = atama.AtamaID,
                    MaxIzlenenSaniye = 0,
                    AktifIzlemeSaniye = 0,
                    TamamlandiMi = false,
                    GuncellemeTarihi = DateTime.Now
                });
                _context.SaveChanges();
                eklenen++;

                try
                {
                    _ = _push.SendToUserBySicilNoAsync(
                        sicil,
                        "Yeni Akademi Eğitimi",
                        $"'{egitim.Baslik}' eğitimi size atandı." + (zorunluMu ? " (Zorunlu)" : ""),
                        new { type = "akademi", screen = "AkademiDetay", id = atama.AtamaID }
                    );
                }
                catch { }
            }

            return eklenen;
        }

        // -------------------------------------------------------------
        // Personel tarafı (web + mobil ortak)
        // -------------------------------------------------------------

        public IEnumerable<object> GetMyAssignments(string sicilNo)
        {
            var query = from a in _context.tb_AkademiAtama
                        where a.SicilNo == sicilNo && !a.IptalMi
                        join e in _context.tb_AkademiEgitim on a.AkademiEgitimID equals e.AkademiEgitimID
                        join i in _context.tb_AkademiIlerleme on a.AtamaID equals i.AtamaID into iler
                        from i in iler.DefaultIfEmpty()
                        orderby a.AtamaTarihi descending
                        select new { a, e, i };

            return query.ToList().Select(x => new
            {
                atamaID = x.a.AtamaID,
                baslik = x.e.Baslik,
                icerikTipi = x.e.IcerikTipi,
                kategoriKodu = x.e.KategoriKodu ?? "",
                sureSaniye = x.e.SureSaniye,
                sonTarih = x.a.SonTarih?.ToString("dd/MM/yyyy"),
                zorunluMu = x.a.ZorunluMu,
                maxIzlenenSaniye = x.i != null ? x.i.MaxIzlenenSaniye : 0,
                tamamlandiMi = x.i != null && x.i.TamamlandiMi
            }).ToList();
        }

        public object GetAssignmentDetail(int atamaID, string sicilNo)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null) throw new Exception("Atama bulunamadi.");
            if (atama.SicilNo != sicilNo) throw new UnauthorizedAccessException("Bu atama size ait degil.");

            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == atama.AkademiEgitimID);
            var ilerleme = _context.tb_AkademiIlerleme.FirstOrDefault(i => i.AtamaID == atamaID);

            var sonTalep = _context.tb_AkademiSinavTalep
                .Where(t => t.AtamaID == atamaID)
                .OrderByDescending(t => t.TalepID)
                .FirstOrDefault();
            string? ekSinavRedSebebi = (sonTalep != null && sonTalep.Durum == "REDDEDILDI") ? sonTalep.RedSebebi : null;

            return new
            {
                atamaID = atama.AtamaID,
                baslik = egitim?.Baslik ?? "",
                aciklama = egitim?.Aciklama ?? "",
                icerikTipi = egitim?.IcerikTipi ?? "Video",
                dosyaUrl = egitim?.DosyaUrl ?? "",
                kategoriKodu = egitim?.KategoriKodu ?? "",
                sureSaniye = egitim?.SureSaniye,
                zorunluMu = atama.ZorunluMu,
                aktifIzlemeZorunlu = atama.AktifIzlemeZorunlu,
                atamaTarihi = atama.AtamaTarihi.ToString("dd/MM/yyyy"),
                sonTarih = atama.SonTarih.HasValue ? atama.SonTarih.Value.ToString("dd/MM/yyyy") : null,
                maxIzlenenSaniye = ilerleme?.MaxIzlenenSaniye ?? 0,
                tamamlandiMi = ilerleme?.TamamlandiMi ?? false,
                tamamlanmaTarihi = (ilerleme != null && ilerleme.TamamlanmaTarihi.HasValue) ? ilerleme.TamamlanmaTarihi.Value.ToString("dd/MM/yyyy") : null,
                sinavAktif = egitim?.SinavAktif ?? false,
                ekSinavRedSebebi
            };
        }

        public bool UpdateProgress(int atamaID, string sicilNo, int maxIzlenenSaniye, int aktifIzlemeSaniyeArtis, bool tamamlaZorla = false)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null) throw new Exception("Atama bulunamadi.");
            if (atama.SicilNo != sicilNo) throw new UnauthorizedAccessException("Bu atama size ait degil.");

            var ilerleme = _context.tb_AkademiIlerleme.FirstOrDefault(i => i.AtamaID == atamaID);
            if (ilerleme == null)
            {
                ilerleme = new tb_AkademiIlerleme { AtamaID = atamaID, GuncellemeTarihi = DateTime.Now };
                _context.tb_AkademiIlerleme.Add(ilerleme);
            }

            if (maxIzlenenSaniye > ilerleme.MaxIzlenenSaniye)
                ilerleme.MaxIzlenenSaniye = maxIzlenenSaniye;

            if (aktifIzlemeSaniyeArtis > 0)
                ilerleme.AktifIzlemeSaniye += aktifIzlemeSaniyeArtis;

            ilerleme.GuncellemeTarihi = DateTime.Now;

            if (!ilerleme.TamamlandiMi)
            {
                var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == atama.AkademiEgitimID);
                if (egitim?.SureSaniye != null && egitim.SureSaniye > 0)
                {
                    // Video: sure taniml, >= %90 esigi otomatik tamamlar.
                    double oran = (double)ilerleme.MaxIzlenenSaniye / egitim.SureSaniye.Value;
                    if (oran >= 0.9)
                    {
                        ilerleme.TamamlandiMi = true;
                        ilerleme.TamamlanmaTarihi = DateTime.Now;
                    }
                }
                else if (tamamlaZorla)
                {
                    // Dokuman/sunum gibi suresiz icerik: otomatik esik yok, kullanici sayfa
                    // sonuna gelip minimum bekleme suresini doldurduktan sonra istemcideki
                    // "Okudum, Tamamla" butonuyla ACIKCA tamamlar.
                    ilerleme.TamamlandiMi = true;
                    ilerleme.TamamlanmaTarihi = DateTime.Now;
                }
            }

            _context.SaveChanges();
            return true;
        }

        // -------------------------------------------------------------
        // İK raporlama
        // -------------------------------------------------------------

        public IEnumerable<object> GetAssignmentReport(int akademiEgitimID)
        {
            var query = from a in _context.tb_AkademiAtama
                        where a.AkademiEgitimID == akademiEgitimID && !a.IptalMi
                        join p in _context.tb_Personel on a.SicilNo equals p.SicilNo into ps
                        from p in ps.DefaultIfEmpty()
                        join i in _context.tb_AkademiIlerleme on a.AtamaID equals i.AtamaID into iler
                        from i in iler.DefaultIfEmpty()
                        orderby a.AtamaTarihi descending
                        select new { a, p, i };

            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == akademiEgitimID);
            int sureSaniye = egitim?.SureSaniye ?? 0;

            return query.ToList().Select(x => new
            {
                atamaID = x.a.AtamaID,
                sicilNo = x.a.SicilNo,
                adSoyad = x.p?.AdSoyad ?? "[Bulunamadi]",
                departman = x.p?.Departman ?? "",
                atamaTarihi = x.a.AtamaTarihi.ToString("dd/MM/yyyy"),
                sonTarih = x.a.SonTarih?.ToString("dd/MM/yyyy"),
                izlemeYuzdesi = sureSaniye > 0 && x.i != null
                    ? Math.Min(100, (int)Math.Round(100.0 * x.i.MaxIzlenenSaniye / sureSaniye))
                    : (x.i != null && x.i.TamamlandiMi ? 100 : 0),
                tamamlandiMi = x.i != null && x.i.TamamlandiMi,
                tamamlanmaTarihi = x.i?.TamamlanmaTarihi?.ToString("dd/MM/yyyy HH:mm")
            }).ToList();
        }

        // -------------------------------------------------------------
        // Faz 2 — Sınav motoru
        // -------------------------------------------------------------

        private class SoruSiraItem
        {
            public int SoruID { get; set; }
            public List<string> SecenekSira { get; set; } = new List<string>();
        }

        private class CevapItem
        {
            public int SoruID { get; set; }
            public string SecilenSecenek { get; set; } = "";
            public bool DogruMu { get; set; }
            public bool SureAsimiMi { get; set; }
        }

        public IEnumerable<object> GetCategories()
        {
            return _context.tb_AkademiKategori
                .Where(k => k.AktifMi)
                .OrderBy(k => k.Ad)
                .Select(k => new { k.KategoriID, k.Ad })
                .ToList();
        }

        public bool AddCategory(int kullaniciID, string ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) throw new Exception("Kategori adi bos olamaz.");
            if (_context.tb_AkademiKategori.Any(k => k.AktifMi && k.Ad.ToLower() == ad.Trim().ToLower()))
                throw new Exception("Bu kategori zaten mevcut.");

            _context.tb_AkademiKategori.Add(new tb_AkademiKategori { Ad = ad.Trim(), AktifMi = true });
            _context.SaveChanges();
            return true;
        }

        public bool SaveExamSettings(int kullaniciID, int akademiEgitimID, bool sinavAktif, int? soruSayisi, int soruSuresiSaniye, int gecmePuanYuzdesi)
        {
            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == akademiEgitimID);
            if (egitim == null) throw new Exception("Akademi egitimi bulunamadi.");

            egitim.SinavAktif = sinavAktif;
            egitim.SinavSoruSayisi = soruSayisi;
            egitim.SinavSoruSuresiSaniye = soruSuresiSaniye > 0 ? soruSuresiSaniye : 60;
            egitim.GecmePuanYuzdesi = gecmePuanYuzdesi > 0 ? gecmePuanYuzdesi : 70;
            _context.SaveChanges();
            return true;
        }

        public IEnumerable<object> GetQuestions(int akademiEgitimID)
        {
            return _context.tb_AkademiSoru
                .Where(s => s.AkademiEgitimID == akademiEgitimID && s.AktifMi)
                .OrderBy(s => s.SoruID)
                .Select(s => new
                {
                    s.SoruID,
                    s.SoruMetni,
                    s.SecenekA,
                    s.SecenekB,
                    s.SecenekC,
                    s.SecenekD,
                    s.DogruSecenek,
                    s.Puan
                })
                .ToList();
        }

        public bool SaveQuestion(int kullaniciID, int akademiEgitimID, string soruMetni, string secenekA, string secenekB, string? secenekC, string? secenekD, string dogruSecenek, int puan)
        {
            ValidateQuestion(soruMetni, secenekA, secenekB, secenekC, secenekD, dogruSecenek);
            _context.tb_AkademiSoru.Add(new tb_AkademiSoru
            {
                AkademiEgitimID = akademiEgitimID,
                SoruMetni = soruMetni.Trim(),
                SecenekA = secenekA.Trim(),
                SecenekB = secenekB.Trim(),
                SecenekC = string.IsNullOrWhiteSpace(secenekC) ? null : secenekC.Trim(),
                SecenekD = string.IsNullOrWhiteSpace(secenekD) ? null : secenekD.Trim(),
                DogruSecenek = dogruSecenek.Trim().ToUpper(),
                Puan = puan > 0 ? puan : 1,
                AktifMi = true,
                KayitTarihi = DateTime.Now
            });
            _context.SaveChanges();
            return true;
        }

        public bool UpdateQuestion(int kullaniciID, int soruID, string soruMetni, string secenekA, string secenekB, string? secenekC, string? secenekD, string dogruSecenek, int puan)
        {
            ValidateQuestion(soruMetni, secenekA, secenekB, secenekC, secenekD, dogruSecenek);
            var soru = _context.tb_AkademiSoru.FirstOrDefault(s => s.SoruID == soruID);
            if (soru == null) throw new Exception("Soru bulunamadi.");

            soru.SoruMetni = soruMetni.Trim();
            soru.SecenekA = secenekA.Trim();
            soru.SecenekB = secenekB.Trim();
            soru.SecenekC = string.IsNullOrWhiteSpace(secenekC) ? null : secenekC.Trim();
            soru.SecenekD = string.IsNullOrWhiteSpace(secenekD) ? null : secenekD.Trim();
            soru.DogruSecenek = dogruSecenek.Trim().ToUpper();
            soru.Puan = puan > 0 ? puan : 1;
            _context.SaveChanges();
            return true;
        }

        public bool DeleteQuestion(int kullaniciID, int soruID)
        {
            var soru = _context.tb_AkademiSoru.FirstOrDefault(s => s.SoruID == soruID);
            if (soru == null) throw new Exception("Soru bulunamadi.");
            soru.AktifMi = false;
            _context.SaveChanges();
            return true;
        }

        public int ImportQuestionsBulk(int kullaniciID, int akademiEgitimID, List<(string soruMetni, string a, string b, string? c, string? d, string dogru, int puan)> rows)
        {
            int eklenen = 0;
            foreach (var r in rows)
            {
                try
                {
                    ValidateQuestion(r.soruMetni, r.a, r.b, r.c, r.d, r.dogru);
                }
                catch { continue; } // gecersiz satiri atla, digerlerini durdurma

                _context.tb_AkademiSoru.Add(new tb_AkademiSoru
                {
                    AkademiEgitimID = akademiEgitimID,
                    SoruMetni = r.soruMetni.Trim(),
                    SecenekA = r.a.Trim(),
                    SecenekB = r.b.Trim(),
                    SecenekC = string.IsNullOrWhiteSpace(r.c) ? null : r.c!.Trim(),
                    SecenekD = string.IsNullOrWhiteSpace(r.d) ? null : r.d!.Trim(),
                    DogruSecenek = r.dogru.Trim().ToUpper(),
                    Puan = r.puan > 0 ? r.puan : 1,
                    AktifMi = true,
                    KayitTarihi = DateTime.Now
                });
                eklenen++;
            }
            if (eklenen > 0) _context.SaveChanges();
            return eklenen;
        }

        private static void ValidateQuestion(string soruMetni, string secenekA, string secenekB, string? secenekC, string? secenekD, string dogruSecenek)
        {
            if (string.IsNullOrWhiteSpace(soruMetni)) throw new Exception("Soru metni bos olamaz.");
            if (string.IsNullOrWhiteSpace(secenekA) || string.IsNullOrWhiteSpace(secenekB)) throw new Exception("A ve B secenekleri zorunludur.");
            string dogru = (dogruSecenek ?? "").Trim().ToUpper();
            var gecerliSecenekler = new List<string> { "A", "B" };
            if (!string.IsNullOrWhiteSpace(secenekC)) gecerliSecenekler.Add("C");
            if (!string.IsNullOrWhiteSpace(secenekD)) gecerliSecenekler.Add("D");
            if (!gecerliSecenekler.Contains(dogru)) throw new Exception("Dogru secenek gecerli bir secenege isaret etmiyor.");
        }

        // Sunucu-yetkili soru gorunumu — dogru cevabi asla client'a gondermez.
        private object BuildQuestionView(tb_AkademiSinavOturum oturum, tb_AkademiEgitim egitim)
        {
            var sira = JsonSerializer.Deserialize<List<SoruSiraItem>>(oturum.SoruSiraJson) ?? new List<SoruSiraItem>();
            var mevcut = sira[oturum.MevcutSoruIndex];
            var soru = _context.tb_AkademiSoru.FirstOrDefault(s => s.SoruID == mevcut.SoruID);
            if (soru == null) throw new Exception("Soru bulunamadi.");

            var secenekMetinleri = new Dictionary<string, string?> { { "A", soru.SecenekA }, { "B", soru.SecenekB }, { "C", soru.SecenekC }, { "D", soru.SecenekD } };
            var secenekler = new List<object>();
            for (int i = 0; i < mevcut.SecenekSira.Count; i++)
            {
                string displayHarf = ((char)('A' + i)).ToString();
                secenekler.Add(new { harf = displayHarf, metin = secenekMetinleri[mevcut.SecenekSira[i]] });
            }

            int gecenSaniye = (int)(DateTime.Now - oturum.SoruBaslangicZamani).TotalSeconds;
            int kalanSaniye = Math.Max(0, egitim.SinavSoruSuresiSaniye - gecenSaniye);

            return new
            {
                tamamlandiMi = false,
                soruNo = oturum.MevcutSoruIndex + 1,
                toplamSoru = sira.Count,
                soruMetni = soru.SoruMetni,
                secenekler,
                kalanSaniye,
                soruSuresiSaniye = egitim.SinavSoruSuresiSaniye,
                sekmeDegisimSayisi = oturum.SekmeDegisimSayisi
            };
        }

        // Oturum ACMADAN sinav bilgilerini doner — hazirlik/bilgilendirme ekrani icin.
        // Daha once tamamlanmis bir deneme varsa (ve tekrar hakki yoksa) bunu da bildirir.
        public object GetExamBrief(int atamaID, string sicilNo)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null) throw new Exception("Atama bulunamadi.");
            if (atama.SicilNo != sicilNo) throw new UnauthorizedAccessException("Bu atama size ait degil.");

            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == atama.AkademiEgitimID);
            if (egitim == null || !egitim.SinavAktif) throw new Exception("Bu egitim icin sinav tanimli degil.");

            int havuzSayisi = _context.tb_AkademiSoru.Count(s => s.AkademiEgitimID == atama.AkademiEgitimID && s.AktifMi);
            int soruSayisi = egitim.SinavSoruSayisi.HasValue && egitim.SinavSoruSayisi.Value < havuzSayisi ? egitim.SinavSoruSayisi.Value : havuzSayisi;

            int tamamlanmisDenemeSayisi = _context.tb_AkademiSinavSonuc.Count(s => s.AtamaID == atamaID);
            int izinliDenemeSayisi = 1 + atama.SinavEkHakSayisi;
            bool tekrarTalebiBekliyor = _context.tb_AkademiSinavTalep.Any(t => t.AtamaID == atamaID && t.Durum == "BEKLEMEDE");

            return new
            {
                soruSayisi,
                soruSuresiSaniye = egitim.SinavSoruSuresiSaniye,
                gecmePuanYuzdesi = egitim.GecmePuanYuzdesi,
                tekrarHakkiKalmadi = tamamlanmisDenemeSayisi >= izinliDenemeSayisi,
                tekrarTalebiBekliyor
            };
        }

        // Personel: hakki bitince sebep yazarak tekrar hakki talep eder — AKADEMI admin
        // belge turune sahip yonetici WebPortal'dan onaylar/reddeder (bkz. WebServiceAkademi.cs
        // ayni isim/mantiktaki AkademiSinavTekrarTalepEt).
        public object RequestExamRetry(int atamaID, string sicilNo, string sebep)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null) throw new Exception("Atama bulunamadi.");
            if (atama.SicilNo != sicilNo) throw new UnauthorizedAccessException("Bu atama size ait degil.");
            if (string.IsNullOrWhiteSpace(sebep)) throw new Exception("Lutfen talep sebebini yaziniz.");

            int tamamlanmisDenemeSayisi = _context.tb_AkademiSinavSonuc.Count(s => s.AtamaID == atamaID);
            if (tamamlanmisDenemeSayisi < 1 + atama.SinavEkHakSayisi)
                throw new Exception("Tekrar hakkiniz zaten var, talep gondermenize gerek yok.");
            if (_context.tb_AkademiSinavTalep.Any(t => t.AtamaID == atamaID && t.Durum == "BEKLEMEDE"))
                throw new Exception("Bu egitim icin zaten bekleyen bir talebiniz var.");

            _context.tb_AkademiSinavTalep.Add(new tb_AkademiSinavTalep
            {
                AtamaID = atamaID,
                TalepSebebi = sebep.Trim(),
                TalepTarihi = DateTime.Now,
                Durum = "BEKLEMEDE"
            });
            _context.SaveChanges();

            NotifyAkademiAdmins(sicilNo, atama.AkademiEgitimID);

            return new { success = true };
        }

        // AKADEMI admin belge turune sahip TUM kullanicilara (WebPortal'daki
        // ".Contains("*AKADEMI")" deseniyle AYNI sorgu) hem zil bildirimi (tb_Notification)
        // hem gercek push gonderir. WebServiceAkademi.cs'teki AkademiSinavTekrarTalepEt'in
        // mobil kaynakli talepler icin izole mantik tekrari — iki backend birbirine HTTP atmiyor.
        private void NotifyAkademiAdmins(string talepEdenSicilNo, int akademiEgitimID)
        {
            try
            {
                var talepEden = _context.tb_Personel.FirstOrDefault(p => p.SicilNo == talepEdenSicilNo);
                var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == akademiEgitimID);
                string adSoyad = talepEden?.AdSoyad ?? talepEdenSicilNo;
                string baslik = "Ek Sınav Hakkı Talebi";
                string mesaj = adSoyad + " - '" + (egitim?.Baslik ?? "") + "' eğitimi için ek sınav hakkı talep etti.";

                var adminSicilleri = _context.tb_Kullanici
                    .Where(u => u.Durum == true && u.AdminBelgeTur != null)
                    .Select(u => new { u.SicilNo, u.AdminBelgeTur })
                    .AsEnumerable()
                    .Where(u => AdminBelgeTuruHelper.HasYetki(u.AdminBelgeTur, "AKADEMI") && !string.IsNullOrEmpty(u.SicilNo))
                    .Select(u => u.SicilNo)
                    .ToList();

                foreach (var adminSicil in adminSicilleri)
                {
                    _context.tb_Notification.Add(new tb_Notification
                    {
                        SicilNo = adminSicil!,
                        Baslik = baslik,
                        Aciklama = mesaj,
                        LinkUrl = "Akademi/Default.html",
                        Kategori = "Akademi",
                        ReferansID = akademiEgitimID.ToString(),
                        Okundu = false,
                        KayitTarihi = DateTime.Now
                    });

                    _ = _push.SendToUserBySicilNoAsync(adminSicil!, baslik, mesaj, new { type = "akademiSinavTalep" });
                }
                _context.SaveChanges();
            }
            catch { }
        }

        public object StartOrResumeExam(int atamaID, string sicilNo)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null) throw new Exception("Atama bulunamadi.");
            if (atama.SicilNo != sicilNo) throw new UnauthorizedAccessException("Bu atama size ait degil.");

            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == atama.AkademiEgitimID);
            if (egitim == null || !egitim.SinavAktif) throw new Exception("Bu egitim icin sinav tanimli degil.");

            var ilerleme = _context.tb_AkademiIlerleme.FirstOrDefault(i => i.AtamaID == atamaID);
            if (ilerleme == null || !ilerleme.TamamlandiMi)
                throw new Exception("Sinava baslamadan once egitim icerigini tamamlamalisiniz.");

            // Tek-deneme kilidi: tamamlanmis deneme sayisi izinli hakki (1 + yonetici tarafindan
            // verilen ek hak) asmis/esitlemisse yeni oturum ACILMAZ. Yonetici WebPortal raporundan
            // "Sinavi Sifirla" ile SinavEkHakSayisi'ni artirarak tekrar hakki tanir.
            int tamamlanmisDenemeSayisi = _context.tb_AkademiSinavSonuc.Count(s => s.AtamaID == atamaID);
            if (tamamlanmisDenemeSayisi >= 1 + atama.SinavEkHakSayisi)
                throw new Exception("Sinavi zaten tamamladiniz. Tekrar hakki icin yoneticinizle iletisime gecin.");

            // Tek-oturum kilidi: tamamlanmamis mevcut oturum varsa onu devam ettir.
            var oturum = _context.tb_AkademiSinavOturum
                .Where(o => o.AtamaID == atamaID && !o.TamamlandiMi)
                .OrderByDescending(o => o.OturumID)
                .FirstOrDefault();

            if (oturum == null)
            {
                var havuz = _context.tb_AkademiSoru.Where(s => s.AkademiEgitimID == atama.AkademiEgitimID && s.AktifMi).ToList();
                if (havuz.Count == 0) throw new Exception("Bu egitim icin henuz soru tanimlanmamis.");

                var rnd = new Random();
                var secilenler = egitim.SinavSoruSayisi.HasValue && egitim.SinavSoruSayisi.Value < havuz.Count
                    ? havuz.OrderBy(_ => rnd.Next()).Take(egitim.SinavSoruSayisi.Value).ToList()
                    : havuz.OrderBy(_ => rnd.Next()).ToList();

                var sira = secilenler.Select(s =>
                {
                    var harfler = new List<string> { "A", "B" };
                    if (!string.IsNullOrWhiteSpace(s.SecenekC)) harfler.Add("C");
                    if (!string.IsNullOrWhiteSpace(s.SecenekD)) harfler.Add("D");
                    var karisik = harfler.OrderBy(_ => rnd.Next()).ToList();
                    return new SoruSiraItem { SoruID = s.SoruID, SecenekSira = karisik };
                }).ToList();

                oturum = new tb_AkademiSinavOturum
                {
                    AtamaID = atamaID,
                    SoruSiraJson = JsonSerializer.Serialize(sira),
                    MevcutSoruIndex = 0,
                    BaslangicZamani = DateTime.Now,
                    SoruBaslangicZamani = DateTime.Now,
                    SekmeDegisimSayisi = 0,
                    TamamlandiMi = false,
                    CevaplarJson = "[]"
                };
                _context.tb_AkademiSinavOturum.Add(oturum);
                _context.SaveChanges();
            }

            return BuildQuestionView(oturum, egitim);
        }

        public object SubmitAnswer(int atamaID, string sicilNo, string secilenSecenek)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null) throw new Exception("Atama bulunamadi.");
            if (atama.SicilNo != sicilNo) throw new UnauthorizedAccessException("Bu atama size ait degil.");

            var egitim = _context.tb_AkademiEgitim.FirstOrDefault(e => e.AkademiEgitimID == atama.AkademiEgitimID);
            if (egitim == null) throw new Exception("Egitim bulunamadi.");

            var oturum = _context.tb_AkademiSinavOturum
                .Where(o => o.AtamaID == atamaID && !o.TamamlandiMi)
                .OrderByDescending(o => o.OturumID)
                .FirstOrDefault();
            if (oturum == null) throw new Exception("Aktif sinav oturumu bulunamadi. Once sinavi baslatin.");

            var sira = JsonSerializer.Deserialize<List<SoruSiraItem>>(oturum.SoruSiraJson) ?? new List<SoruSiraItem>();
            var mevcut = sira[oturum.MevcutSoruIndex];
            var soru = _context.tb_AkademiSoru.FirstOrDefault(s => s.SoruID == mevcut.SoruID);
            if (soru == null) throw new Exception("Soru bulunamadi.");

            // Sunucu-yetkili sure kontrolu — istemci saati manipule edemez.
            int gecenSaniye = (int)(DateTime.Now - oturum.SoruBaslangicZamani).TotalSeconds;
            bool sureAsimiMi = gecenSaniye > (egitim.SinavSoruSuresiSaniye + 2); // 2sn ag toleransi

            // Gosterilen (karisik) harfi orijinal secenek harfine cevir.
            int displayIndex = "ABCD".IndexOf((secilenSecenek ?? "").Trim().ToUpper());
            string? orijinalHarf = (!sureAsimiMi && displayIndex >= 0 && displayIndex < mevcut.SecenekSira.Count)
                ? mevcut.SecenekSira[displayIndex] : null;
            bool dogruMu = !sureAsimiMi && orijinalHarf != null && orijinalHarf == soru.DogruSecenek;

            var cevaplar = JsonSerializer.Deserialize<List<CevapItem>>(oturum.CevaplarJson) ?? new List<CevapItem>();
            cevaplar.Add(new CevapItem { SoruID = soru.SoruID, SecilenSecenek = orijinalHarf ?? "", DogruMu = dogruMu, SureAsimiMi = sureAsimiMi });
            oturum.CevaplarJson = JsonSerializer.Serialize(cevaplar);
            oturum.MevcutSoruIndex++;

            if (oturum.MevcutSoruIndex >= sira.Count)
            {
                // Sinav bitti — puanla-ve-kaydet.
                var tumSorular = sira.Select(s => _context.tb_AkademiSoru.First(x => x.SoruID == s.SoruID)).ToList();
                int toplamPuan = tumSorular.Sum(s => s.Puan);
                int kazanilanPuan = 0;
                int dogruSayisi = 0;
                foreach (var c in cevaplar)
                {
                    if (c.DogruMu)
                    {
                        dogruSayisi++;
                        kazanilanPuan += tumSorular.First(s => s.SoruID == c.SoruID).Puan;
                    }
                }
                int puanYuzdesi = toplamPuan > 0 ? (int)Math.Round(100.0 * kazanilanPuan / toplamPuan) : 0;
                bool basariliMi = puanYuzdesi >= egitim.GecmePuanYuzdesi;

                oturum.TamamlandiMi = true;
                _context.tb_AkademiSinavSonuc.Add(new tb_AkademiSinavSonuc
                {
                    AtamaID = atamaID,
                    OturumID = oturum.OturumID,
                    DogruSayisi = dogruSayisi,
                    ToplamSoru = sira.Count,
                    PuanYuzdesi = puanYuzdesi,
                    BasariliMi = basariliMi,
                    SekmeDegisimSayisi = oturum.SekmeDegisimSayisi,
                    BaslangicZamani = oturum.BaslangicZamani,
                    BitisZamani = DateTime.Now
                });
                _context.SaveChanges();

                return new { tamamlandiMi = true, basariliMi, puanYuzdesi, dogruSayisi, toplamSoru = sira.Count, sekmeDegisimSayisi = oturum.SekmeDegisimSayisi };
            }

            oturum.SoruBaslangicZamani = DateTime.Now;
            _context.SaveChanges();
            return BuildQuestionView(oturum, egitim);
        }

        public bool ReportTabSwitch(int atamaID, string sicilNo)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null || atama.SicilNo != sicilNo) return false;

            var oturum = _context.tb_AkademiSinavOturum
                .Where(o => o.AtamaID == atamaID && !o.TamamlandiMi)
                .OrderByDescending(o => o.OturumID)
                .FirstOrDefault();
            if (oturum == null) return false;

            oturum.SekmeDegisimSayisi++;
            _context.SaveChanges();
            return true;
        }

        public object? GetExamResult(int atamaID, string sicilNo)
        {
            var atama = _context.tb_AkademiAtama.FirstOrDefault(a => a.AtamaID == atamaID);
            if (atama == null || atama.SicilNo != sicilNo) return null;

            var sonuc = _context.tb_AkademiSinavSonuc
                .Where(s => s.AtamaID == atamaID)
                .OrderByDescending(s => s.SonucID)
                .FirstOrDefault();
            if (sonuc == null) return null;

            return new
            {
                sonuc.DogruSayisi,
                sonuc.ToplamSoru,
                sonuc.PuanYuzdesi,
                sonuc.BasariliMi,
                sonuc.SekmeDegisimSayisi,
                bitisZamani = sonuc.BitisZamani.ToString("dd/MM/yyyy HH:mm")
            };
        }
    }
}
