using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class EgitimService : IEgitimService
    {
        private readonly IYbsDbContext _context;

        public EgitimService(IYbsDbContext context)
        {
            _context = context;
        }

        public IEnumerable<object> GetEgitimList(int kullaniciID, string search)
        {
            // Tüm kayıtlar listelenir; oluşturma/düzenleme/silme sahiplik kontrolüyle kısıtlıdır.
            var query = from e in _context.tb_Egitim
                        join ek in _context.tb_EgitimKategori on e.KategoriID equals ek.KategoriID into eks
                        from ek in eks.DefaultIfEmpty()
                        join p in _context.tb_Personel on e.KayitEposta equals p.Eposta into ps
                        from p in ps.DefaultIfEmpty()
                        select new { e, ek, p };

            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.e.Konu.Contains(search));

            var list = query.OrderByDescending(x => x.e.KayitTar).ToList();

            return list.Select(x => new
            {
                id = x.e.EgitimID,
                konu = x.e.Konu ?? "",
                dosyaUrl = x.e.DosyaUrl ?? "",
                kayitEposta = x.e.KayitEposta ?? "",
                tarih = x.e.KayitTar != null ? x.e.KayitTar.Value.ToString("dd/MM/yyyy") : "",
                kategori = x.ek != null ? x.ek.Tanim : "Genel",
                adSoyad = x.p != null ? x.p.AdSoyad : "[Bulunamadi]",
                kategoriID = x.e.KategoriID ?? 0
            }).ToList();
        }

        public IEnumerable<object> GetEgitimCategories()
        {
            // Tablo boşsa varsayılan kategorileri oluştur
            if (!_context.tb_EgitimKategori.Any())
            {
                var defaults = new[] { "Genel", "Teknik", "Yönetim", "Sağlık ve Güvenlik", "Mesleki Gelişim" };
                foreach (var t in defaults)
                    _context.tb_EgitimKategori.Add(new tb_EgitimKategori { Tanim = t });
                _context.SaveChanges();
            }
            return _context.tb_EgitimKategori
                .OrderBy(k => k.Tanim)
                .ToList()
                .Select(k => new { kategoriID = k.KategoriID, tanim = k.Tanim ?? "" })
                .ToList();
        }

        public bool SaveEgitim(int kullaniciID, string konu, string aciklama, int kategoriID, string dosyaUrl)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new Exception("Kullanici bulunamadi.");

            var egitim = new tb_Egitim
            {
                Konu = konu,
                DosyaUrl = dosyaUrl,
                KategoriID = kategoriID,
                KayitEposta = user.Eposta,
                KayitTar = DateTime.Now
            };

            _context.tb_Egitim.Add(egitim);
            _context.SaveChanges();
            return true;
        }

        public bool UpdateEgitim(int kullaniciID, int egitimID, string konu, string aciklama, int kategoriID, string dosyaUrl)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new Exception("Kullanici bulunamadi.");

            var egitim = _context.tb_Egitim.FirstOrDefault(e => e.EgitimID == egitimID);
            if (egitim == null) throw new Exception("Egitim bulunamadi.");

            if (egitim.KayitEposta != user.Eposta)
                throw new UnauthorizedAccessException("Bu egitimi duzenleme yetkiniz yok.");

            egitim.Konu = konu;
            egitim.DosyaUrl = dosyaUrl;
            egitim.KategoriID = kategoriID;

            _context.SaveChanges();
            return true;
        }

        public bool DeleteEgitim(int kullaniciID, int egitimID)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new Exception("Kullanici bulunamadi.");

            var egitim = _context.tb_Egitim.FirstOrDefault(e => e.EgitimID == egitimID);
            if (egitim == null) throw new Exception("Egitim bulunamadi.");

            if (egitim.KayitEposta != user.Eposta)
                throw new UnauthorizedAccessException("Bu egitimi silme yetkiniz yok.");

            _context.tb_Egitim.Remove(egitim);
            _context.SaveChanges();
            return true;
        }
    }
}
