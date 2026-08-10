using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Contexts;
using OyemCore.DataLayer.Entities;

namespace OyemCore.BusinessLayer.Services
{
    public class BordroService : IBordroService
    {
        private readonly YbsDbContext _dbContext;

        public BordroService(YbsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public (int total, IEnumerable<object> data) GetBordroListesiUser(int kullaniciID, int pageIndex, int pageSize)
        {
            var user = _dbContext.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null || string.IsNullOrEmpty(user.SicilNo))
            {
                return (0, Enumerable.Empty<object>());
            }

            var query = _dbContext.tb_Bordro
                .Where(b => b.SicilNo == user.SicilNo)
                .OrderByDescending(b => b.Donem);

            int totalCount = query.Count();
            var data = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            return (totalCount, data);
        }

        public bool BordroAksiyonKaydet(int kullaniciID, int bordroID, string aksiyon)
        {
            var user = _dbContext.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            if (user == null)
            {
                return false;
            }

            var bordro = _dbContext.tb_Bordro.FirstOrDefault(b => b.BordroID == bordroID && b.SicilNo == user.SicilNo);
            if (bordro == null)
            {
                return false;
            }

            if (aksiyon == "OKUDU")
            {
                if (bordro.Durum == "Bekliyor" || string.IsNullOrEmpty(bordro.Durum))
                {
                    bordro.OkunmaTarihi = DateTime.Now;
                    bordro.Durum = "Okundu";
                }
            }
            else if (aksiyon == "ONAYLADI")
            {
                bordro.OnayTarihi = DateTime.Now;
                bordro.Durum = "Onaylandı";
            }
            else
            {
                return false;
            }

            _dbContext.SaveChanges();
            return true;
        }
    }
}
