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
    }
}
