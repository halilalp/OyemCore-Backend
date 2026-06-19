using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebPortalSpace.BusinessLayer.Dtos;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.DataLayer.Interfaces;

namespace WebPortalSpace.BusinessLayer.Services
{
    public class BakimService : IBakimService
    {
        private readonly IYbsDbContext _context;
        private readonly ILogger<BakimService> _logger;

        public BakimService(IYbsDbContext context, ILogger<BakimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IEnumerable<MakineDto> GetMakines(string sirketKodu, string bolumKodu, string aramaText)
        {
            var query = from m in _context.tb_Makine
                        join b in _context.tb_Bolum on m.BolumKodu equals b.BolumKodu into bs
                        from b in bs.DefaultIfEmpty()
                        join s in _context.tb_Sirket on b.SirketKodu equals s.SirketKodu into ss
                        from s in ss.DefaultIfEmpty()
                        select new { m, b, s };

            if (!string.IsNullOrEmpty(sirketKodu))
            {
                query = query.Where(x => x.b != null && x.b.SirketKodu == sirketKodu);
            }

            if (!string.IsNullOrEmpty(bolumKodu))
            {
                query = query.Where(x => x.m.BolumKodu == bolumKodu);
            }

            if (!string.IsNullOrEmpty(aramaText))
            {
                string searchLower = aramaText.ToLower();
                query = query.Where(x => x.m.MakineAdi.ToLower().Contains(searchLower) || x.m.MakineKodu.ToLower().Contains(searchLower));
            }

            var list = query.OrderBy(x => x.m.MakineAdi).ToList();

            return list.Select(o => new MakineDto
            {
                MakineKodu = o.m.MakineKodu,
                MakineAdi = o.m.MakineAdi,
                BolumKodu = o.m.BolumKodu,
                BolumAdi = o.b != null ? o.b.BolumAdi : "",
                SirketKodu = o.b != null ? o.b.SirketKodu : "",
                SirketAdi = o.s != null ? o.s.SirketAdi : "",
                Durum = o.m.Durum
            }).ToList();
        }

        public bool SaveMakine(tb_Makine makine)
        {
            var existing = _context.tb_Makine
                .FirstOrDefault(m => m.MakineKodu == makine.MakineKodu);

            if (existing != null)
            {
                existing.MakineAdi = makine.MakineAdi;
                existing.BolumKodu = makine.BolumKodu;
                existing.Durum = makine.Durum;
            }
            else
            {
                _context.tb_Makine.Add(makine);
            }

            return _context.SaveChanges() > 0;
        }

        public (IEnumerable<BakimPlanDto> Data, int TotalCount) GetBakimPlanList(string sirket, string bolum, string hat, string durum, string bakimTuru, string arama, int pageIndex, int pageSize)
        {
            var query = from p in _context.tb_BakimPlan
                        join h in _context.tb_Hat on p.HatKodu equals h.HatKodu into hs
                        from h in hs.DefaultIfEmpty()
                        join b in _context.tb_Bolum on (h != null ? h.BolumKodu : null) equals b.BolumKodu into bs
                        from b in bs.DefaultIfEmpty()
                        join s in _context.tb_Sirket on (b != null ? b.SirketKodu : null) equals s.SirketKodu into ss
                        from s in ss.DefaultIfEmpty()
                        select new { p, h, b, s };

            if (!string.IsNullOrEmpty(hat))
            {
                query = query.Where(x => x.p.HatKodu == hat);
            }

            if (!string.IsNullOrEmpty(bolum) && bolum != "0")
            {
                query = query.Where(x => x.h != null && x.h.BolumKodu == bolum);
            }

            if (!string.IsNullOrEmpty(sirket))
            {
                query = query.Where(x => x.b != null && x.b.SirketKodu == sirket);
            }

            if (!string.IsNullOrEmpty(durum))
            {
                query = query.Where(x => x.p.Durum == durum);
            }

            if (!string.IsNullOrEmpty(bakimTuru))
            {
                query = query.Where(x => x.p.BakimTuru == bakimTuru);
            }

            if (!string.IsNullOrEmpty(arama))
            {
                string searchLower = arama.ToLower();
                query = query.Where(x => x.p.PlanKodu.ToLower().Contains(searchLower)
                                      || x.p.HatKodu.ToLower().Contains(searchLower)
                                      || (x.h != null && x.h.HatAdi.ToLower().Contains(searchLower)));
            }

            int totalCount = query.Count();

            var rawDataList = query
                .OrderByDescending(x => x.p.KayitTar)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var data = rawDataList.Select(o => new BakimPlanDto
            {
                ID = o.p.ID,
                PlanKodu = o.p.PlanKodu,
                HatKodu = o.p.HatKodu,
                HatAdi = o.h != null ? o.h.HatAdi : "",
                BolumAdi = o.b != null ? o.b.BolumAdi : "",
                SirketAdi = o.s != null ? o.s.SirketAdi : "",
                BakimTuru = o.p.BakimTuru,
                Durum = o.p.Durum,
                KayitSicil = o.p.KayitSicil,
                KayitPersonelAd = _context.tb_Personel.AsNoTracking().FirstOrDefault(pr => pr.SicilNo == o.p.KayitSicil)?.AdSoyad,
                IslemSicil = o.p.IslemSicil,
                IslemPersonelAd = _context.tb_Personel.AsNoTracking().FirstOrDefault(pr => pr.SicilNo == o.p.IslemSicil)?.AdSoyad,
                HedefBaslangicStr = o.p.HedefBaslangic != null ? o.p.HedefBaslangic.Value.ToString("dd.MM.yyyy") : "",
                HedefBitisStr = o.p.HedefBitis != null ? o.p.HedefBitis.Value.ToString("dd.MM.yyyy") : "",
                BaslamaTarStr = o.p.BaslamaTar != null ? o.p.BaslamaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                BitisTarStr = o.p.BitisTar != null ? o.p.BitisTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                KayitTarStr = o.p.KayitTar != null ? o.p.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
            }).ToList();

            return (data, totalCount);
        }

        public string SaveBakimPlan(string planKodu, string hatKodu, string bakimTuru, string basTar, string bitTar, string sicil)
        {
            DateTime? dtBas = null;
            DateTime? dtBit = null;

            if (DateTime.TryParse(basTar, out var parsedBas)) dtBas = parsedBas;
            if (DateTime.TryParse(bitTar, out var parsedBit)) dtBit = parsedBit;

            if (string.IsNullOrEmpty(planKodu))
            {
                var plan = new tb_BakimPlan
                {
                    HatKodu = hatKodu,
                    BakimTuru = bakimTuru,
                    HedefBaslangic = dtBas,
                    HedefBitis = dtBit,
                    Durum = "BEKLEMEDE",
                    KayitSicil = sicil,
                    KayitTar = DateTime.Now
                };

                _context.tb_BakimPlan.Add(plan);
                _context.SaveChanges(); // ID populated

                string generatedCode = $"PLN-{DateTime.Now.Year}{DateTime.Now.Month:00}-{plan.ID}";
                plan.PlanKodu = generatedCode;
                _context.SaveChanges();

                return generatedCode;
            }
            else
            {
                var plan = _context.tb_BakimPlan
                    .FirstOrDefault(p => p.PlanKodu == planKodu);

                if (plan != null)
                {
                    plan.HatKodu = hatKodu;
                    plan.BakimTuru = bakimTuru;
                    plan.HedefBaslangic = dtBas;
                    plan.HedefBitis = dtBit;
                    _context.SaveChanges();
                }

                return planKodu;
            }
        }

        public bool UpdateBakimPlanStatus(string planKodu, string durum, string note, string dosyaUrl, string sicil)
        {
            var plan = _context.tb_BakimPlan
                .FirstOrDefault(p => p.PlanKodu == planKodu);

            if (plan == null) return false;

            plan.Durum = durum;
            if (durum == "DEVAM" && plan.BaslamaTar == null)
            {
                plan.BaslamaTar = DateTime.Now;
            }
            else if (durum == "TAMAMLANDI")
            {
                plan.BitisTar = DateTime.Now;
                plan.IslemSicil = sicil;
            }

            _context.SaveChanges();

            if (!string.IsNullOrEmpty(note))
            {
                var detay = new tb_BakimPlanDetay
                {
                    PlanKodu = planKodu,
                    IslemNotu = note,
                    KayitSicil = sicil,
                    KayitTar = DateTime.Now,
                    DosyaUrl = dosyaUrl
                };
                _context.tb_BakimPlanDetay.Add(detay);
                _context.SaveChanges();
            }

            return true;
        }

        public IEnumerable<BakimPlanDetayDto> GetBakimNotlari(string planKodu)
        {
            var query = from d in _context.tb_BakimPlanDetay
                        join p in _context.tb_Personel on d.KayitSicil equals p.SicilNo into ps
                        from p in ps.DefaultIfEmpty()
                        where d.PlanKodu == planKodu
                        orderby d.KayitTar descending
                        select new BakimPlanDetayDto
                        {
                            ID = d.ID,
                            PlanKodu = d.PlanKodu,
                            Aciklama = d.IslemNotu,
                            DosyaUrl = d.DosyaUrl,
                            Personel = p != null ? p.AdSoyad : d.KayitSicil,
                            KayitSicil = d.KayitSicil,
                            TarihStr = d.KayitTar != null ? d.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                        };

            return query.ToList();
        }

        public bool DeleteBakimPlan(string planKodu)
        {
            var details = _context.tb_BakimPlanDetay
                .Where(d => d.PlanKodu == planKodu).ToList();
            _context.tb_BakimPlanDetay.RemoveRange(details);

            var plan = _context.tb_BakimPlan
                .FirstOrDefault(p => p.PlanKodu == planKodu);

            if (plan != null)
            {
                _context.tb_BakimPlan.Remove(plan);
            }

            return _context.SaveChanges() > 0;
        }

        public bool DeleteBakimGelisme(int id)
        {
            var detail = _context.tb_BakimPlanDetay
                .FirstOrDefault(d => d.ID == id);

            if (detail != null)
            {
                _context.tb_BakimPlanDetay.Remove(detail);
                return _context.SaveChanges() > 0;
            }

            return false;
        }

        public (IEnumerable<PeriyodikKontrolDto> Data, int TotalCount) GetPeriyodikKontrolList(string sirket, string bolum, string durum, string kontrolTuru, string arama, int pageIndex, int pageSize)
        {
            var query = from k in _context.tb_BakimPerKontrol
                        join b in _context.tb_Bolum on k.BolumKodu equals b.BolumKodu into bs
                        from b in bs.DefaultIfEmpty()
                        join s in _context.tb_Sirket on (b != null ? b.SirketKodu : null) equals s.SirketKodu into ss
                        from s in ss.DefaultIfEmpty()
                        select new { k, b, s };

            if (!string.IsNullOrEmpty(bolum) && bolum != "0")
            {
                query = query.Where(x => x.k.BolumKodu == bolum);
            }

            if (!string.IsNullOrEmpty(sirket))
            {
                query = query.Where(x => x.b != null && x.b.SirketKodu == sirket);
            }

            if (!string.IsNullOrEmpty(durum))
            {
                query = query.Where(x => x.k.Durum == durum);
            }

            if (!string.IsNullOrEmpty(kontrolTuru))
            {
                query = query.Where(x => x.k.KontrolTuru == kontrolTuru);
            }

            if (!string.IsNullOrEmpty(arama))
            {
                string searchLower = arama.ToLower();
                query = query.Where(x => x.k.KontrolKodu.ToLower().Contains(searchLower)
                                      || x.k.Aciklama.ToLower().Contains(searchLower)
                                      || (x.b != null && x.b.BolumAdi.ToLower().Contains(searchLower)));
            }

            int totalCount = query.Count();

            var rawDataList = query
                .OrderByDescending(x => x.k.KayitTar)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var data = rawDataList.Select(o => new PeriyodikKontrolDto
            {
                ID = o.k.ID,
                KontrolKodu = o.k.KontrolKodu,
                BolumKodu = o.k.BolumKodu,
                BolumAdi = o.b != null ? o.b.BolumAdi : "",
                SirketAdi = o.s != null ? o.s.SirketAdi : "",
                KontrolTuru = o.k.KontrolTuru,
                Durum = o.k.Durum,
                Aciklama = o.k.Aciklama,
                KayitSicil = o.k.KayitSicil,
                KayitPersonelAd = _context.tb_Personel.AsNoTracking().FirstOrDefault(pr => pr.SicilNo == o.k.KayitSicil)?.AdSoyad,
                IslemSicil = o.k.IslemSicil,
                IslemPersonelAd = _context.tb_Personel.AsNoTracking().FirstOrDefault(pr => pr.SicilNo == o.k.IslemSicil)?.AdSoyad,
                HedefBaslangicStr = o.k.HedefBaslangic != null ? o.k.HedefBaslangic.Value.ToString("dd.MM.yyyy") : "",
                HedefBitisStr = o.k.HedefBitis != null ? o.k.HedefBitis.Value.ToString("dd.MM.yyyy") : "",
                BaslamaTarStr = o.k.BaslamaTar != null ? o.k.BaslamaTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                BitisTarStr = o.k.BitisTar != null ? o.k.BitisTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                KayitTarStr = o.k.KayitTar != null ? o.k.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
            }).ToList();

            return (data, totalCount);
        }

        public string SavePeriyodikKontrol(string kontrolKodu, string bolumKodu, string kontrolTuru, string basTar, string bitTar, string aciklama, string sicil)
        {
            DateTime? dtBas = null;
            DateTime? dtBit = null;

            if (DateTime.TryParse(basTar, out var parsedBas)) dtBas = parsedBas;
            if (DateTime.TryParse(bitTar, out var parsedBit)) dtBit = parsedBit;

            if (string.IsNullOrEmpty(kontrolKodu))
            {
                var kontrol = new tb_BakimPerKontrol
                {
                    BolumKodu = bolumKodu,
                    KontrolTuru = kontrolTuru,
                    HedefBaslangic = dtBas,
                    HedefBitis = dtBit,
                    Durum = "BEKLEMEDE",
                    Aciklama = aciklama,
                    KayitSicil = sicil,
                    KayitTar = DateTime.Now
                };

                _context.tb_BakimPerKontrol.Add(kontrol);
                _context.SaveChanges();

                string generatedCode = $"KON-{DateTime.Now.Year}{DateTime.Now.Month:00}-{kontrol.ID}";
                kontrol.KontrolKodu = generatedCode;
                _context.SaveChanges();

                return generatedCode;
            }
            else
            {
                var kontrol = _context.tb_BakimPerKontrol
                    .FirstOrDefault(k => k.KontrolKodu == kontrolKodu);

                if (kontrol != null)
                {
                    kontrol.BolumKodu = bolumKodu;
                    kontrol.KontrolTuru = kontrolTuru;
                    kontrol.HedefBaslangic = dtBas;
                    kontrol.HedefBitis = dtBit;
                    kontrol.Aciklama = aciklama;
                    _context.SaveChanges();
                }

                return kontrolKodu;
            }
        }

        public bool UpdatePeriyodikStatus(string kontrolKodu, string status, string aciklama, string sicil)
        {
            var plan = _context.tb_BakimPerKontrol
                .FirstOrDefault(k => k.KontrolKodu == kontrolKodu);

            if (plan == null) return false;

            plan.Durum = status;
            if (status == "DEVAM" && plan.BaslamaTar == null)
            {
                plan.BaslamaTar = DateTime.Now;
                plan.IslemSicil = sicil;
            }
            else if (status == "TAMAMLANDI")
            {
                plan.BitisTar = DateTime.Now;
            }

            _context.SaveChanges();

            if (!string.IsNullOrEmpty(aciklama))
            {
                var detay = new tb_BakimPerKontrolDetay
                {
                    KontrolKodu = kontrolKodu,
                    IslemNotu = aciklama,
                    KayitSicil = sicil,
                    KayitTar = DateTime.Now
                };
                _context.tb_BakimPerKontrolDetay.Add(detay);
                _context.SaveChanges();
            }

            return true;
        }

        public bool DeletePeriyodikKontrol(string kontrolKodu)
        {
            var sarfiyats = _context.tb_BakimPerKontrolSarfiyat
                .Where(s => s.KontrolKodu == kontrolKodu).ToList();
            _context.tb_BakimPerKontrolSarfiyat.RemoveRange(sarfiyats);

            var detays = _context.tb_BakimPerKontrolDetay
                .Where(d => d.KontrolKodu == kontrolKodu).ToList();
            _context.tb_BakimPerKontrolDetay.RemoveRange(detays);

            var kontrol = _context.tb_BakimPerKontrol
                .FirstOrDefault(k => k.KontrolKodu == kontrolKodu);

            if (kontrol != null)
            {
                _context.tb_BakimPerKontrol.Remove(kontrol);
            }

            return _context.SaveChanges() > 0;
        }

        public IEnumerable<PeriyodikSarfiyatDto> GetPeriyodikSarfiyats(string kontrolKodu)
        {
            var query = from s in _context.tb_BakimPerKontrolSarfiyat
                        join st in _context.tb_Malzeme on s.MalzemeKodu equals st.MalzemeKodu into sts
                        from st in sts.DefaultIfEmpty()
                        join m in _context.tb_Makine on s.MakineKodu equals m.MakineKodu into ms
                        from m in ms.DefaultIfEmpty()
                        where s.KontrolKodu == kontrolKodu
                        orderby s.KayitTar descending
                        select new PeriyodikSarfiyatDto
                        {
                            ID = s.ID,
                            KontrolKodu = s.KontrolKodu,
                            MalzemeKodu = s.MalzemeKodu,
                            MalzemeAdi = st != null ? st.MalzemeAdi : "",
                            Birim = st != null ? st.BirimKodu : "",
                            Miktar = s.Miktar,
                            MakineKodu = s.MakineKodu,
                            MakineAdi = m != null ? m.MakineAdi : "",
                            KayitSicil = s.KayitSicil,
                            KayitTar = s.KayitTar
                        };

            return query.ToList();
        }

        public bool SavePeriyodikSarfiyat(string kontrolKodu, string malzemeKodu, decimal miktar, string makineKodu, string sicil)
        {
            var sarfiyat = new tb_BakimPerKontrolSarfiyat
            {
                KontrolKodu = kontrolKodu,
                MalzemeKodu = malzemeKodu,
                Miktar = miktar,
                MakineKodu = string.IsNullOrEmpty(makineKodu) ? null : makineKodu,
                KayitSicil = sicil,
                KayitTar = DateTime.Now
            };

            _context.tb_BakimPerKontrolSarfiyat.Add(sarfiyat);
            return _context.SaveChanges() > 0;
        }

        public bool DeletePeriyodikSarfiyat(int id)
        {
            var sarf = _context.tb_BakimPerKontrolSarfiyat
                .FirstOrDefault(s => s.ID == id);

            if (sarf != null)
            {
                _context.tb_BakimPerKontrolSarfiyat.Remove(sarf);
                return _context.SaveChanges() > 0;
            }

            return false;
        }

        public IEnumerable<BakimPlanDetayDto> GetPeriyodikGelismeler(string kontrolKodu)
        {
            var query = from d in _context.tb_BakimPerKontrolDetay
                        join p in _context.tb_Personel on d.KayitSicil equals p.SicilNo into ps
                        from p in ps.DefaultIfEmpty()
                        where d.KontrolKodu == kontrolKodu
                        orderby d.KayitTar descending
                        select new BakimPlanDetayDto
                        {
                            ID = d.ID,
                            PlanKodu = d.KontrolKodu,
                            Aciklama = d.IslemNotu,
                            DosyaUrl = d.DosyaUrl,
                            Personel = p != null ? p.AdSoyad : d.KayitSicil,
                            KayitSicil = d.KayitSicil,
                            TarihStr = d.KayitTar != null ? d.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
                        };

            return query.ToList();
        }

        public bool SavePeriyodikGelisme(string kontrolKodu, string aciklama, string dosyaUrl, string sicil)
        {
            var gelisme = new tb_BakimPerKontrolDetay
            {
                KontrolKodu = kontrolKodu,
                IslemNotu = aciklama,
                KayitSicil = sicil,
                KayitTar = DateTime.Now,
                DosyaUrl = dosyaUrl
            };

            _context.tb_BakimPerKontrolDetay.Add(gelisme);
            return _context.SaveChanges() > 0;
        }

        public bool DeletePeriyodikGelisme(int id)
        {
            var gelisme = _context.tb_BakimPerKontrolDetay
                .FirstOrDefault(g => g.ID == id);

            if (gelisme != null)
            {
                _context.tb_BakimPerKontrolDetay.Remove(gelisme);
                return _context.SaveChanges() > 0;
            }

            return false;
        }

        public (IEnumerable<MalzemeDto> Results, bool HasMore) SearchMalzemes(string term, int page, int pageSize, bool sarfOnly)
        {
            var query = _context.tb_Malzeme
                .AsNoTracking()
                .Where(m => m.Aktif == true);

            if (!string.IsNullOrEmpty(term))
            {
                string searchLower = term.ToLower();
                query = query.Where(m => m.MalzemeAdi.ToLower().Contains(searchLower)
                                      || m.MalzemeKodu.ToLower().Contains(searchLower)
                                      || m.MalzemeGrupKodu.ToLower().Contains(searchLower));
            }

            if (sarfOnly)
            {
                query = query.Where(m => m.SatinAlinabilir == true && m.MalzemeTipKodu == "SARF");
            }

            int totalCount = query.Count();

            var list = query
                .OrderBy(m => m.MalzemeAdi)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MalzemeDto
                {
                    MalzemeKodu = m.MalzemeKodu,
                    MalzemeAdi = m.MalzemeAdi,
                    Birim = m.BirimKodu
                })
                .ToList();

            bool hasMore = (page * pageSize) < totalCount;
            return (list, hasMore);
        }

        public BakimDropdownsDto GetBakimDropdowns(string sicilNo, string adminBelgeTur)
        {
            bool isBakimAdmin = adminBelgeTur != null && (adminBelgeTur.Contains("BAKIMADMIN") || adminBelgeTur.Contains("ADMIN"));

            var sirkets = _context.tb_Sirket.AsNoTracking().OrderBy(s => s.SirketKodu).ToList();
            var bolums = _context.tb_Bolum.AsNoTracking().OrderBy(b => b.BolumAdi).ToList();
            var hats = _context.tb_Hat.AsNoTracking().Where(h => h.Durum == true).OrderBy(h => h.HatAdi).ToList();
            var makines = _context.tb_Makine.AsNoTracking().Where(m => m.Durum == true).OrderBy(m => m.MakineAdi).ToList();

            return new BakimDropdownsDto
            {
                IsAdmin = isBakimAdmin,
                Sirkets = sirkets,
                Bolums = bolums,
                Hats = hats,
                Makines = makines
            };
        }

        public IEnumerable<PersonelPerformansRaporuDto> GetPersonelPerformansRaporu(string yil, string ay, string sirket)
        {
            DateTime t1, t2;
            int yilNum = 2026;
            int.TryParse(yil, out yilNum);

            if (ay == "Tümü" || string.IsNullOrEmpty(ay))
            {
                t1 = new DateTime(yilNum, 1, 1);
                t2 = new DateTime(yilNum, 12, 31, 23, 59, 59);
            }
            else
            {
                int ayNum = 1;
                int.TryParse(ay, out ayNum);
                t1 = new DateTime(yilNum, ayNum, 1);
                t2 = new DateTime(yilNum, ayNum, DateTime.DaysInMonth(yilNum, ayNum), 23, 59, 59);
            }

            var query = from t in _context.tb_Talep
                        join tb in _context.tb_TalepBakim on t.TalepKodu equals tb.TalepKodu into tbs
                        from tb in tbs.DefaultIfEmpty()
                        join p in _context.tb_Personel on t.SorumluSicil equals p.SicilNo into ps
                        from p in ps.DefaultIfEmpty()
                        where t.TalepTurKodu == "BAKIM" && t.KayitTar >= t1 && t.KayitTar <= t2
                        select new { t, tb, SorumluAd = p != null ? p.AdSoyad : t.SorumluSicil };

            if (!string.IsNullOrEmpty(sirket))
            {
                query = query.Where(x => x.tb != null && x.tb.SirketKodu == sirket);
            }

            var kaynakListe = query.ToList();

            var acikTalepKodlari = kaynakListe.Where(x => x.t.Durum == false).Select(x => x.t.TalepKodu).ToList();
            var onayBekleyenDict = _context.tb_TalepAmir
                .Where(x => acikTalepKodlari.Contains(x.TalepKodu) && x.Durum == null && x.IslemTur == "ONAY")
                .Select(x => x.TalepKodu).Distinct().ToList();

            var personeller = kaynakListe
                .Where(x => !string.IsNullOrEmpty(x.t.SorumluSicil))
                .GroupBy(x => new { x.t.SorumluSicil, Name = x.SorumluAd })
                .Select(g => new PersonelPerformansRaporuDto
                {
                    Sicil = g.Key.SorumluSicil,
                    Name = g.Key.Name ?? "Bilinmiyor",
                    Title = "Bakım Personeli",
                    Dept = "-",
                    OpenTasks = g.Count(x => x.t.Durum == false),
                    Tamamlanan = g.Count(x => x.t.Durum == true),
                    Bekleyen = g.Count(x => x.t.Durum == false),
                    OnayBekleyen = onayBekleyenDict.Count(o => g.Any(t => t.t.TalepKodu == o)),
                    AvgResolve = g.Where(x => x.t.Durum == true && x.t.MttrTamamSure.HasValue && x.t.MttrTamamSure > 0).Any()
                         ? (g.Where(x => x.t.Durum == true && x.t.MttrTamamSure.HasValue && x.t.MttrTamamSure > 0).Average(x => (double)x.t.MttrTamamSure.Value) / 60.0).ToString("0.0") + " Saat"
                         : "0 Saat",
                    OrtalamaHizDouble = g.Where(x => x.t.Durum == true && x.t.MttrTamamSure.HasValue && x.t.MttrTamamSure > 0).Any()
                         ? (g.Where(x => x.t.Durum == true && x.t.MttrTamamSure.HasValue && x.t.MttrTamamSure > 0).Average(x => (double)x.t.MttrTamamSure.Value) / 60.0)
                         : 0,
                    Cost = "-",
                    Rating = g.Where(x => x.t.TalepPuan.HasValue && x.t.TalepPuan > 0).Any()
                        ? g.Where(x => x.t.TalepPuan.HasValue && x.t.TalepPuan > 0).Average(x => (double)x.t.TalepPuan.Value)
                        : 0
                }).ToList();

            return personeller;
        }

        public IEnumerable<BakimDashboardStatsDto> GetBakimDashboardStats(string yillar, string sirket)
        {
            var yearList = yillar.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(int.Parse).ToList();
            if (yearList.Count == 0) return Enumerable.Empty<BakimDashboardStatsDto>();

            var minYear = yearList.Min();
            var maxYear = yearList.Max();

            var query = from t in _context.tb_Talep
                        join tb in _context.tb_TalepBakim on t.TalepKodu equals tb.TalepKodu into bakimGroup
                        from bakim in bakimGroup.DefaultIfEmpty()
                        join k in _context.tb_TalepKategori on t.KategoriID equals k.TalepKategoriID into katGroup
                        from kat in katGroup.DefaultIfEmpty()
                        where t.TalepTurKodu == "BAKIM"
                        && t.KayitTar.HasValue
                        && t.KayitTar.Value.Year >= minYear && t.KayitTar.Value.Year <= maxYear
                        select new
                        {
                            t.KayitTar,
                            t.Durum,
                            t.MttrTamamSure,
                            UretimDurusu = bakim != null ? bakim.UretimDurusu : "H",
                            KategoriTanim = kat != null ? kat.Tanim : "",
                            SirketKodu = bakim != null ? bakim.SirketKodu : ""
                        };

            if (!string.IsNullOrEmpty(sirket))
            {
                query = query.Where(o => o.SirketKodu == sirket);
            }

            var allData = query.ToList();

            var results = new List<BakimDashboardStatsDto>();

            foreach (var year in yearList)
            {
                var yearMonthlyData = new List<BakimDashboardMonthStatsDto>();
                for (int month = 1; month <= 12; month++)
                {
                    var monthly = allData.Where(o => o.KayitTar.Value.Year == year && o.KayitTar.Value.Month == month).ToList();

                    int totalCount = monthly.Count;
                    int completedCount = monthly.Count(o => o.Durum == true);
                    int electricCount = monthly.Count(o => o.KategoriTanim.ToUpper().Contains("ELEKTRİK"));
                    int mechanicCount = monthly.Count(o => o.KategoriTanim.ToUpper().Contains("MEKANİK"));

                    double totalMttrMinutes = monthly.Where(o => o.Durum == true && o.MttrTamamSure.HasValue).Sum(o => (double)o.MttrTamamSure.Value);
                    double downtimeMinutes = monthly.Where(o => o.UretimDurusu != "H" && o.MttrTamamSure.HasValue).Sum(o => (double)o.MttrTamamSure.Value);

                    double daysInMonth = DateTime.DaysInMonth(year, month);
                    double totalHoursInMonth = daysInMonth * 24.0;

                    yearMonthlyData.Add(new BakimDashboardMonthStatsDto
                    {
                        Month = month,
                        MonthName = new DateTime(year, month, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")).ToUpper(),
                        TotalCount = totalCount,
                        ElectricCount = electricCount,
                        MechanicCount = mechanicCount,
                        CompletedCount = completedCount,
                        RemainingCount = totalCount - completedCount,
                        
                        // Zaman Metrikleri
                        DowntimeHours = downtimeMinutes / 60.0,
                        MttrTotalHours = totalMttrMinutes / 60.0,
                        MttrAvgHours = completedCount > 0 ? (totalMttrMinutes / 60.0) / (double)completedCount : 0,
                        MtbfHours = totalCount > 0 ? totalHoursInMonth / (double)totalCount : 0,
                    });
                }
                results.Add(new BakimDashboardStatsDto { Year = year, Data = yearMonthlyData });
            }

            return results;
        }
    }
}
