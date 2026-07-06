using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class HaberService : IHaberService
    {
        private readonly IYbsDbContext _context;

        public HaberService(IYbsDbContext context)
        {
            _context = context;
        }

        public IEnumerable<object> GetHaberList(int kullaniciID, string search, string startDate, string endDate)
        {
            // Tüm kayıtlar listelenir; düzenleme/silme sahiplik kontrolüyle kısıtlıdır.
            var query = _context.tb_Haber.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(h => h.Konu.Contains(search));

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime sd))
                query = query.Where(h => h.KayitTar >= sd);

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime ed))
                query = query.Where(h => h.KayitTar <= ed.AddDays(1));

            return query.OrderByDescending(h => h.HaberID).ToList().Select(h => new
            {
                id = h.HaberID,
                konu = h.Konu ?? "",
                profilUrl = h.ProfilUrl ?? "",
                kayitEposta = h.KayitEposta ?? "",
                tarih = h.KayitTar != null ? h.KayitTar.Value.ToString("dd/MM/yyyy") : "",
                aciklama = h.Aciklama ?? ""
            }).ToList();
        }

        public object GetHaberDetail(int id)
        {
            var h = _context.tb_Haber.FirstOrDefault(x => x.HaberID == id);
            if (h == null) return null;

            return new
            {
                id = h.HaberID,
                konu = h.Konu ?? "",
                profilUrl = h.ProfilUrl ?? "",
                kayitEposta = h.KayitEposta ?? "",
                tarih = h.KayitTar != null ? h.KayitTar.Value.ToString("dd/MM/yyyy") : "",
                aciklama = h.Aciklama ?? ""
            };
        }

        public bool SaveHaber(int kullaniciID, string konu, string aciklama, string profilUrl)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new Exception("Kullanici bulunamadi.");

            var haber = new tb_Haber
            {
                Konu = konu,
                Aciklama = aciklama,
                ProfilUrl = profilUrl,
                KayitEposta = user.Eposta,
                KayitTar = DateTime.Now
            };

            _context.tb_Haber.Add(haber);
            _context.SaveChanges();
            return true;
        }

        public bool UpdateHaber(int kullaniciID, int haberID, string konu, string aciklama, string profilUrl)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new Exception("Kullanici bulunamadi.");

            var haber = _context.tb_Haber.FirstOrDefault(h => h.HaberID == haberID);
            if (haber == null) throw new Exception("Haber bulunamadi.");

            if (haber.KayitEposta != user.Eposta)
                throw new UnauthorizedAccessException("Bu haberi duzenleme yetkiniz yok.");

            haber.Konu = konu;
            haber.Aciklama = aciklama;
            haber.ProfilUrl = profilUrl;

            _context.SaveChanges();
            return true;
        }

        public bool DeleteHaber(int kullaniciID, int haberID)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null) throw new Exception("Kullanici bulunamadi.");

            var haber = _context.tb_Haber.FirstOrDefault(h => h.HaberID == haberID);
            if (haber == null) throw new Exception("Haber bulunamadi.");

            if (haber.KayitEposta != user.Eposta)
                throw new UnauthorizedAccessException("Bu haberi silme yetkiniz yok.");

            _context.tb_Haber.Remove(haber);
            _context.SaveChanges();
            return true;
        }
    }
}
