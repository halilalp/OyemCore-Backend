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
    // Referans WebServicePersonel.IKDashboardVerisiGetir ile birebir aynı hesaplama.
    // İK (insan kaynakları) dashboard'u: turnover, aktif personel, cinsiyet/yaş/kıdem/
    // yaka/şirket/departman/ünvan dağılımları.
    [Route("api/ik")]
    [ApiController]
    [Authorize]
    public class IKController : ControllerBase
    {
        private readonly IYbsDbContext _context;
        public IKController(IYbsDbContext context) { _context = context; }

        [HttpGet("dashboard")]
        public IActionResult GetDashboard([FromQuery] string basTar = "", [FromQuery] string bitTar = "", [FromQuery] string yaka = "", [FromQuery] string sirketFilter = "")
        {
            try
            {
                var tr = new CultureInfo("tr-TR");

                DateTime? start = null, end = null;
                if (!string.IsNullOrEmpty(basTar) && DateTime.TryParse(basTar, out var ds)) start = ds;
                if (!string.IsNullOrEmpty(bitTar) && DateTime.TryParse(bitTar, out var de)) end = de;

                string yakaFilter = string.IsNullOrEmpty(yaka) || yaka == "Tümü" ? "" : yaka;
                string sFilter = string.IsNullOrEmpty(sirketFilter) || sirketFilter == "Tümü" ? "" : sirketFilter;

                // Admin (IK/ADMIN belgesi) tüm şirketleri görür; değilse kendi şirketine kilitlenir.
                var (ikAdmin, ownSirket) = Helpers.ScopeHelper.GetCompanyScope(User, _context, "IK");
                if (!ikAdmin) sFilter = ownSirket;

                var baseList = _context.tb_Personel.AsNoTracking().ToList();
                if (!string.IsNullOrEmpty(yakaFilter)) baseList = baseList.Where(p => p.MyBy == yakaFilter).ToList();
                if (!string.IsNullOrEmpty(sFilter)) baseList = baseList.Where(p => p.SirketKodu == sFilter).ToList();

                var sirketDict = _context.tb_Sirket.AsNoTracking().Where(s => s.SirketKodu != null).ToList().GroupBy(s => s.SirketKodu).ToDictionary(g => g.Key, g => g.First().SirketAdi);
                var departmanDict = _context.tb_Departman.AsNoTracking().Where(k => k.Kod != null).ToList().GroupBy(k => k.Kod).ToDictionary(g => g.Key, g => g.First().DepartmanAdi);
                var unvanDict = _context.tb_Unvan.AsNoTracking().Where(k => k.UnvanKodu != null).ToList().GroupBy(k => k.UnvanKodu).ToDictionary(g => g.Key, g => g.First().UnvanTanim);

                // 1. Turnover (son 12 ay)
                DateTime referenceDate = end ?? DateTime.Now;
                var turnover = new List<object>();
                for (int i = 11; i >= 0; i--)
                {
                    var date = referenceDate.AddMonths(-i);
                    int giren = baseList.Count(p => p.IseBasTar.HasValue && p.IseBasTar.Value.Month == date.Month && p.IseBasTar.Value.Year == date.Year);
                    int cikan = baseList.Count(p => p.IstenCikisTar.HasValue && p.IstenCikisTar.Value.Month == date.Month && p.IstenCikisTar.Value.Year == date.Year);
                    turnover.Add(new { Ay = date.ToString("MMM yyyy", tr), Giren = giren, Cikan = cikan });
                }

                // 2. Aktif / işe alınan / çıkan
                DateTime calc = end ?? DateTime.Now;
                var activeList = baseList.Where(p => p.IseBasTar != null && p.IseBasTar <= calc && (p.IstenCikisTar == null || p.IstenCikisTar > calc)).ToList();
                int totalActive = activeList.Count;

                var hired = baseList.Where(p => p.IseBasTar != null && p.IseBasTar <= calc);
                if (start.HasValue) hired = hired.Where(p => p.IseBasTar >= start.Value);
                int monthlyHired = hired.Count();

                var fired = baseList.Where(p => p.IstenCikisTar != null && p.IstenCikisTar <= calc);
                if (start.HasValue) fired = fired.Where(p => p.IstenCikisTar >= start.Value);
                int monthlyFired = fired.Count();

                // 3. Cinsiyet
                var gender = activeList.GroupBy(p => p.Cinsiyet).Select(g =>
                {
                    string label = string.IsNullOrEmpty(g.Key) ? "Belirtilmemiş" : g.Key.Trim().ToUpper();
                    if (label == "E") label = "Erkek"; else if (label == "K") label = "Kadın";
                    return new { Label = label, Value = g.Count() };
                }).ToList();

                // 4. Yaş grupları
                var ageGroups = new[] {
                    new { Label = "18-25", Min = 18, Max = 25 }, new { Label = "26-35", Min = 26, Max = 35 },
                    new { Label = "36-45", Min = 36, Max = 45 }, new { Label = "46-55", Min = 46, Max = 55 },
                    new { Label = "55+", Min = 56, Max = 100 }
                };
                var ages = activeList.Where(p => p.DogumTar.HasValue).Select(p => p.DogumTar.Value).ToList();
                var ageData = ageGroups.Select(gr => new { Label = gr.Label, Value = ages.Count(dob => { int age = calc.Year - dob.Year; return age >= gr.Min && age <= gr.Max; }) }).ToList();

                // 4.5 Kıdem grupları
                var tenureGroups = new[] {
                    new { Label = "0-1 Yıl", Min = 0.0, Max = 1.0 }, new { Label = "1-3 Yıl", Min = 1.0, Max = 3.0 },
                    new { Label = "3-5 Yıl", Min = 3.0, Max = 5.0 }, new { Label = "5-7 Yıl", Min = 5.0, Max = 7.0 },
                    new { Label = "7-10 Yıl", Min = 7.0, Max = 10.0 }, new { Label = "10-15 Yıl", Min = 10.0, Max = 15.0 },
                    new { Label = "15+ Yıl", Min = 15.0, Max = 100.0 }
                };
                var hireDates = activeList.Where(p => p.IseBasTar.HasValue).Select(p => p.IseBasTar.Value).ToList();
                var tenureData = tenureGroups.Select(gr => new { Label = gr.Label, Value = hireDates.Count(hd => { double years = (calc - hd).TotalDays / 365.25; return gr.Max >= 100 ? years >= gr.Min : (years >= gr.Min && years < gr.Max); }) }).ToList();

                // 5. Yaka
                var collarData = activeList.GroupBy(p => p.MyBy).Select(g =>
                {
                    string label = string.IsNullOrEmpty(g.Key) ? "Belirtilmemiş" : g.Key.Trim().ToUpper();
                    if (label == "MY") label = "Mavi Yaka"; else if (label == "BY") label = "Beyaz Yaka"; else if (label == "GY") label = "Gri Yaka";
                    return new { Label = label, Value = g.Count() };
                }).ToList();

                // 6. Şirket
                var companyData = activeList.GroupBy(p => p.SirketKodu).Select(g => new
                {
                    Label = string.IsNullOrEmpty(g.Key) ? "Belirtilmemiş" : (sirketDict.TryGetValue(g.Key, out var sn) ? sn : g.Key),
                    Value = g.Count()
                }).OrderByDescending(x => x.Value).ToList();

                // 7. Departman
                var departmentData = activeList.Select(p => new
                {
                    ResolvedName = string.IsNullOrEmpty(p.DepartmanKodu) ? "BELİRTİLMEMİŞ" : (departmanDict.TryGetValue(p.DepartmanKodu, out var dn) ? dn : p.DepartmanKodu)
                })
                .Select(x => new { ResolvedName = (x.ResolvedName ?? "").ToUpper().Replace("(GENEL)", " GENEL").Replace("( GENEL)", " GENEL").Replace("  ", " ").Trim() })
                .GroupBy(x => x.ResolvedName).Select(g => new { Label = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ToList();

                // 8. Ünvan
                var titleData = activeList.Select(p => new
                {
                    ResolvedName = string.IsNullOrEmpty(p.UnvanKodu) ? "BELİRTİLMEMİŞ" : (unvanDict.TryGetValue(p.UnvanKodu, out var un) ? un : p.UnvanKodu)
                })
                .Select(x => new { ResolvedName = (x.ResolvedName ?? "").ToUpper().Replace("(GENEL)", " GENEL").Replace("( GENEL)", " GENEL").Replace("  ", " ").Trim() })
                .GroupBy(x => x.ResolvedName).Select(g => new { Label = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ToList();

                return Ok(new
                {
                    Turnover = turnover,
                    Gender = gender,
                    Age = ageData,
                    Tenure = tenureData,
                    Collar = collarData,
                    Company = companyData,
                    Department = departmentData,
                    Title = titleData,
                    TotalActive = totalActive,
                    MonthlyHired = monthlyHired,
                    MonthlyFired = monthlyFired
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"İK dashboard verisi alinirken hata olustu: {ex.Message}" });
            }
        }
    }
}
