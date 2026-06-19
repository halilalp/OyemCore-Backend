using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.DataLayer.Interfaces;

namespace WebPortalSpace.BusinessLayer.Services
{
    public class IzinService : IIzinService
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;

        public IzinService(IYbsDbContext context, IPushNotificationService pushNotificationService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
        }

        public (bool Success, string Message) CustomError(string msg) => (false, msg);

        private bool HasHRAuthority(tb_Kullanici user)
        {
            if (user == null) return false;
            if (user.KullaniciAdi == "admin") return true;
            if (string.IsNullOrEmpty(user.AdminBelgeTur)) return false;
            var tokens = user.AdminBelgeTur.Split('*', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpper());
            return tokens.Contains("IK") || tokens.Contains("ADMIN") || tokens.Contains("SYS") || tokens.Contains("ADM");
        }

        public (IEnumerable<object> Requests, int YillikIzinBalance) GetIzinRequests(int kullaniciID)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return (Enumerable.Empty<object>(), 0);

            var requests = (from i in _context.tb_IzinOnay
                            join p in _context.tb_Personel on i.KayitSicil equals p.SicilNo into ps
                            from p in ps.DefaultIfEmpty()
                            where i.KayitSicil == user.SicilNo
                            orderby i.KayitTar descending
                            select new
                            {
                                i.IzinOnayID,
                                i.BelgeNo,
                                i.IzinTuru,
                                i.Aciklama,
                                i.KayitSicil,
                                i.KayitEposta,
                                CikisTarStr = i.CikisTar != null ? i.CikisTar.Value.ToString("dd.MM.yyyy") : "",
                                IsBasiTarStr = i.IsBasiTar != null ? i.IsBasiTar.Value.ToString("dd.MM.yyyy") : "",
                                i.IsGunu,
                                KayitTarStr = i.KayitTar != null ? i.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                i.Durum,
                                i.SurecDurum,
                                i.BekleyenOnay,
                                i.SonDurumBilgi,
                                AdSoyad = p != null ? p.AdSoyad : "",
                                Unvan = p != null ? p.Unvan : ""
                            }).ToList();

            return (requests, (int)(user.YillikIzin ?? 0));
        }

        public IEnumerable<object> GetIzinApprovals(int kullaniciID)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return Enumerable.Empty<object>();

            bool isHR = HasHRAuthority(user);

            var approvals = (from i in _context.tb_IzinOnay
                             join p in _context.tb_Personel on i.KayitSicil equals p.SicilNo into ps
                             from p in ps.DefaultIfEmpty()
                             where (i.BekleyenOnay == user.SicilNo && i.SurecDurum == "BEKLEMEDE") ||
                                   (isHR && i.SurecDurum == "IKONAY" && i.Durum == null)
                             orderby i.KayitTar descending
                             select new
                             {
                                 i.IzinOnayID,
                                 i.BelgeNo,
                                 i.IzinTuru,
                                 i.Aciklama,
                                 i.KayitSicil,
                                 i.KayitEposta,
                                 CikisTarStr = i.CikisTar != null ? i.CikisTar.Value.ToString("dd.MM.yyyy") : "",
                                 IsBasiTarStr = i.IsBasiTar != null ? i.IsBasiTar.Value.ToString("dd.MM.yyyy") : "",
                                 i.IsGunu,
                                 KayitTarStr = i.KayitTar != null ? i.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                                 i.Durum,
                                 i.SurecDurum,
                                 i.BekleyenOnay,
                                 i.SonDurumBilgi,
                                 AdSoyad = p != null ? p.AdSoyad : "",
                                 Unvan = p != null ? p.Unvan : ""
                             }).ToList();

            return approvals;
        }

        public bool SaveIzinRequest(int kullaniciID, tb_IzinOnay request)
        {
            var user = _context.tb_Kullanici
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");

            // Get hierarchy amir
            var amirInfo = _context.tb_Hiyerarsi
                .AsNoTracking()
                .FirstOrDefault(h => h.SicilNo == user.SicilNo);

            string amir1 = amirInfo?.Amir1;
            if (string.IsNullOrEmpty(amir1))
            {
                throw new InvalidOperationException("Onay amiriniz tanımlanmamış. Lütfen İK ile iletişime geçiniz.");
            }

            // Generate BelgeNo
            int totalCount = _context.tb_IzinOnay.Count();
            string code = $"IZN-{DateTime.Now:yyyyMMdd}-{totalCount + 1:000}";

            request.BelgeNo = code;
            request.KayitSicil = user.SicilNo;
            request.KayitEposta = user.Eposta;
            request.KayitTar = DateTime.Now;
            request.Durum = null;
            request.SurecDurum = "BEKLEMEDE";
            request.BekleyenOnay = amir1;
            var amir1Name = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == amir1)?.AdSoyad ?? amir1;
            request.SonDurumBilgi = $"{amir1Name} Onay Kutusunda ({DateTime.Now:d.MM.yyyy HH:mm})";

            _context.tb_IzinOnay.Add(request);

            // Add approval flow records in tb_BelgeOnay
            short sira = 1;
            if (!string.IsNullOrEmpty(amir1))
            {
                _context.tb_BelgeOnay.Add(new tb_BelgeOnay
                {
                    BelgeNo = code,
                    Sira = sira++,
                    OnaySicil = amir1,
                    Durum = null,
                    OnayTur = "ONAY"
                });
            }
            if (amirInfo != null && !string.IsNullOrEmpty(amirInfo.Amir2))
            {
                _context.tb_BelgeOnay.Add(new tb_BelgeOnay
                {
                    BelgeNo = code,
                    Sira = sira++,
                    OnaySicil = amirInfo.Amir2,
                    Durum = null,
                    OnayTur = "ONAY"
                });
            }
            if (amirInfo != null && !string.IsNullOrEmpty(amirInfo.Amir3))
            {
                _context.tb_BelgeOnay.Add(new tb_BelgeOnay
                {
                    BelgeNo = code,
                    Sira = sira++,
                    OnaySicil = amirInfo.Amir3,
                    Durum = null,
                    OnayTur = "ONAY"
                });
            }

            _context.SaveChanges();

            BelgeTarihceKaydet(code, "İzin Talebi Oluşturuldu", $"Yeni izin talebi açıldı. (Talep Eden: {user.AdSoyad})");

            _ = _pushNotificationService.NotifyNewLeaveRequestAsync(request.IzinOnayID);

            return true;
        }

        public bool ApproveIzinRequest(int kullaniciID, int izinOnayID)
        {
            var user = _context.tb_Kullanici
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return false;

            var request = _context.tb_IzinOnay
                .FirstOrDefault(i => i.IzinOnayID == izinOnayID);

            if (request == null) throw new InvalidOperationException("İzin talebi bulunamadı.");

            if (request.SurecDurum == "IKONAY")
            {
                if (!HasHRAuthority(user)) throw new InvalidOperationException("Bu talebi onaylama yetkiniz bulunmuyor.");

                // Update tb_BelgeOnay ISLEM record
                var approvalRecord = _context.tb_BelgeOnay
                    .FirstOrDefault(b => b.BelgeNo == request.BelgeNo && b.OnayTur == "ISLEM" && b.Sira == 1 && b.Durum == null);
                if (approvalRecord != null)
                {
                    approvalRecord.OnaySicil = user.SicilNo;
                    approvalRecord.Durum = true;
                    approvalRecord.IslemTar = DateTime.Now;
                    approvalRecord.Aciklama = "İK Tarafından Onaylandı";
                }

                // Final Approval Details
                request.Durum = true;
                request.SurecDurum = "ONAYLANDI";
                request.SonDurumBilgi = $"Talep {user.AdSoyad} ile tamamlandı. ({DateTime.Now:dd.MM.yyyy HH:mm})";

                // Decrement Annual Leave Balance
                if (request.IsGunu.HasValue)
                {
                    var requesterUser = _context.tb_Kullanici
                        .FirstOrDefault(u => u.SicilNo == request.KayitSicil);
                    if (requesterUser != null)
                    {
                        requesterUser.YillikIzin = (requesterUser.YillikIzin ?? 0) - (request.IsGunu ?? 0);
                    }
                }

                _context.SaveChanges();

                BelgeTarihceKaydet(request.BelgeNo, "İzin Onaylandı (İK)", $"İzin talebi İK tarafından onaylandı ve tamamlandı. (İşlem Yapan: {user.AdSoyad})");

                _ = _pushNotificationService.NotifyLeaveRequestCompletedAsync(request.IzinOnayID, user.KullaniciID);

                return true;
            }
            else
            {
                if (request.BekleyenOnay != user.SicilNo) throw new InvalidOperationException("Bu talebi onaylama yetkiniz bulunuyor.");

                // Get hierarchy for the request creator
                var amirInfo = _context.tb_Hiyerarsi
                    .AsNoTracking()
                    .FirstOrDefault(h => h.SicilNo == request.KayitSicil);

                string nextAmir = null;
                string stateInfo = "Onaylandı";

                if (amirInfo != null)
                {
                    string amir1 = amirInfo.Amir1;
                    string amir2 = amirInfo.Amir2;
                    string amir3 = amirInfo.Amir3;

                    if (user.SicilNo == amir1 && !string.IsNullOrEmpty(amir2))
                    {
                        nextAmir = amir2;
                        stateInfo = "2. Amir Onayı Bekliyor";
                    }
                    else if (user.SicilNo == amir2 && !string.IsNullOrEmpty(amir3))
                    {
                        nextAmir = amir3;
                        stateInfo = "3. Amir Onayı Bekliyor";
                    }
                }

                // Update tb_BelgeOnay record
                var approvalRecord = _context.tb_BelgeOnay
                    .FirstOrDefault(b => b.BelgeNo == request.BelgeNo && b.OnaySicil == user.SicilNo && b.OnayTur == "ONAY" && b.Durum == null);
                if (approvalRecord != null)
                {
                    approvalRecord.Durum = true;
                    approvalRecord.IslemTar = DateTime.Now;
                    approvalRecord.Aciklama = "Onaylandı";
                }

                if (string.IsNullOrEmpty(nextAmir))
                {
                    // Manager hierarchy completed! Send to HR (İK)
                    request.BekleyenOnay = null;
                    request.SurecDurum = "IKONAY";
                    request.SonDurumBilgi = $"İK Onay Kutusunda ({DateTime.Now:dd.MM.yyyy HH:mm})";

                    // Insert open ISLEM record for HR
                    _context.tb_BelgeOnay.Add(new tb_BelgeOnay
                    {
                        BelgeNo = request.BelgeNo,
                        OnayTur = "ISLEM",
                        Sira = 1,
                        Durum = null
                    });

                    _context.SaveChanges();

                    BelgeTarihceKaydet(request.BelgeNo, "İzin Onaylandı", $"İzin talebi amir onaylarından geçerek İK onayına sevk edildi. (Son Onaylayan: {user.AdSoyad})");

                    _ = _pushNotificationService.NotifyLeaveManagerApprovalsCompletedAsync(request.IzinOnayID);
                }
                else
                {
                    // Approved by current amir, pending next amir
                    request.BekleyenOnay = nextAmir;
                    var nextAmirName = _context.tb_Personel.AsNoTracking().FirstOrDefault(p => p.SicilNo == nextAmir)?.AdSoyad ?? nextAmir;
                    request.SonDurumBilgi = $"{nextAmirName} Onay Kutusunda ({DateTime.Now:dd.MM.yyyy HH:mm})";
                    _context.SaveChanges();

                    BelgeTarihceKaydet(request.BelgeNo, "İzin Kısmi Onay", $"İzin talebi onaylandı, bir sonraki onay aşamasına geçildi. (Onaylayan: {user.AdSoyad})");

                    _ = _pushNotificationService.NotifyNewLeaveRequestAsync(request.IzinOnayID);
                }

                return true;
            }
        }

        public bool RejectIzinRequest(int kullaniciID, int izinOnayID)
        {
            var user = _context.tb_Kullanici
                .AsNoTracking()
                .FirstOrDefault(u => u.KullaniciID == kullaniciID);

            if (user == null) return false;

            var request = _context.tb_IzinOnay
                .FirstOrDefault(i => i.IzinOnayID == izinOnayID);

            if (request == null) throw new InvalidOperationException("İzin talebi bulunamadı.");

            if (request.SurecDurum == "IKONAY")
            {
                if (!HasHRAuthority(user)) throw new InvalidOperationException("Bu talebi reddetme yetkiniz bulunmuyor.");

                // Update tb_BelgeOnay ISLEM record
                var approvalRecord = _context.tb_BelgeOnay
                    .FirstOrDefault(b => b.BelgeNo == request.BelgeNo && b.OnayTur == "ISLEM" && b.Sira == 1 && b.Durum == null);
                if (approvalRecord != null)
                {
                    approvalRecord.OnaySicil = user.SicilNo;
                    approvalRecord.Durum = false;
                    approvalRecord.IslemTar = DateTime.Now;
                    approvalRecord.Aciklama = "İK Tarafından Reddedildi";
                }

                // Final Rejection Details
                request.BekleyenOnay = null;
                request.Durum = false;
                request.SurecDurum = "REDDEDILDI";
                request.SonDurumBilgi = $"Talep {user.AdSoyad} tarafından reddedildi. ({DateTime.Now:dd.MM.yyyy HH:mm})";

                _context.SaveChanges();

                BelgeTarihceKaydet(request.BelgeNo, "İzin Reddedildi (İK)", $"İzin talebi İK tarafından reddedildi. (Reddeden: {user.AdSoyad})");

                _ = _pushNotificationService.NotifyLeaveRequestRejectedAsync(request.IzinOnayID, user.KullaniciID);

                return true;
            }
            else
            {
                if (request.BekleyenOnay != user.SicilNo) throw new InvalidOperationException("Bu talebi reddetme yetkiniz bulunmuyor.");

                // Update tb_BelgeOnay record
                var approvalRecord = _context.tb_BelgeOnay
                    .FirstOrDefault(b => b.BelgeNo == request.BelgeNo && b.OnaySicil == user.SicilNo && b.OnayTur == "ONAY" && b.Durum == null);
                if (approvalRecord != null)
                {
                    approvalRecord.Durum = false;
                    approvalRecord.IslemTar = DateTime.Now;
                    approvalRecord.Aciklama = "Reddedildi";
                }

                request.BekleyenOnay = null;
                request.Durum = false;
                request.SurecDurum = "REDDEDILDI";
                request.SonDurumBilgi = $"{user.AdSoyad} Tarafından Reddedildi. ({DateTime.Now:dd.MM.yyyy HH:mm})";
                _context.SaveChanges();

                BelgeTarihceKaydet(request.BelgeNo, "İzin Reddedildi", $"İzin talebi reddedildi. (Reddeden: {user.AdSoyad})");

                _ = _pushNotificationService.NotifyLeaveRequestRejectedAsync(request.IzinOnayID, user.KullaniciID);

                return true;
            }
        }

        public IEnumerable<object> GetIzinHistory(string belgeNo)
        {
            return _context.tb_BelgeTarihce
                .AsNoTracking()
                .Where(h => h.BelgeKodu == belgeNo)
                .OrderByDescending(h => h.BelgeTarihceID)
                .Select(h => new
                {
                    h.BelgeTarihceID,
                    h.BelgeKodu,
                    Tarih = h.KayitTar.HasValue ? h.KayitTar.Value.ToString("dd.MM.yyyy HH:mm") : "",
                    h.Konu,
                    h.Aciklama
                })
                .ToList();
        }

        private void BelgeTarihceKaydet(string code, string konu, string aciklama)
        {
            try
            {
                var history = new tb_BelgeTarihce
                {
                    BelgeKodu = code,
                    Konu = konu,
                    Aciklama = aciklama,
                    KayitTar = DateTime.Now
                };
                _context.tb_BelgeTarihce.Add(history);
                _context.SaveChanges();
            }
            catch { }
        }
    }
}
