using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IYbsDbContext _context;
        private static readonly HttpClient _httpClient = new HttpClient();

        public DashboardService(IYbsDbContext context)
        {
            _context = context;

            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            }
        }

        public async Task<object> GetWeatherAsync(string city = "IZMIR")
        {
            try
            {
                var response = await _httpClient.GetStringAsync("https://www.mgm.gov.tr/FTPDATA/analiz/sonSOA.xml");
                if (string.IsNullOrEmpty(response))
                {
                    return null;
                }

                var xDoc = XDocument.Parse(response);
                var cityNode = xDoc.Descendants("sehirler")
                    .FirstOrDefault(x => string.Equals((string)x.Element("Merkez"), city, StringComparison.OrdinalIgnoreCase));

                if (cityNode == null) return null;

                var condition = (string)cityNode.Element("Durum") ?? "";
                string icon = "?";
                string lowerCond = condition.ToLower();
                if (lowerCond.Contains("g?nes")) icon = "??";
                else if (lowerCond.Contains("yagmur")) icon = "g????";
                else if (lowerCond.Contains("bulut")) icon = "??";

                return new
                {
                    bolge = (string)cityNode.Element("Bolge"),
                    merkez = (string)cityNode.Element("Merkez"),
                    durum = condition,
                    il = (string)cityNode.Element("ili"),
                    mak = (string)cityNode.Element("Mak"),
                    icon = icon,
                    tarih = DateTime.Now.ToString("dd.MM.yyyy")
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<object> GetCurrenciesAsync()
        {
            try
            {
                var dt = DateTime.Now;
                if (dt.DayOfWeek == DayOfWeek.Saturday)
                    dt = dt.AddDays(-1);
                else if (dt.DayOfWeek == DayOfWeek.Sunday)
                    dt = dt.AddDays(-2);

                string url = "https://www.tcmb.gov.tr/kurlar/today.xml";
                if (dt.Date != DateTime.Now.Date)
                {
                    url = $"https://www.tcmb.gov.tr/kurlar/{dt.Year}{dt.Month:00}/{dt.Day:00}{dt.Month:00}{dt.Year}.xml";
                }

                var xmlData = await _httpClient.GetStringAsync(url);
                if (string.IsNullOrEmpty(xmlData)) return null;

                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.LoadXml(xmlData);

                var dolarNode = xmlDoc.SelectSingleNode("Tarih_Date/Currency[@Kod='USD']");
                var euroNode = xmlDoc.SelectSingleNode("Tarih_Date/Currency[@Kod='EUR']");
                var gbpNode = xmlDoc.SelectSingleNode("Tarih_Date/Currency[@Kod='GBP']");

                Func<System.Xml.XmlNode, string, string> getVal = (node, type) =>
                {
                    if (node == null) return "--";
                    var field = type == "ALIS" ? "ForexBuying" : "ForexSelling";
                    var target = node.SelectSingleNode(field);
                    return target != null && !string.IsNullOrEmpty(target.InnerText)
                        ? target.InnerText.Trim().Replace(",", ".")
                        : "--";
                };

                return new
                {
                    dolarA = getVal(dolarNode, "ALIS"),
                    dolarS = getVal(dolarNode, "SATIS"),
                    euroA = getVal(euroNode, "ALIS"),
                    euroS = getVal(euroNode, "SATIS"),
                    gbpA = getVal(gbpNode, "ALIS"),
                    gbpS = getVal(gbpNode, "SATIS")
                };
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<object> GetBirthdays()
        {
            var today = DateTime.Today;
            return _context.tb_Personel
                .AsNoTracking()
                .Where(p => p.Durum == true && p.DogumTar != null && p.DogumTar.Value.Month == today.Month && p.DogumTar.Value.Day == today.Day)
                .OrderBy(p => p.AdSoyad)
                .ToList()
                .Select(p => new
                {
                    AdSoyad = p.AdSoyad,
                    SicilNo = p.SicilNo,
                    Departman = p.Departman ?? "",
                    Unvan = CleanUnvan(p.Unvan)
                })
                .ToList();
        }

        public IEnumerable<object> GetTrainings()
        {
            var query = from e in _context.tb_Egitim
                        join ek in _context.tb_EgitimKategori on e.KategoriID equals ek.KategoriID into eks
                        from ek in eks.DefaultIfEmpty()
                        join p in _context.tb_Personel on e.KayitEposta equals p.Eposta into ps
                        from p in ps.DefaultIfEmpty()
                        where p == null || p.Durum == true
                        select new { e, ek, p };

            var list = query.OrderByDescending(x => x.e.KayitTar).ToList();

            return list.Select(x => new
            {
                ID = x.e.EgitimID,
                Konu = x.e.Konu ?? "",
                DosyaUrl = x.e.DosyaUrl ?? "",
                Tarih = x.e.KayitTar != null ? x.e.KayitTar.Value.ToString("dd/MM/yyyy") : "",
                Kategori = x.ek != null ? x.ek.Tanim : "Genel",
                AdSoyad = x.p != null ? x.p.AdSoyad : "[Bulunamadi]"
            }).ToList();
        }

        public IEnumerable<object> GetContacts()
        {
            return _context.tb_Personel
                .AsNoTracking()
                .Where(p => p.Durum == true && ((p.DahiliNo != null && p.DahiliNo.Length > 1) || p.Eposta != null))
                .OrderBy(p => p.AdSoyad)
                .ToList()
                .Select(p => new
                {
                    p.SicilNo,
                    p.AdSoyad,
                    Eposta = p.Eposta ?? "",
                    DahiliNo = p.DahiliNo ?? "",
                    Departman = p.Departman ?? "",
                    Telefon = p.Telefon ?? "",
                    Unvan = CleanUnvan(p.Unvan)
                })
                .ToList();
        }

        public IEnumerable<object> GetNews()
        {
            return _context.tb_Haber
                .AsNoTracking()
                .OrderByDescending(h => h.HaberID)
                .Take(5)
                .ToList()
                .Select(h => new
                {
                    ID = h.HaberID,
                    Konu = h.Konu ?? "",
                    ProfilUrl = h.ProfilUrl ?? "duyuru.jpg",
                    h.KayitEposta,
                    Tarih = h.KayitTar != null ? h.KayitTar.Value.ToString("dd/MM/yyyy") : "",
                    Aciklama = h.Aciklama ?? ""
                })
                .ToList();
        }

        public object GetNewsDetail(int id)
        {
            var h = _context.tb_Haber
                .AsNoTracking()
                .FirstOrDefault(item => item.HaberID == id);

            if (h == null) return null;

            return new
            {
                ID = h.HaberID,
                Konu = h.Konu ?? "",
                ProfilUrl = h.ProfilUrl ?? "duyuru.jpg",
                h.KayitEposta,
                Tarih = h.KayitTar != null ? h.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                Aciklama = h.Aciklama ?? ""
            };
        }

        public IEnumerable<object> GetMenu(int userId)
        {
            // Standart EF Core (LINQ) yapısı, TSQL (Stored Procedure) yerine
            var query = from k in _context.tb_Kullanici
                        join y in _context.tb_KullaniciYetki on k.KullaniciID equals y.KullaniciID
                        join s in _context.tb_Sayfa on y.SayfaID equals s.SayfaID
                        join p in _context.tb_Proje on s.ProjeID equals p.ProjeID
                        where y.KullaniciID == userId
                           && p.Durum == true
                           && s.MenudeGoster == true
                           && s.Durum == true
                        orderby p.SiraNo, s.SiraNo
                        select new
                        {
                            kullaniciID = y.KullaniciID,
                            sayfaAdi = s.SayfaAdi ?? "",
                            sayfaUrl = s.SayfaUrl ?? "",
                            projeID = s.ProjeID,
                            projeAdi = p.ProjeAdi ?? "",
                            ikon = p.Ikon ?? "ki-outline ki-abstract-26",
                            mobilGoster = s.MobilGoster ?? false,
                            mobilUrl = s.MobilUrl,
                            mobilIcon = s.MobilIcon
                        };

            return query.ToList();
        }

        public object DbDebug()
        {
            // We can return the registered model details directly or execute simple schema query details
            var projects = _context.tb_Proje.AsNoTracking().OrderBy(p => p.SiraNo).ToList();
            var pages = _context.tb_Sayfa.AsNoTracking().OrderBy(p => p.ProjeID).ThenBy(p => p.SiraNo).ToList();
            var categories = _context.tb_TicketKategori.AsNoTracking().ToList();
            var hierarchy = _context.tb_Hiyerarsi.AsNoTracking().ToList();
            var talepKategori = _context.tb_TalepKategori.AsNoTracking().ToList();

            return new {
                projects,
                pages,
                categories,
                hierarchy,
                talepKategori
            };
        }

        private string CleanUnvan(string unvan)
        {
            if (string.IsNullOrEmpty(unvan)) return "";
            return unvan.Replace("-A", "")
                        .Replace("-B", "")
                        .Replace("-C", "")
                        .Replace("-D", "")
                        .Replace("-E", "")
                        .Replace("-F", "")
                        .Replace("/A", "")
                        .Replace("/B", "")
                        .Replace("/C", "")
                        .Replace("/D", "");
        }
    }
}
