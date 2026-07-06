using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Contexts;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class TakvimService : ITakvimService
    {
        private readonly IYbsDbContext _context;

        public TakvimService(IYbsDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TakvimDto>> GetTakvimEventsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = from t in _context.tb_Takvim
                        join a in _context.tb_TakvimAyar on t.AyarID equals a.AyarID into t_a
                        from a in t_a.DefaultIfEmpty()
                        where (!startDate.HasValue || t.BasTar >= startDate.Value) &&
                              (!endDate.HasValue || t.BasTar <= endDate.Value)
                        orderby t.BasTar descending
                        select new TakvimDto
                        {
                            TakvimID = t.TakvimID,
                            AyarID = t.AyarID,
                            MasterID = t.MasterID,
                            Konu = t.Konu,
                            KayitSicil = t.KayitSicil,
                            BasTar = t.BasTar,
                            BitTar = t.BitTar,
                            Katilimci = t.Katilimci,
                            Aciklama = t.Aciklama,
                            BgColor = a != null ? a.BgColor : "#0F172A", // fallback color
                            BrColor = a != null ? a.BrColor : "#0F172A"
                        };

            return await query.ToListAsync();
        }

        public async Task<TakvimDto> GetTakvimEventByIdAsync(int id)
        {
            var query = from t in _context.tb_Takvim
                        join a in _context.tb_TakvimAyar on t.AyarID equals a.AyarID into t_a
                        from a in t_a.DefaultIfEmpty()
                        where t.TakvimID == id
                        select new TakvimDto
                        {
                            TakvimID = t.TakvimID,
                            AyarID = t.AyarID,
                            MasterID = t.MasterID,
                            Konu = t.Konu,
                            KayitSicil = t.KayitSicil,
                            BasTar = t.BasTar,
                            BitTar = t.BitTar,
                            Katilimci = t.Katilimci,
                            Aciklama = t.Aciklama,
                            BgColor = a != null ? a.BgColor : "#0F172A",
                            BrColor = a != null ? a.BrColor : "#0F172A"
                        };

            return await query.FirstOrDefaultAsync();
        }

        public async Task<TakvimDto> CreateTakvimEventAsync(TakvimDto dto)
        {
            var entity = new tb_Takvim
            {
                AyarID = dto.AyarID,
                MasterID = dto.MasterID,
                Konu = dto.Konu,
                KayitSicil = dto.KayitSicil,
                BasTar = dto.BasTar,
                BitTar = dto.BitTar,
                Katilimci = dto.Katilimci,
                Aciklama = dto.Aciklama
            };

            _context.tb_Takvim.Add(entity);
            
            // Note: Saving changes via a cast since IYbsDbContext has SaveChangesAsync from DbContext
            var dbContext = _context as DbContext;
            if (dbContext != null)
            {
                await dbContext.SaveChangesAsync();
            }

            dto.TakvimID = entity.TakvimID;

            // Recurrence support
            if (dto.TekrarSayisi.HasValue && dto.TekrarSayisi.Value > 1 && 
                (dto.Periyot == "W" || dto.Periyot == "M") && dto.BasTar.HasValue && dto.BitTar.HasValue)
            {
                int masterID = entity.TakvimID;
                for (int i = 1; i < dto.TekrarSayisi.Value; i++)
                {
                    DateTime nBas = dto.Periyot == "W" ? dto.BasTar.Value.AddDays(i * 7) : dto.BasTar.Value.AddMonths(i);
                    DateTime nBit = dto.Periyot == "W" ? dto.BitTar.Value.AddDays(i * 7) : dto.BitTar.Value.AddMonths(i);

                    var recurringEntity = new tb_Takvim
                    {
                        AyarID = dto.AyarID,
                        MasterID = masterID,
                        Konu = dto.Konu,
                        KayitSicil = dto.KayitSicil,
                        BasTar = nBas,
                        BitTar = nBit,
                        Katilimci = dto.Katilimci,
                        Aciklama = dto.Aciklama
                    };
                    _context.tb_Takvim.Add(recurringEntity);
                }

                if (dbContext != null)
                {
                    await dbContext.SaveChangesAsync();
                }
            }

            return dto;
        }

        public async Task<bool> UpdateTakvimEventAsync(int id, TakvimDto dto)
        {
            var entity = await _context.tb_Takvim.FindAsync(id);
            if (entity == null) return false;

            DateTime? oldBas = entity.BasTar;
            DateTime? oldBit = entity.BitTar;

            entity.AyarID = dto.AyarID;
            entity.MasterID = dto.MasterID;
            entity.Konu = dto.Konu;
            entity.KayitSicil = dto.KayitSicil;
            entity.BasTar = dto.BasTar;
            entity.BitTar = dto.BitTar;
            entity.Katilimci = dto.Katilimci;
            entity.Aciklama = dto.Aciklama;

            var dbContext = _context as DbContext;

            // Series update logic: if this is a master event (i.e., MasterID is null), update all repeating events
            if (entity.MasterID == null && dto.BasTar.HasValue && dto.BitTar.HasValue && oldBas.HasValue && oldBit.HasValue)
            {
                var duration = dto.BitTar.Value - dto.BasTar.Value;
                var linkedEvents = await _context.tb_Takvim.Where(t => t.MasterID == entity.TakvimID).ToListAsync();
                foreach (var b in linkedEvents)
                {
                    b.Konu = dto.Konu;
                    b.Aciklama = dto.Aciklama;
                    b.AyarID = dto.AyarID;
                    b.Katilimci = dto.Katilimci;
                    
                    // Sync time: keep the day of the linked event, but set the hour/minute from the new start time
                    if (b.BasTar.HasValue)
                    {
                        b.BasTar = new DateTime(b.BasTar.Value.Year, b.BasTar.Value.Month, b.BasTar.Value.Day, dto.BasTar.Value.Hour, dto.BasTar.Value.Minute, 0);
                        b.BitTar = b.BasTar.Value.Add(duration);
                    }
                }
            }

            if (dbContext != null)
            {
                await dbContext.SaveChangesAsync();
            }
            return true;
        }

        public async Task<bool> DeleteTakvimEventAsync(int id)
        {
            var entity = await _context.tb_Takvim.FindAsync(id);
            if (entity == null) return false;

            // Delete associated events if this is a master event
            if (entity.MasterID == null)
            {
                var linkedEvents = await _context.tb_Takvim.Where(t => t.MasterID == entity.TakvimID).ToListAsync();
                _context.tb_Takvim.RemoveRange(linkedEvents);
            }

            _context.tb_Takvim.Remove(entity);
            
            var dbContext = _context as DbContext;
            if (dbContext != null)
            {
                await dbContext.SaveChangesAsync();
            }
            return true;
        }

        public async Task<IEnumerable<tb_TakvimAyar>> GetCategoriesAsync()
        {
            return await _context.tb_TakvimAyar.Where(a => a.Durum == true).ToListAsync();
        }
    }
}
