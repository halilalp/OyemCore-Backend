using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // Proje / Toplantı yönetimi. Referans: ServiceToplantiIslemleri.
    // Faz 1 — okuma. Yazma (kaydet/görev ekle) sonraki aşamada.
    public class ProjeToplantiService : IProjeToplantiService
    {
        private readonly IYbsDbContext _context;

        public ProjeToplantiService(IYbsDbContext context)
        {
            _context = context;
        }

        private static string TurAdi(string tur) =>
            tur == "P" ? "PROJE" : (tur == "T" ? "TOPLANTI" : "GÖREVLENDİRME");

        // Görev durumlarından özet: tamamlanan / toplam (referans HesaplaOzet).
        private string Ozet(int toplantiId)
        {
            var durumlar = _context.tb_ToplantiGorev
                .Where(g => g.ToplantiID == toplantiId && g.GoruntuDurum == true)
                .Select(g => g.Durum)
                .ToList();
            if (durumlar.Count == 0) return "";
            int tamam = durumlar.Count(d => d == true);
            return $"{tamam}/{durumlar.Count}";
        }

        public IEnumerable<object> GetList(int userId, string konu, string durum, string tur)
        {
            var usr = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == userId);
            if (usr == null) return new List<object>();
            string eposta = usr.Eposta ?? "";
            var adminTurler = (usr.AdminBelgeTur ?? "").Split('*').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            // Yetki: sahibi + proje türü admini + katılımcı + görev sorumlusu
            var q = _context.tb_Toplanti.Where(i =>
                i.KullaniciEposta == eposta ||
                (i.ProjeTur != null && i.ProjeTur != "" && adminTurler.Contains(i.ProjeTur)) ||
                _context.tb_ToplantiKullanici.Any(o => o.ToplantiID == i.ID && o.KullaniciEposta == eposta) ||
                _context.tb_ToplantiGorev.Any(g => g.ToplantiID == i.ID && g.SorumluEposta == eposta));

            if (!string.IsNullOrEmpty(durum))
            {
                bool durumBool = durum == "TAMAMLANDI";
                q = q.Where(o => o.Durum == durumBool);
            }
            if (!string.IsNullOrEmpty(tur))
                q = q.Where(o => o.Tur == tur);

            var rawList = q.OrderByDescending(i => i.ID)
                .Select(i => new
                {
                    i.ID,
                    i.Konu,
                    i.Tur,
                    i.ProjeTur,
                    i.KullaniciEposta,
                    i.BasTarih,
                    i.BitTarih,
                    i.Durum
                }).ToList();

            var list = rawList.Select(i =>
            {
                var per = _context.tb_Personel.FirstOrDefault(p => p.Eposta == i.KullaniciEposta);
                return new
                {
                    id = i.ID,
                    konu = i.Konu ?? "",
                    tur = i.Tur ?? "",
                    turAdi = TurAdi(i.Tur),
                    projeTur = i.ProjeTur ?? "",
                    ad = per != null ? per.AdSoyad : (i.KullaniciEposta ?? ""),
                    sicilNo = per != null ? per.SicilNo : "",
                    kullaniciEposta = i.KullaniciEposta ?? "",
                    basTarih = i.BasTarih != null ? i.BasTarih.Value.ToString("dd.MM.yyyy") : "",
                    bitTarih = i.BitTarih != null ? i.BitTarih.Value.ToString("dd.MM.yyyy") : "",
                    durum = i.Durum == true ? "TAMAMLANDI" : "BEKLEMEDE",
                    ozet = Ozet(i.ID)
                };
            }).ToList();

            if (!string.IsNullOrEmpty(konu))
            {
                string s = konu.ToLower().Replace(" ", "");
                list = list.Where(o =>
                    o.konu.ToLower().Replace(" ", "").Contains(s) ||
                    o.ad.ToLower().Replace(" ", "").Contains(s) ||
                    o.kullaniciEposta.ToLower().Replace(" ", "").Contains(s)).ToList();
            }

            return list;
        }

        public object GetDetail(int userId, int toplantiId)
        {
            var usr = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == userId);
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == toplantiId);
            if (t == null) return null;

            var sahip = _context.tb_Personel.FirstOrDefault(p => p.Eposta == t.KullaniciEposta);

            var katilimcilar = (from k in _context.tb_ToplantiKullanici
                                where k.ToplantiID == toplantiId
                                join p in _context.tb_Personel on k.KullaniciEposta equals p.Eposta into ps
                                from p in ps.DefaultIfEmpty()
                                select new
                                {
                                    id = k.ID,
                                    eposta = k.KullaniciEposta ?? "",
                                    ad = p != null ? p.AdSoyad : k.KullaniciEposta,
                                    sicilNo = p != null ? p.SicilNo : ""
                                }).ToList();

            var gorevler = (from g in _context.tb_ToplantiGorev
                            where g.ToplantiID == toplantiId
                            join p in _context.tb_Personel on g.SorumluEposta equals p.Eposta into ps
                            from p in ps.DefaultIfEmpty()
                            orderby g.GorevNo
                            select new
                            {
                                id = g.ID,
                                gorevNo = g.GorevNo,
                                aciklama = g.Aciklama ?? "",
                                sorumluEposta = g.SorumluEposta ?? "",
                                sorumluAd = p != null ? p.AdSoyad : (g.SorumluEposta ?? "Atanmadı"),
                                terminTarStr = g.TerminTar != null ? g.TerminTar.Value.ToString("dd.MM.yyyy") : "",
                                baslamaTarStr = g.BaslamaTarihi != null ? g.BaslamaTarihi.Value.ToString("dd.MM.yyyy") : "",
                                durum = g.Durum == true ? "TAMAMLANDI" : "DEVAM EDİYOR",
                                revizyonAdet = g.RevizyonAdet ?? 0,
                                trlSeviyeKodu = g.TrlSeviyeKodu ?? ""
                            }).ToList();

            var dosyalar = (from d in _context.tb_ToplantiDosya
                            where d.ToplantiID == toplantiId
                            orderby d.ID descending
                            select new
                            {
                                id = d.ID,
                                baslik = d.DosyaBaslik ?? "",
                                aciklama = d.Aciklama ?? "",
                                dosyaUrl = d.DosyaUrl ?? "",
                                kayitTarStr = d.KayitTar != null ? d.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                            }).ToList();

            return DetailObject(usr, t, sahip, katilimcilar, gorevler, dosyalar);
        }

        private object DetailObject(tb_Kullanici usr, tb_Toplanti t, tb_Personel sahip,
            object katilimcilar, object gorevler, object dosyalar)
        {
            return new
            {
                toplanti = new
                {
                    id = t.ID,
                    konu = t.Konu ?? "",
                    aciklama = t.Aciklama ?? "",
                    tur = t.Tur ?? "",
                    turAdi = TurAdi(t.Tur),
                    projeTur = t.ProjeTur ?? "",
                    kullaniciEposta = t.KullaniciEposta ?? "",
                    olusturan = sahip != null ? sahip.AdSoyad : (t.KullaniciEposta ?? ""),
                    olusturanSicil = sahip != null ? sahip.SicilNo : "",
                    basTarihStr = t.BasTarih != null ? t.BasTarih.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    bitTarihStr = t.BitTarih != null ? t.BitTarih.Value.ToString("dd.MM.yyyy") : "",
                    kayitTarStr = t.KayitTar != null ? t.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    durum = t.Durum == true ? "TAMAMLANDI" : "BEKLEMEDE",
                    // Sadece oluşturan yönetebilir (referans Adm)
                    yonetebilir = usr != null && t.KullaniciEposta == usr.Eposta
                },
                katilimcilar,
                gorevler,
                dosyalar
            };
        }

        // dd/MM/yyyy veya dd.MM.yyyy → DateTime (referans ParseDateTimeRobust)
        private static DateTime? ParseTarih(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var p = s.Replace("/", ".").Split(' ')[0].Split('.');
            if (p.Length == 3 && int.TryParse(p[0], out int g) && int.TryParse(p[1], out int a) && int.TryParse(p[2], out int y))
                return new DateTime(y, a, g);
            return DateTime.TryParse(s, out var d) ? d : (DateTime?)null;
        }

        private tb_Kullanici GetUser(int userId) =>
            _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == userId)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        public int Create(int userId, string tur, string projeTur, string konu, string aciklama,
                          string basTarih, string bitTarih, List<string> katilimciEpostalar)
        {
            var usr = GetUser(userId);
            if (string.IsNullOrWhiteSpace(konu)) throw new InvalidOperationException("Konu boş bırakılamaz.");
            if (string.IsNullOrWhiteSpace(basTarih)) throw new InvalidOperationException("Başlangıç tarihi boş bırakılamaz.");
            if (tur == "P" && string.IsNullOrWhiteSpace(bitTarih))
                throw new InvalidOperationException("Bitiş tarihi boş bırakılamaz.");

            var t = new tb_Toplanti
            {
                Tur = tur,
                ProjeTur = (projeTur == "0" || string.IsNullOrEmpty(projeTur)) ? null : projeTur,
                Konu = konu,
                Aciklama = aciklama,
                Durum = false,
                KullaniciEposta = usr.Eposta,
                BasTarih = ParseTarih(basTarih),
                BitTarih = ParseTarih(bitTarih),
                KayitTar = DateTime.Now
            };
            _context.tb_Toplanti.Add(t);
            _context.SaveChanges();

            if (katilimciEpostalar != null)
            {
                foreach (var e in katilimciEpostalar.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                {
                    bool aktif = _context.tb_Personel.Any(p => p.Eposta == e && p.Durum == true);
                    if (aktif && !_context.tb_ToplantiKullanici.Any(k => k.ToplantiID == t.ID && k.KullaniciEposta == e))
                        _context.tb_ToplantiKullanici.Add(new tb_ToplantiKullanici { ToplantiID = t.ID, KullaniciEposta = e });
                }
                _context.SaveChanges();
            }
            return t.ID;
        }

        public void UpdateDurum(int userId, int toplantiId, bool durum)
        {
            var usr = GetUser(userId);
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == toplantiId)
                ?? throw new InvalidOperationException("Kayıt bulunamadı.");
            if (t.KullaniciEposta != usr.Eposta)
                throw new InvalidOperationException("Durumu sadece kaydı oluşturan personel değiştirebilir.");
            t.Durum = durum;
            t.DurumTar = DateTime.Now;
            _context.SaveChanges();
        }

        public int AddGorev(int userId, int toplantiId, string aciklama, string sorumluEposta,
                            string terminTar, string baslamaTar, string trl)
        {
            var usr = GetUser(userId);
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == toplantiId)
                ?? throw new InvalidOperationException("Kayıt bulunamadı.");

            bool sahip = t.KullaniciEposta == usr.Eposta;
            bool katilimci = _context.tb_ToplantiKullanici.Any(k => k.ToplantiID == toplantiId && k.KullaniciEposta == usr.Eposta);
            if (!sahip && !katilimci)
                throw new InvalidOperationException("Görevi sadece oluşturan personel ya da katılımcılar ekleyebilir.");
            if (string.IsNullOrWhiteSpace(aciklama)) throw new InvalidOperationException("Görev açıklaması boş olamaz.");
            if (string.IsNullOrWhiteSpace(sorumluEposta)) throw new InvalidOperationException("Sorumlu seçilmelidir.");

            var basla = ParseTarih(baslamaTar);
            var termin = ParseTarih(terminTar);
            if (basla != null && t.BasTarih != null && basla < t.BasTarih)
                throw new InvalidOperationException($"Görev başlangıcı proje başlangıcından ({t.BasTarih.Value:dd.MM.yyyy}) önce olamaz.");
            if (termin != null && t.BitTarih != null && termin > t.BitTarih)
                throw new InvalidOperationException($"Görev bitişi proje bitişinden ({t.BitTarih.Value:dd.MM.yyyy}) sonra olamaz.");

            // EF Core DefaultIfEmpty().Max()'ı SQL'e çeviremiyor; nullable Max + ?? 0 çevrilir.
            int sonNo = _context.tb_ToplantiGorev.Where(g => g.ToplantiID == toplantiId).Max(g => (int?)g.GorevNo) ?? 0;
            var tg = new tb_ToplantiGorev
            {
                ToplantiID = toplantiId,
                GorevNo = sonNo + 1,
                Aciklama = aciklama,
                SorumluEposta = sorumluEposta,
                KayitEposta = usr.Eposta,
                KayitTar = DateTime.Now,
                TerminTar = termin,
                BaslamaTarihi = basla,
                Durum = false,
                GoruntuDurum = true,
                RevizyonAdet = 0,
                TrlSeviyeKodu = string.IsNullOrEmpty(trl) ? null : trl
            };
            _context.tb_ToplantiGorev.Add(tg);
            _context.SaveChanges();
            return tg.ID;
        }

        public void CompleteGorev(int userId, int gorevId)
        {
            var usr = GetUser(userId);
            var g = _context.tb_ToplantiGorev.FirstOrDefault(o => o.ID == gorevId)
                ?? throw new InvalidOperationException("Görev bulunamadı.");
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == g.ToplantiID);
            if (t != null && t.KullaniciEposta != usr.Eposta && g.KayitEposta != usr.Eposta)
                throw new InvalidOperationException("Görev durumunu sadece kaydı oluşturan personel değiştirebilir.");
            g.Durum = true;
            g.OnayTar = DateTime.Now;
            _context.SaveChanges();
        }

        public void DeleteGorev(int userId, int gorevId)
        {
            var usr = GetUser(userId);
            var g = _context.tb_ToplantiGorev.FirstOrDefault(o => o.ID == gorevId)
                ?? throw new InvalidOperationException("Görev bulunamadı.");
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == g.ToplantiID);
            if (t != null && t.KullaniciEposta != usr.Eposta && g.KayitEposta != usr.Eposta)
                throw new InvalidOperationException("Görevi sadece oluşturan personel silebilir.");
            _context.tb_ToplantiGorev.Remove(g);
            _context.SaveChanges();
        }

        public void AddKatilimci(int userId, int toplantiId, string eposta)
        {
            var usr = GetUser(userId);
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == toplantiId)
                ?? throw new InvalidOperationException("Kayıt bulunamadı.");
            if (t.KullaniciEposta != usr.Eposta)
                throw new InvalidOperationException("Katılımcıyı sadece kaydı oluşturan personel ekleyebilir.");
            if (!_context.tb_Personel.Any(p => p.Eposta == eposta && p.Durum == true))
                throw new InvalidOperationException("Aktif personel bulunamadı.");
            if (_context.tb_ToplantiKullanici.Any(k => k.ToplantiID == toplantiId && k.KullaniciEposta == eposta))
                throw new InvalidOperationException("Bu katılımcı zaten ekli.");
            _context.tb_ToplantiKullanici.Add(new tb_ToplantiKullanici { ToplantiID = toplantiId, KullaniciEposta = eposta });
            _context.SaveChanges();
        }

        public void RemoveKatilimci(int userId, int katilimciId)
        {
            var usr = GetUser(userId);
            var k = _context.tb_ToplantiKullanici.FirstOrDefault(o => o.ID == katilimciId)
                ?? throw new InvalidOperationException("Katılımcı bulunamadı.");
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == k.ToplantiID);
            if (t != null && t.KullaniciEposta != usr.Eposta)
                throw new InvalidOperationException("Katılımcıyı sadece kaydı oluşturan personel çıkarabilir.");
            _context.tb_ToplantiKullanici.Remove(k);
            _context.SaveChanges();
        }

        public IEnumerable<object> GetAktifPersoneller(string arama)
        {
            var q = _context.tb_Personel.Where(p => p.Durum == true && p.Eposta != null && p.Eposta != "");
            if (!string.IsNullOrWhiteSpace(arama))
            {
                var a = arama.Trim();
                q = q.Where(p => p.AdSoyad.Contains(a) || p.Eposta.Contains(a) || p.SicilNo.Contains(a));
            }
            return q.OrderBy(p => p.AdSoyad).Take(300)
                .Select(p => new { eposta = p.Eposta, ad = p.AdSoyad, sicilNo = p.SicilNo })
                .ToList();
        }

        public void AddDosya(int userId, int toplantiId, string baslik, string dosyaUrl)
        {
            var usr = GetUser(userId);
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == toplantiId)
                ?? throw new InvalidOperationException("Kayıt bulunamadı.");
            if (string.IsNullOrWhiteSpace(dosyaUrl)) throw new InvalidOperationException("Dosya seçilmedi.");
            _context.tb_ToplantiDosya.Add(new tb_ToplantiDosya
            {
                ToplantiID = toplantiId,
                DosyaBaslik = string.IsNullOrWhiteSpace(baslik) ? "Dosya" : baslik,
                DosyaUrl = dosyaUrl,
                KayitEposta = usr.Eposta,
                KayitTar = DateTime.Now
            });
            _context.SaveChanges();
        }

        public void DeleteDosya(int userId, int dosyaId)
        {
            var usr = GetUser(userId);
            var d = _context.tb_ToplantiDosya.FirstOrDefault(o => o.ID == dosyaId)
                ?? throw new InvalidOperationException("Dosya bulunamadı.");
            var t = _context.tb_Toplanti.FirstOrDefault(o => o.ID == d.ToplantiID);
            if (t != null && t.KullaniciEposta != usr.Eposta && d.KayitEposta != usr.Eposta)
                throw new InvalidOperationException("Dosyayı sadece ekleyen veya kaydı oluşturan silebilir.");
            _context.tb_ToplantiDosya.Remove(d);
            _context.SaveChanges();
        }
    }
}
