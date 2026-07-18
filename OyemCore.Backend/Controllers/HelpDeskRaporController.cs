using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Referans WebServiceHelpDeskRapor.HelpDeskPerformansRaporuGetir ile birebir.
    // IT/ERP HelpDesk personel performans raporu (talepTur parametresine göre).
    [Route("api/helpdeskrapor")]
    [ApiController]
    [Authorize]
    public class HelpDeskRaporController : ControllerBase
    {
        private readonly IYbsDbContext _context;
        public HelpDeskRaporController(IYbsDbContext context) { _context = context; }

        [HttpGet("performans")]
        public IActionResult Performans([FromQuery] string yil = "", [FromQuery] string ay = "", [FromQuery] string talepTur = "IT", [FromQuery] string sirket = "")
        {
            try
            {
                int yilNum = 2026;
                int.TryParse(yil, out yilNum);
                if (yilNum <= 0) yilNum = DateTime.Now.Year;

                DateTime t1, t2;
                if (ay == "Tümü" || string.IsNullOrEmpty(ay))
                {
                    t1 = new DateTime(yilNum, 1, 1);
                    t2 = new DateTime(yilNum, 12, 31, 23, 59, 59);
                }
                else
                {
                    int ayNum = 1;
                    int.TryParse(ay, out ayNum);
                    if (ayNum < 1 || ayNum > 12) ayNum = 1;
                    t1 = new DateTime(yilNum, ayNum, 1);
                    t2 = new DateTime(yilNum, ayNum, DateTime.DaysInMonth(yilNum, ayNum), 23, 59, 59);
                }

                var sourceList = _context.tb_Talep.AsNoTracking()
                    .Where(t => t.KayitTar >= t1 && t.KayitTar <= t2 && t.TalepTurKodu == talepTur)
                    .ToList();

                if (!string.IsNullOrEmpty(sirket))
                {
                    var kodlar = _context.tb_TalepBakim.AsNoTracking().Where(b => b.SirketKodu == sirket).Select(b => b.TalepKodu).ToHashSet();
                    sourceList = sourceList.Where(t => kodlar.Contains(t.TalepKodu)).ToList();
                }

                int talepSayisi = sourceList.Count;
                int acikTalep = sourceList.Count(x => x.Durum == false);
                int tamamlananTalep = sourceList.Count(x => x.Durum == true);

                double tamamlamaSuresi = 0;
                var tamamlanmis = sourceList.Where(x => x.Durum == true && x.IslemSure.HasValue && x.IslemSure > 0).ToList();
                if (tamamlanmis.Any()) tamamlamaSuresi = tamamlanmis.Average(x => x.IslemSure.Value) / 60.0;

                double mudahaleSuresi = 0;
                var mudahaleli = sourceList.Where(x => x.KilitSure.HasValue && x.KilitSure > 0).ToList();
                if (mudahaleli.Any()) mudahaleSuresi = mudahaleli.Average(x => x.KilitSure.Value) / 60.0;

                int personelSayisi = sourceList.Where(x => !string.IsNullOrEmpty(x.SorumluSicil)).Select(x => x.SorumluSicil).Distinct().Count();
                double ortIsYuku = personelSayisi > 0 ? (double)acikTalep / personelSayisi : 0;

                var sicilNos = sourceList.Where(x => !string.IsNullOrEmpty(x.SorumluSicil)).Select(x => x.SorumluSicil).Distinct().ToList();
                var personelDict = _context.tb_Personel.AsNoTracking().Where(p => sicilNos.Contains(p.SicilNo))
                    .Where(p => p.SicilNo != null).ToList().GroupBy(p => p.SicilNo).ToDictionary(g => g.Key, g => g.First().AdSoyad);

                var personeller = sourceList.Where(x => !string.IsNullOrEmpty(x.SorumluSicil))
                    .GroupBy(x => x.SorumluSicil)
                    .Select(g =>
                    {
                        var mud = g.Where(x => x.KilitSure.HasValue && x.KilitSure > 0).ToList();
                        var tam = g.Where(x => x.Durum == true && x.IslemSure.HasValue && x.IslemSure > 0).ToList();
                        var skorlu = g.Where(x => x.Skor.HasValue && x.Skor > 0).ToList();
                        return new
                        {
                            sicil = g.Key,
                            name = personelDict.TryGetValue(g.Key, out var n) ? n : "Bilinmiyor",
                            title = "HelpDesk Personeli",
                            dept = "-",
                            openTasks = g.Count(x => x.Durum == false),
                            tamamlanan = g.Count(x => x.Durum == true),
                            bekleyen = g.Count(x => x.Durum == false),
                            onayBekleyen = 0,
                            avgMudahale = mud.Any() ? (mud.Average(x => x.KilitSure.Value) / 60.0).ToString("0.0", CultureInfo.InvariantCulture) + " Saat" : "0.0 Saat",
                            avgTamamlama = tam.Any() ? (tam.Average(x => x.IslemSure.Value) / 60.0).ToString("0.0", CultureInfo.InvariantCulture) + " Saat" : "0.0 Saat",
                            ortalamaHizDouble = tam.Any() ? (tam.Average(x => x.IslemSure.Value) / 60.0) : 0,
                            cost = "-",
                            rating = skorlu.Any() ? skorlu.Average(x => x.Skor.Value) : 0
                        };
                    }).ToList();

                return Ok(new
                {
                    Kpi = new
                    {
                        talepSayisi,
                        acikTalep,
                        tamamlananTalep,
                        onayBekleyen = 0,
                        bekleyenTalep = acikTalep,
                        tamamlamaSuresi,
                        mudahaleSuresi,
                        ortIsYuku
                    },
                    Personel = personeller
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"HelpDesk performans raporu alinirken hata olustu: {ex.Message}" });
            }
        }
    }
}
