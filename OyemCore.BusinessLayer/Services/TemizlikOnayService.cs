using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // Bakım Planı / Periyodik Kontrol Planı ortak "Temizlik Onay Formu" akışı.
    // Referans: WebPortal App_Code/WebServicePlanTemizlikOnay.cs (davranış birebir).
    public class TemizlikOnayService : ITemizlikOnayService
    {
        private readonly IYbsDbContext _context;
        private readonly IPushNotificationService _push;

        public TemizlikOnayService(IYbsDbContext context, IPushNotificationService push)
        {
            _context = context;
            _push = push;
        }

        public int CreateOnayKaydi(string planTuru, string planKodu, string secenSicil, string secilenSicil)
        {
            if (string.IsNullOrEmpty(secilenSicil))
                throw new InvalidOperationException("Lütfen temizlik onay formunu dolduracak personeli seçiniz.");

            var onay = new tb_BakimPlanTemizlikOnay
            {
                PlanTuru = planTuru,
                PlanKodu = planKodu,
                SecenSicil = secenSicil,
                SecilenSicil = secilenSicil,
                OnayDurumu = "BEKLEMEDE",
                KayitTar = DateTime.Now
            };
            _context.tb_BakimPlanTemizlikOnay.Add(onay);
            _context.SaveChanges();

            _ = _push.NotifyTemizlikOnayCreatedAsync(onay.OnayID);

            return onay.OnayID;
        }

        public TemizlikOnayDurumDto GetDurum(string planTuru, string planKodu)
        {
            var onay = _context.tb_BakimPlanTemizlikOnay
                .Where(o => o.PlanTuru == planTuru && o.PlanKodu == planKodu)
                .OrderByDescending(o => o.OnayID)
                .FirstOrDefault();

            if (onay == null) return new TemizlikOnayDurumDto { Exists = false };

            var secilen = _context.tb_Personel.FirstOrDefault(p => p.SicilNo == onay.SecilenSicil);

            return new TemizlikOnayDurumDto
            {
                Exists = true,
                OnayID = onay.OnayID,
                OnayDurumu = onay.OnayDurumu,
                SecilenAdSoyad = secilen != null ? secilen.AdSoyad : onay.SecilenSicil,
                SecilenSicil = onay.SecilenSicil,
                KayitTarStr = onay.KayitTar.ToString("dd.MM.yyyy HH:mm"),
                OnayTarStr = onay.OnayTar.HasValue ? onay.OnayTar.Value.ToString("dd.MM.yyyy HH:mm") : ""
            };
        }

        public TemizlikOnayDetayDto GetDetay(int onayId, string sicilNo)
        {
            var onay = _context.tb_BakimPlanTemizlikOnay.SingleOrDefault(o => o.OnayID == onayId);
            if (onay == null) throw new InvalidOperationException("Onay kaydı bulunamadı.");

            // Form BEKLEMEDE iken yalnızca doldurması gereken kişi görebilir (düzenleme koruması).
            // ONAYLANDI olduktan sonra plan/kontrolün tamamlanmış kaydının bir parçası haline gelir
            // ve plana erişimi olan herkes (tarihçe gibi) salt okunur görebilir.
            if (onay.SecilenSicil != sicilNo && onay.OnayDurumu != "ONAYLANDI")
                throw new InvalidOperationException("Bu formu doldurma yetkiniz bulunmamaktadır.");

            string planAciklama = "";
            if (onay.PlanTuru == "PERIYODIK")
            {
                var kontrol = _context.tb_BakimPerKontrol.SingleOrDefault(o => o.KontrolKodu == onay.PlanKodu);
                if (kontrol != null) planAciklama = kontrol.Aciklama;
            }

            return new TemizlikOnayDetayDto
            {
                OnayID = onay.OnayID,
                PlanTuru = onay.PlanTuru,
                PlanKodu = onay.PlanKodu,
                OnayDurumu = onay.OnayDurumu,
                PlanAciklama = planAciklama,
                EksikSomunDurum = onay.EksikSomunDurum,
                YagDurum = onay.YagDurum,
                MiknatisDurum = onay.MiknatisDurum,
                FazlaParcaDurum = onay.FazlaParcaDurum,
                GuvRiskDurum = onay.GuvRiskDurum,
                MakineDurum = onay.MakineDurum,
                TemizlikDurum = onay.TemizlikDurum,
                GidaRiskDurum = onay.GidaRiskDurum,
                OnayAciklama = onay.OnayAciklama
            };
        }

        public void Kaydet(int onayId, string sicilNo, SaveTemizlikOnayRequest request)
        {
            var onay = _context.tb_BakimPlanTemizlikOnay.SingleOrDefault(o => o.OnayID == onayId);
            if (onay == null) throw new InvalidOperationException("Onay kaydı bulunamadı.");

            if (onay.SecilenSicil != sicilNo)
                throw new InvalidOperationException("Bu formu doldurma yetkiniz bulunmamaktadır.");

            if (onay.OnayDurumu == "ONAYLANDI")
                throw new InvalidOperationException("Bu form daha önce onaylanmış.");

            onay.EksikSomunDurum = request.EksikSomun;
            onay.YagDurum = request.Yag;
            onay.MiknatisDurum = request.Miknatis;
            onay.FazlaParcaDurum = request.FazlaParca;
            onay.GuvRiskDurum = request.Guvenlik;
            onay.MakineDurum = request.Makine;
            onay.TemizlikDurum = request.Temizlik;
            onay.GidaRiskDurum = request.Gida;
            onay.OnayAciklama = request.Aciklama;
            onay.OnayDurumu = "ONAYLANDI";
            onay.OnayTar = DateTime.Now;

            // Form onaylandı: sürecin kendisi de artık gerçekten tamamlandı sayılır.
            if (onay.PlanTuru == "PERIYODIK")
            {
                var kontrol = _context.tb_BakimPerKontrol.SingleOrDefault(o => o.KontrolKodu == onay.PlanKodu);
                if (kontrol != null && kontrol.Durum == "ONAY") kontrol.Durum = "TAMAMLANDI";
            }
            else
            {
                var plan = _context.tb_BakimPlan.SingleOrDefault(o => o.PlanKodu == onay.PlanKodu);
                if (plan != null && plan.Durum == "ONAY") plan.Durum = "TAMAMLANDI";
            }

            _context.SaveChanges();

            _ = _push.NotifyTemizlikOnayCompletedAsync(onay.OnayID);
        }

        public void Reddet(int onayId, string sicilNo, string aciklama)
        {
            var onay = _context.tb_BakimPlanTemizlikOnay.SingleOrDefault(o => o.OnayID == onayId);
            if (onay == null) throw new InvalidOperationException("Onay kaydı bulunamadı.");

            if (onay.SecilenSicil != sicilNo)
                throw new InvalidOperationException("Bu işlem için yetkiniz bulunmamaktadır.");

            if (onay.OnayDurumu == "ONAYLANDI")
                throw new InvalidOperationException("Bu form daha önce onaylanmış.");

            onay.OnayDurumu = "REDDEDILDI";
            onay.OnayAciklama = aciklama;
            onay.OnayTar = DateTime.Now;

            // Süreç işlemi yapan kişiye geri döner: DEVAM durumuna alınır, bitiş tarihi
            // sıfırlanır çünkü fiilen tamamlanmadığı bildirildi.
            if (onay.PlanTuru == "PERIYODIK")
            {
                var kontrol = _context.tb_BakimPerKontrol.SingleOrDefault(o => o.KontrolKodu == onay.PlanKodu);
                if (kontrol != null && kontrol.Durum == "ONAY")
                {
                    kontrol.Durum = "DEVAM";
                    kontrol.BitisTar = null;
                }
            }
            else
            {
                var plan = _context.tb_BakimPlan.SingleOrDefault(o => o.PlanKodu == onay.PlanKodu);
                if (plan != null && plan.Durum == "ONAY")
                {
                    plan.Durum = "DEVAM";
                    plan.BitisTar = null;
                }
            }

            _context.SaveChanges();

            _ = _push.NotifyTemizlikOnayRejectedAsync(onay.OnayID);
        }

        public IEnumerable<TemizlikOnayBekleyenDto> GetBekleyenlerim(string sicilNo)
        {
            return _context.tb_BakimPlanTemizlikOnay
                .Where(o => o.SecilenSicil == sicilNo && o.OnayDurumu == "BEKLEMEDE")
                .OrderByDescending(o => o.KayitTar)
                .Select(o => new TemizlikOnayBekleyenDto
                {
                    OnayID = o.OnayID,
                    PlanTuru = o.PlanTuru,
                    PlanKodu = o.PlanKodu,
                    KayitTarStr = o.KayitTar.ToString("dd.MM.yyyy HH:mm")
                }).ToList();
        }
    }
}
