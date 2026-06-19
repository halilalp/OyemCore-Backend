using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebPortalSpace.BusinessLayer.Common;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.DataLayer.Interfaces;

namespace WebPortalSpace.BusinessLayer.Services
{
    public class AdminService : IAdminService
    {
        private readonly IYbsDbContext _context;

        public AdminService(IYbsDbContext context)
        {
            _context = context;
        }

        public IEnumerable<object> GetUsers(string search, string status)
        {
            var query = _context.tb_Kullanici.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(u => u.KullaniciAdi.ToLower().Contains(searchLower)
                                      || u.AdSoyad.ToLower().Contains(searchLower)
                                      || u.SicilNo.Contains(searchLower));
            }

            if (!string.IsNullOrEmpty(status))
            {
                bool isTrue = status == "1" || status.Equals("true", StringComparison.OrdinalIgnoreCase);
                query = query.Where(u => u.Durum == isTrue);
            }

            var list = query.OrderBy(u => u.AdSoyad).ToList();

            return list.Select(u => new
            {
                id = u.KullaniciID,
                adSoyad = u.AdSoyad ?? "",
                unvan = u.Unvan ?? "",
                eposta = u.Eposta ?? "",
                sicilNo = u.SicilNo ?? "",
                kullaniciAdi = u.KullaniciAdi ?? "",
                durum = u.Durum ?? false,
                adminBelgeTur = u.AdminBelgeTur ?? "",
                yonetici = u.Yonetici ?? false,
                zimmetSorumlusu = u.ZimmetSorumlusu ?? false,
                tel1 = u.Tel1 ?? ""
            }).ToList();
        }

        public object GetUserDetail(int id)
        {
            var u = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(usr => usr.KullaniciID == id);

            if (u == null) return null;

            return new
            {
                id = u.KullaniciID,
                adSoyad = u.AdSoyad ?? "",
                unvan = u.Unvan ?? "",
                eposta = u.Eposta ?? "",
                sicilNo = u.SicilNo ?? "",
                kullaniciAdi = u.KullaniciAdi ?? "",
                durum = u.Durum ?? false,
                adminBelgeTur = u.AdminBelgeTur ?? "",
                yonetici = u.Yonetici ?? false,
                zimmetSorumlusu = u.ZimmetSorumlusu ?? false,
                tel1 = u.Tel1 ?? ""
            };
        }

        public (bool Success, string Message, int? Id) SaveUser(int currentUserId, tb_Kullanici model)
        {
            var existingUser = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.SicilNo == model.SicilNo);

            bool isEdit = model.KullaniciID > 0 || existingUser != null;
            bool isAD = (model.Eposta ?? "").ToLower().EndsWith("@isiktarim.com");

            if (isEdit)
            {
                int userId = model.KullaniciID > 0 ? model.KullaniciID : existingUser.KullaniciID;
                var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == userId);
                if (user == null) return (false, "Güncellenecek kullanıcı bulunamadı.", null);

                user.AdSoyad = model.AdSoyad;
                user.Unvan = model.Unvan;
                user.Eposta = model.Eposta;
                user.Tel1 = model.Tel1;
                user.Durum = model.Durum;
                user.Yonetici = model.Yonetici;
                user.ZimmetSorumlusu = model.ZimmetSorumlusu;
                user.AdminBelgeTur = model.AdminBelgeTur;

                if (!isAD && !string.IsNullOrEmpty(model.Sifre))
                {
                    user.Sifre = SecurityHelper.EncryptPassword(model.Sifre);
                }

                _context.SaveChanges();
                return (true, "Kullanıcı başarıyla güncellendi.", userId);
            }
            else
            {
                if (_context.tb_Kullanici.Any(u => u.SicilNo == model.SicilNo))
                {
                    return (false, "Bu sicil no sistemde zaten mevcut.", null);
                }

                if (!isAD)
                {
                    if (_context.tb_Kullanici.Any(u => u.KullaniciAdi == model.KullaniciAdi))
                    {
                        return (false, "Bu kullanıcı adı sistemde zaten mevcut.", null);
                    }
                    model.Sifre = SecurityHelper.EncryptPassword(model.Sifre ?? "Isik123!");
                }
                else
                {
                    model.KullaniciAdi = null;
                    model.Sifre = null;
                }

                model.KayitTar = DateTime.Now;
                model.YillikIzin = 0;
                model.DefaultProje = 20;

                var per = _context.tb_Personel
                    .AsNoTracking()
                    .FirstOrDefault(p => p.SicilNo == model.SicilNo);

                if (per != null)
                {
                    model.Cinsiyet = !string.IsNullOrEmpty(per.Cinsiyet) ? per.Cinsiyet[0] : 'E';
                    model.DepartmanKod = per.DepartmanKodu;
                }
                else
                {
                    model.Cinsiyet = 'E';
                }

                _context.tb_Kullanici.Add(model);
                _context.SaveChanges(); // ID populated

                // Default permissions
                var sayfaIds = _context.tb_Sayfa
                    .AsNoTracking()
                    .Where(s => (s.ProjeID == 6 && (s.SayfaID == 24 || s.SayfaID == 25 || s.SayfaID == 26))
                             || (s.ProjeID == 20 && (s.SayfaID == 1082 || s.SayfaID == 1083))
                             || (s.ProjeID == 22 && (s.SayfaID == 1087 || s.SayfaID == 1088))
                             || (s.ProjeID == 21)
                             || (s.ProjeID == 23 && (s.SayfaID == 1091 || s.SayfaID == 1092))
                             || (s.SayfaID == 30))
                    .Select(s => s.SayfaID)
                    .ToList();

                foreach (var sayfaId in sayfaIds)
                {
                    _context.tb_KullaniciYetki.Add(new tb_KullaniciYetki
                    {
                        KullaniciID = model.KullaniciID,
                        SayfaID = sayfaId,
                        KayitTar = DateTime.Now
                    });
                }
                _context.SaveChanges();

                return (true, "Kullanıcı başarıyla oluşturuldu.", model.KullaniciID);
            }
        }

        public bool DeleteUser(int id)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == id);
            if (user != null)
            {
                var perms = _context.tb_KullaniciYetki.Where(p => p.KullaniciID == id).ToList();
                _context.tb_KullaniciYetki.RemoveRange(perms);

                _context.tb_Kullanici.Remove(user);
                return _context.SaveChanges() > 0;
            }
            return false;
        }

        public bool DeactivateUser(int id)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == id);
            if (user != null)
            {
                user.Durum = false;
                return _context.SaveChanges() > 0;
            }
            return false;
        }

        public IEnumerable<object> GetPersonnel()
        {
            var query = from p in _context.tb_Personel
                        join d in _context.tb_Departman on p.DepartmanKodu equals d.Kod into ds
                        from d in ds.DefaultIfEmpty()
                        where p.Durum == true
                        orderby p.AdSoyad
                        select new
                        {
                            sicilNo = p.SicilNo,
                            adSoyad = p.AdSoyad,
                            eposta = p.Eposta ?? "",
                            telefon = p.Telefon ?? "",
                            unvan = p.Unvan ?? "",
                            departman = d != null ? d.DepartmanAdi : ""
                        };

            return query.ToList();
        }

        public IEnumerable<object> GetProjects()
        {
            return _context.tb_Proje
                .AsNoTracking()
                .OrderBy(p => p.SiraNo)
                .Select(p => new
                {
                    projeID = p.ProjeID,
                    projeAdi = p.ProjeAdi,
                    ikon = p.Ikon ?? "ki-outline ki-abstract-26",
                    siraNo = (int)(p.SiraNo ?? 0),
                    durum = p.Durum ?? false,
                    anaSayfa = p.AnaSayfa ?? ""
                })
                .ToList();
        }

        public (bool Success, string Message) SaveProject(tb_Proje model)
        {
            if (model.ProjeID > 0)
            {
                if (_context.tb_Proje.Any(p => p.SiraNo == model.SiraNo && p.ProjeID != model.ProjeID))
                {
                    return (false, "Bu sıra numarası başka bir proje tarafından kullanılmaktadır.");
                }

                var project = _context.tb_Proje.FirstOrDefault(p => p.ProjeID == model.ProjeID);
                if (project != null)
                {
                    project.ProjeAdi = model.ProjeAdi;
                    project.Ikon = model.Ikon;
                    project.Durum = model.Durum;
                    project.SiraNo = model.SiraNo;
                    _context.SaveChanges();
                }
            }
            else
            {
                int nextSira = (_context.tb_Proje.Max(p => (int?)p.SiraNo) ?? 0) + 1;
                model.SiraNo = (short)nextSira;
                model.Durum = true;

                _context.tb_Proje.Add(model);
                _context.SaveChanges();
            }

            return (true, "Proje başarıyla kaydedildi.");
        }

        public (bool Success, string Message) DeleteProject(int id)
        {
            if (_context.tb_Sayfa.Any(s => s.ProjeID == id))
            {
                return (false, "Bu projeye bağlı sayfalar olduğu için silinemez. Önce sayfaları silmelisiniz.");
            }

            var project = _context.tb_Proje.FirstOrDefault(p => p.ProjeID == id);
            if (project != null)
            {
                _context.tb_Proje.Remove(project);
                _context.SaveChanges();
                return (true, "Proje silindi.");
            }

            return (false, "Proje bulunamadı.");
        }

        public bool SortProjects(List<int> sortedIds)
        {
            for (int i = 0; i < sortedIds.Count; i++)
            {
                var project = _context.tb_Proje.FirstOrDefault(p => p.ProjeID == sortedIds[i]);
                if (project != null)
                {
                    project.SiraNo = (short)(i + 1);
                }
            }
            return _context.SaveChanges() > 0;
        }

        public IEnumerable<object> GetPages(int projectId)
        {
            var query = _context.tb_Sayfa.AsNoTracking().AsQueryable();
            if (projectId > 0)
            {
                query = query.Where(s => s.ProjeID == projectId);
            }

            return query
                .OrderBy(s => s.SiraNo)
                .Select(s => new
                {
                    sayfaID = s.SayfaID,
                    sayfaAdi = s.SayfaAdi,
                    projeID = s.ProjeID,
                    sayfaUrl = s.SayfaUrl ?? "",
                    siraNo = (int)(s.SiraNo ?? 0),
                    bilgiEkrani = s.BilgiEkrani ?? "",
                    menudeGoster = s.MenudeGoster ?? false,
                    durum = s.Durum ?? false,
                    etiket = s.Etiket ?? ""
                })
                .ToList();
        }

        public (bool Success, string Message) SavePage(tb_Sayfa model)
        {
            if (model.SayfaID > 0)
            {
                if (_context.tb_Sayfa.Any(s => s.SiraNo == model.SiraNo && s.ProjeID == model.ProjeID && s.SayfaID != model.SayfaID))
                {
                    return (false, "Bu sıra numarası bu proje altındaki başka bir sayfa tarafından kullanılmaktadır.");
                }

                var page = _context.tb_Sayfa.FirstOrDefault(s => s.SayfaID == model.SayfaID);
                if (page != null)
                {
                    page.ProjeID = model.ProjeID;
                    page.SayfaAdi = model.SayfaAdi;
                    page.SayfaUrl = model.SayfaUrl;
                    page.SiraNo = model.SiraNo;
                    page.BilgiEkrani = model.BilgiEkrani;
                    page.MenudeGoster = model.MenudeGoster;
                    page.Durum = model.Durum;
                    page.Etiket = model.Etiket;
                    _context.SaveChanges();
                }
            }
            else
            {
                int nextSira = (_context.tb_Sayfa.Where(s => s.ProjeID == model.ProjeID).Max(s => (int?)s.SiraNo) ?? 0) + 1;
                model.SiraNo = (short)nextSira;
                model.Durum = true;

                _context.tb_Sayfa.Add(model);
                _context.SaveChanges();
            }

            return (true, "Sayfa kaydedildi.");
        }

        public (bool Success, string Message) DeletePage(int id)
        {
            if (_context.tb_KullaniciYetki.Any(k => k.SayfaID == id))
            {
                return (false, "Bu sayfaya ait kullanıcı yetkileri bulunmaktadır. Önce ilgili yetkileri silmelisiniz.");
            }

            var page = _context.tb_Sayfa.FirstOrDefault(s => s.SayfaID == id);
            if (page != null)
            {
                _context.tb_Sayfa.Remove(page);
                _context.SaveChanges();
                return (true, "Sayfa silindi.");
            }

            return (false, "Sayfa bulunamadı.");
        }

        public bool SortPages(List<int> sortedIds)
        {
            for (int i = 0; i < sortedIds.Count; i++)
            {
                var page = _context.tb_Sayfa.FirstOrDefault(s => s.SayfaID == sortedIds[i]);
                if (page != null)
                {
                    page.SiraNo = (short)(i + 1);
                }
            }
            return _context.SaveChanges() > 0;
        }

        public List<int> GetPermissions(int userId)
        {
            return _context.tb_KullaniciYetki
                .AsNoTracking()
                .Where(p => p.KullaniciID == userId)
                .Select(p => p.SayfaID)
                .ToList();
        }

        public bool SavePermissions(int currentUserId, int userId, List<int> sayfaIds)
        {
            var perms = _context.tb_KullaniciYetki
                .Where(p => p.KullaniciID == userId)
                .ToList();
            _context.tb_KullaniciYetki.RemoveRange(perms);

            if (sayfaIds != null && sayfaIds.Count > 0)
            {
                foreach (var sayfaId in sayfaIds)
                {
                    _context.tb_KullaniciYetki.Add(new tb_KullaniciYetki
                    {
                        KullaniciID = userId,
                        SayfaID = sayfaId,
                        KayitTar = DateTime.Now
                    });
                }
            }

            _context.SaveChanges();

            // Save log
            var currentUser = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == currentUserId);

            if (currentUser != null)
            {
                var log = new tb_Log
                {
                    Eposta = currentUser.Eposta,
                    SicilNo = currentUser.SicilNo,
                    Konu = "LOG",
                    Aciklama = $"Kullanıcı Yetkileri Güncellendi. (KullaniciID:{userId})",
                    KayitTar = DateTime.Now
                };
                _context.tb_Log.Add(log);
                _context.SaveChanges();
            }

            return true;
        }

        public object GetAdminDashboardStats()
        {
            int userCount = _context.tb_Kullanici.Count();
            int activeUserCount = _context.tb_Kullanici.Count(u => u.Durum == true);
            int logCount = _context.tb_Log.Count();
            int smsCount = _context.tb_Sms.Count();
            int pageCount = _context.tb_Sayfa.Count();
            int projectCount = _context.tb_Proje.Count();

            return new
            {
                userCount,
                activeUserCount,
                logCount,
                smsCount,
                pageCount,
                projectCount
            };
        }

        public IEnumerable<object> GetLogs(string search)
        {
            var query = _context.tb_Log.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(l => l.Eposta.ToLower().Contains(searchLower)
                                      || l.Konu.ToLower().Contains(searchLower)
                                      || l.Aciklama.ToLower().Contains(searchLower)
                                      || l.SicilNo.Contains(searchLower));
            }

            return query
                .OrderByDescending(l => l.KayitTar)
                .Take(200)
                .Select(l => new
                {
                    eposta = l.Eposta ?? "",
                    sicilNo = l.SicilNo ?? "",
                    konu = l.Konu ?? "",
                    aciklama = l.Aciklama ?? "",
                    kayitTar = l.KayitTar ?? DateTime.Now
                })
                .ToList();
        }

        public IEnumerable<object> GetSmsLogs(string search)
        {
            var query = _context.tb_Sms.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(s => s.Alan.ToLower().Contains(searchLower)
                                      || s.AlanTlf.Contains(searchLower)
                                      || s.Icerik.ToLower().Contains(searchLower));
            }

            return query
                .OrderByDescending(s => s.KayitTarih)
                .Take(100)
                .Select(s => new
                {
                    smsID = s.SmsID,
                    gonderen = s.Gonderen ?? "",
                    alan = s.Alan ?? "",
                    alanTlf = s.AlanTlf ?? "",
                    konu = s.Konu ?? "",
                    icerik = s.Icerik ?? "",
                    kayitTarih = s.KayitTarih ?? DateTime.Now,
                    durum = s.Durum ?? false,
                    gonTarih = s.GonTarih
                })
                .ToList();
        }

        public IEnumerable<object> GetBelgeTarihce(string search)
        {
            var query = _context.tb_BelgeTarihce.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(h => h.BelgeKodu.ToLower().Contains(searchLower)
                                      || h.Konu.ToLower().Contains(searchLower)
                                      || h.Aciklama.ToLower().Contains(searchLower));
            }

            return query
                .OrderByDescending(h => h.BelgeTarihceID)
                .Take(100)
                .Select(h => new
                {
                    belgeTarihceID = h.BelgeTarihceID,
                    belgeKodu = h.BelgeKodu ?? "",
                    konu = h.Konu ?? "",
                    aciklama = h.Aciklama ?? "",
                    kayitTar = h.KayitTar ?? DateTime.Now
                })
                .ToList();
        }

        public IEnumerable<object> GetAiSettings()
        {
            return _context.tb_AiAyarlar
                .AsNoTracking()
                .OrderBy(a => a.SiraNo)
                .Select(a => new
                {
                    id = a.ID,
                    ayarAdi = a.AyarAdi ?? "",
                    provider = a.Provider ?? "",
                    model = a.Model ?? "",
                    apiKey = a.ApiKey ?? "",
                    sistemPrompt = a.SistemPrompt ?? "",
                    maksimumToken = a.MaksimumToken ?? 2000,
                    aktif = a.Aktif ?? false
                })
                .ToList();
        }

        public (bool Success, string Message) SaveAiSetting(tb_AiAyarlar model)
        {
            if (model.ID > 0)
            {
                var setting = _context.tb_AiAyarlar.FirstOrDefault(a => a.ID == model.ID);
                if (setting == null) return (false, "Yapay zeka ayarı bulunamadı.");

                setting.AyarAdi = model.AyarAdi;
                setting.Provider = model.Provider;
                setting.Model = model.Model;
                setting.ApiKey = model.ApiKey;
                setting.SistemPrompt = model.SistemPrompt;
                setting.MaksimumToken = model.MaksimumToken;
                setting.Aktif = model.Aktif;
            }
            else
            {
                int nextSira = (_context.tb_AiAyarlar.Max(a => (int?)a.SiraNo) ?? 0) + 1;
                model.SiraNo = nextSira;
                model.KayitTarihi = DateTime.Now;
                _context.tb_AiAyarlar.Add(model);
            }

            _context.SaveChanges();
            return (true, "Yapay zeka ayarı kaydedildi.");
        }

        public IEnumerable<object> GetTicketCategories()
        {
            return _context.tb_TicketKategori
                .AsNoTracking()
                .OrderBy(c => c.Tanim)
                .Select(c => new
                {
                    id = c.ID,
                    tanim = c.Tanim ?? "",
                    sirketKodu = c.SirketKodu ?? "",
                    durum = c.Durum ?? false
                })
                .ToList();
        }

        public (bool Success, string Message) SaveTicketCategory(tb_TicketKategori model)
        {
            if (model.ID > 0)
            {
                var category = _context.tb_TicketKategori.FirstOrDefault(c => c.ID == model.ID);
                if (category == null) return (false, "Bilet kategorisi bulunamadı.");

                category.Tanim = model.Tanim;
                category.SirketKodu = model.SirketKodu;
                category.Durum = model.Durum;
            }
            else
            {
                _context.tb_TicketKategori.Add(model);
            }

            _context.SaveChanges();
            return (true, "Kategori başarıyla kaydedildi.");
        }

        public bool DeleteTicketCategory(int id)
        {
            var category = _context.tb_TicketKategori.FirstOrDefault(c => c.ID == id);
            if (category != null)
            {
                _context.tb_TicketKategori.Remove(category);
                return _context.SaveChanges() > 0;
            }
            return false;
        }

        public IEnumerable<object> GetHierarchy()
        {
            var query = from h in _context.tb_Hiyerarsi
                        join p in _context.tb_Personel on h.SicilNo equals p.SicilNo into ps
                        from p in ps.DefaultIfEmpty()
                        select new
                        {
                            hiyerarsiID = h.HiyerarsiID,
                            eposta = h.Eposta ?? "",
                            sicilNo = h.SicilNo ?? "",
                            amir1 = h.Amir1 ?? "",
                            amir2 = h.Amir2 ?? "",
                            amir3 = h.Amir3 ?? "",
                            izin = h.izin ?? 0,
                            adSoyad = p != null ? p.AdSoyad : "",
                            unvan = p != null ? p.Unvan : "",
                            sirketKodu = p != null ? p.SirketKodu : ""
                        };

            return query.ToList();
        }

        private static readonly List<(string Kod, string Tanim, string Aciklama)> StaticDocumentTypes = new List<(string, string, string)>
        {
            ("ADAY", "İşe Alım Sorumlusu", "İşe alım talepleri, aday değerlendirmeleri ve mülakat takip süreçlerini yöneten İK personeli yetkisidir."),
            ("ARGE", "Ar-Ge Proje Yöneticisi", "Ar-Ge projeleri, kaynak planlama ve proje toplantı takip süreçlerini yöneten yetkilidir."),
            ("BAKIM", "Bakım Departmanı Sorumlusu", "Makine, tesis, ekipman arıza ve periyodik bakım taleplerini atayan ve onaylayan yetkilidir."),
            ("BAKIMADMIN", "Bakım Departmanı Yöneticisi", "Tüm şirketlere ait Makine, tesis, ekipman arıza ve periyodik bakım taleplerini atayan ve onaylayan yetkilidir."),
            ("ERP", "ERP Sistem Sorumlusu", "ERP sistemi yetkilendirme, modül aktivasyon ve destek taleplerini yöneten yetkilidir."),
            ("GENELMUDUR", "Genel Müdür", "Satınalma onay süreçleri (GMONAY) başta olmak üzere portal genelindeki en üst düzey onay ve yönetim yetkisidir."),
            ("IK", "İnsan Kaynakları Yöneticisi", "İzin talepleri, aday işe alım, İK form onay süreçlerini yöneten yetkilidir."),
            ("ISG", "İSG Sorumlusu", "İş Sağlığı ve Güvenliği risk analizleri, kaza raporları ve İSG talep/destek süreçlerini yöneten yetkilidir."),
            ("IT", "BT (Bilgi Teknolojileri) Yön.", "BT donanım, yazılım, e-posta ve HelpDesk destek taleplerini atayan ve çözümleyen yetkilidir."),
            ("KALITE", "Kalite Güvence Sorumlusu", "Kalite dökümantasyonu, iç/dış denetimler ve Tedarikçi Değerlendirme onay süreçlerini yöneten yetkilidir."),
            ("KITAP", "Kütüphane Sorumlusu", "Çalışanların kitap/eğitim materyali talep süreçlerini ve kitap teslimlerini yöneten yetkilidir."),
            ("OPEKS", "Opeks Sorumlusu", "Operasyonel harcamalar, bütçe aşım onayları ve Opeks iyileştirme projeleri takip yetkisidir."),
            ("SAT-MD", "Satınalma Müdürü", "Belirli limitlerin üzerindeki satınalma tekliflerinin onaylanması (MDONAY) ve süreç takibinden sorumlu müdür yetkisidir."),
            ("SAT-UZ", "Satınalma Uzmanı", "Satınalma taleplerine teklif toplama, teklif girişi (TEKLIF) ve sipariş oluşturma süreçlerinden sorumlu uzman yetkisidir."),
            ("TICKET", "Destek Masası (HelpDesk) Sor.", "Portal genelindeki destek taleplerini (Ticket) departmanlara yönlendiren ve süreç takibini yapan genel yetkilidir."),
            ("URGE", "Ür-Ge Proje Sorumlusu", "Ürün Geliştirme (Ür-Ge) proje süreçleri, kaynak planlama ve ilgili toplantı dökümanlarını yöneten yetkilidir."),
            ("TLPACIL", "Talep-Önem Seviye Yetkilisi", "Bakım taleplerinde önem seviyesini acil olarak girebilen sorumlu yetkisidir.")
        };

        public (bool Success, string Message) UpdateUserPassword(int id, string newPassword)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == id);
            if (user == null) return (false, "Kullanıcı bulunamadı.");

            bool isAD = (user.Eposta ?? "").ToLower().EndsWith("@isiktarim.com");
            if (isAD) return (false, "Active Directory kullanıcısının şifresi değiştirilemez.");

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 5)
            {
                return (false, "Şifre en az 5 karakter olmalıdır.");
            }

            user.Sifre = SecurityHelper.EncryptPassword(newPassword);
            _context.SaveChanges();
            return (true, "Şifre başarıyla güncellendi.");
        }

        public IEnumerable<object> GetUserDocumentTypes(int userId)
        {
            var user = _context.tb_Kullanici.AsNoTracking().FirstOrDefault(u => u.KullaniciID == userId);
            var userCodes = user?.AdminBelgeTur ?? "";

            return StaticDocumentTypes.Select(t => new
            {
                kod = t.Kod,
                tanim = t.Tanim,
                aciklama = t.Aciklama,
                aktif = userCodes.Contains("*" + t.Kod + "*")
            }).ToList();
        }

        public (bool Success, string Message) SaveUserDocumentTypes(int userId, List<string> codes)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == userId);
            if (user == null) return (false, "Kullanıcı bulunamadı.");

            if (codes == null || codes.Count == 0)
            {
                user.AdminBelgeTur = "";
            }
            else
            {
                user.AdminBelgeTur = "*" + string.Join("*", codes.Where(c => !string.IsNullOrEmpty(c))) + "*";
            }

            _context.SaveChanges();
            return (true, "Yönetici ayarları başarıyla kaydedildi.");
        }

        public IEnumerable<object> GetHelpDeskCategories(string search, string categoryId, string typeCode)
        {
            var categories = _context.tb_TalepKategori.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(typeCode))
            {
                categories = categories.Where(c => c.TalepTurKodu == typeCode);
            }

            if (!string.IsNullOrEmpty(categoryId) && int.TryParse(categoryId, out int catId))
            {
                categories = categories.Where(c => c.UstKategoriID == catId || c.TalepKategoriID == catId);
            }

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                categories = categories.Where(c => c.Tanim.ToLower().Contains(searchLower));
            }

            var allList = categories.ToList();

            var allDbCategories = _context.tb_TalepKategori.AsNoTracking().ToList();
            var categoryDict = allDbCategories.ToDictionary(c => c.TalepKategoriID, c => c.Tanim);

            var responsibles = (from ta in _context.tb_TalepAyar.AsNoTracking()
                                join kp in _context.tb_Kullanici.AsNoTracking() on ta.SicilNo equals kp.SicilNo
                                select new
                                {
                                    talepAyarID = ta.TalepAyarID,
                                    kategoriID = ta.KategoriID,
                                    sicilNo = ta.SicilNo,
                                    yoneticiMi = ta.YoneticiMi ?? false,
                                    sirketKodu = ta.SirketKodu ?? "",
                                    adSoyad = kp.AdSoyad ?? "",
                                    eposta = kp.Eposta ?? "",
                                    durum = kp.Durum ?? false
                                }).ToList();

            var companies = _context.tb_Sirket.AsNoTracking().ToDictionary(s => s.SirketKodu, s => s.SirketAdi);

            return allList.Select(c => new
            {
                id = c.TalepKategoriID,
                tanim = c.Tanim ?? "",
                ustKategoriID = c.UstKategoriID,
                ustKategori = c.UstKategoriID.HasValue && categoryDict.ContainsKey(c.UstKategoriID.Value) ? categoryDict[c.UstKategoriID.Value] : null,
                durum = c.Durum,
                talepTurKodu = c.TalepTurKodu ?? "",
                talepTur = c.TalepTurKodu == "IT" ? "Bilgi Teknolojileri" : (c.TalepTurKodu == "ERP" ? "ERP Sistem" : (c.TalepTurKodu == "BAKIM" ? "Bakım Onarım" : c.TalepTurKodu)),
                yetkiBelgeTur = c.YetkiBelgeTur ?? "",
                kullanicilar = responsibles.Where(r => r.kategoriID == c.TalepKategoriID).Select(r => new
                {
                    r.talepAyarID,
                    r.kategoriID,
                    r.sicilNo,
                    r.yoneticiMi,
                    r.sirketKodu,
                    sirketAdi = companies.ContainsKey(r.sirketKodu) ? companies[r.sirketKodu] : (r.sirketKodu == "" ? "Genel" : r.sirketKodu),
                    r.adSoyad,
                    r.eposta,
                    r.durum
                }).ToList()
            }).ToList();
        }

        public object GetHelpDeskCategoryDetail(int id)
        {
            var c = _context.tb_TalepKategori.AsNoTracking().FirstOrDefault(cat => cat.TalepKategoriID == id);
            if (c == null) return null;

            return new
            {
                id = c.TalepKategoriID,
                tanim = c.Tanim ?? "",
                ustKategoriID = c.UstKategoriID,
                durum = c.Durum,
                talepTurKodu = c.TalepTurKodu ?? "",
                yetkiBelgeTur = c.YetkiBelgeTur ?? ""
            };
        }

        public (bool Success, string Message) SaveHelpDeskCategory(tb_TalepKategori model)
        {
            if (string.IsNullOrEmpty(model.Tanim)) return (false, "Kategori tanımı boş olamaz.");
            if (string.IsNullOrEmpty(model.TalepTurKodu)) return (false, "Talep türü seçilmelidir.");

            if (model.TalepKategoriID > 0)
            {
                var category = _context.tb_TalepKategori.FirstOrDefault(c => c.TalepKategoriID == model.TalepKategoriID);
                if (category == null) return (false, "Kategori bulunamadı.");

                category.Tanim = model.Tanim;
                category.UstKategoriID = model.UstKategoriID;
                category.TalepTurKodu = model.TalepTurKodu;
                category.Durum = model.Durum;
                category.YetkiBelgeTur = model.YetkiBelgeTur;
            }
            else
            {
                _context.tb_TalepKategori.Add(model);
            }

            _context.SaveChanges();
            return (true, "Kategori başarıyla kaydedildi.");
        }

        public bool DeleteHelpDeskCategory(int id)
        {
            var category = _context.tb_TalepKategori.FirstOrDefault(c => c.TalepKategoriID == id);
            if (category != null)
            {
                var ayarlar = _context.tb_TalepAyar.Where(a => a.KategoriID == id).ToList();
                _context.tb_TalepAyar.RemoveRange(ayarlar);

                _context.tb_TalepKategori.Remove(category);
                return _context.SaveChanges() > 0;
            }
            return false;
        }

        public (bool Success, string Message) SaveCategoryResponsible(tb_TalepAyar model)
        {
            if (string.IsNullOrEmpty(model.SicilNo)) return (false, "Personel seçilmelidir.");
            if (!model.KategoriID.HasValue) return (false, "Kategori ID belirtilmelidir.");

            bool exists = _context.tb_TalepAyar.Any(a => a.KategoriID == model.KategoriID
                                                    && a.SicilNo == model.SicilNo
                                                    && a.SirketKodu == model.SirketKodu);
            if (exists)
            {
                return (false, "Bu personel bu şirket için bu kategoriye zaten atanmış.");
            }

            _context.tb_TalepAyar.Add(model);
            _context.SaveChanges();
            return (true, "Sorumlu başarıyla atandı.");
        }

        public bool DeleteCategoryResponsible(int id)
        {
            var ayar = _context.tb_TalepAyar.FirstOrDefault(a => a.TalepAyarID == id);
            if (ayar != null)
            {
                _context.tb_TalepAyar.Remove(ayar);
                return _context.SaveChanges() > 0;
            }
            return false;
        }

        public IEnumerable<object> GetHelpDeskTypes()
        {
            return new List<object>
            {
                new { kod = "IT", tanim = "Bilgi Teknolojileri (IT)" },
                new { kod = "ERP", tanim = "ERP Sistem" },
                new { kod = "BAKIM", tanim = "Bakım Onarım (Bakım)" }
            };
        }

        public IEnumerable<object> GetCompanies()
        {
            return _context.tb_Sirket
                .AsNoTracking()
                .OrderBy(s => s.SirketAdi)
                .Select(s => new
                {
                    sirketKodu = s.SirketKodu,
                    sirketAdi = s.SirketAdi
                })
                .ToList();
        }

        public (IEnumerable<object> Items, int TotalCount) GetLogsPaged(string search, string userEmail, DateTime? startDate, DateTime? endDate, int page, int pageSize)
        {
            var query = _context.tb_Log.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(l => l.Konu.ToLower().Contains(searchLower)
                                      || l.Aciklama.ToLower().Contains(searchLower)
                                      || l.SicilNo.Contains(searchLower)
                                      || l.Eposta.ToLower().Contains(searchLower));
            }

            if (!string.IsNullOrEmpty(userEmail))
            {
                string emailLower = userEmail.ToLower();
                query = query.Where(l => l.Eposta.ToLower() == emailLower);
            }

            if (startDate.HasValue)
            {
                query = query.Where(l => l.KayitTar >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddSeconds(-1);
                query = query.Where(l => l.KayitTar <= endOfDay);
            }

            int totalCount = query.Count();
            int skip = (page - 1) * pageSize;

            var items = query
                .OrderByDescending(l => l.KayitTar)
                .Skip(skip)
                .Take(pageSize)
                .Select(l => new
                {
                    eposta = l.Eposta ?? "",
                    sicilNo = l.SicilNo ?? "",
                    konu = l.Konu ?? "",
                    aciklama = l.Aciklama ?? "",
                    kayitTar = l.KayitTar ?? DateTime.Now
                })
                .ToList();

            return (items, totalCount);
        }

        public (IEnumerable<object> Items, int TotalCount) GetBelgeTarihcePaged(string search, string documentCode, DateTime? startDate, DateTime? endDate, int page, int pageSize)
        {
            var query = _context.tb_BelgeTarihce.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(h => h.BelgeKodu.ToLower().Contains(searchLower)
                                      || h.Konu.ToLower().Contains(searchLower)
                                      || h.Aciklama.ToLower().Contains(searchLower));
            }

            if (!string.IsNullOrEmpty(documentCode))
            {
                string docLower = documentCode.ToLower();
                query = query.Where(h => h.BelgeKodu.ToLower().Contains(docLower));
            }

            if (startDate.HasValue)
            {
                query = query.Where(h => h.KayitTar >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddSeconds(-1);
                query = query.Where(h => h.KayitTar <= endOfDay);
            }

            int totalCount = query.Count();
            int skip = (page - 1) * pageSize;

            var items = query
                .OrderByDescending(h => h.BelgeTarihceID)
                .Skip(skip)
                .Take(pageSize)
                .Select(h => new
                {
                    belgeTarihceID = h.BelgeTarihceID,
                    belgeKodu = h.BelgeKodu ?? "",
                    konu = h.Konu ?? "",
                    aciklama = h.Aciklama ?? "",
                    kayitTar = h.KayitTar ?? DateTime.Now
                })
                .ToList();

            return (items, totalCount);
        }
    }
}
