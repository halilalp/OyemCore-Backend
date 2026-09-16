using System.Collections.Generic;
using OyemCore.BusinessLayer.Dtos;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Bakım Planı / Periyodik Kontrol Planı ortak "Temizlik Onay Formu" akışı.
    // Referans: WebPortal App_Code/WebServicePlanTemizlikOnay.cs.
    public interface ITemizlikOnayService
    {
        // BakimService.UpdateBakimPlanStatus / UpdatePeriyodikStatus, Durum=TAMAMLANDI geldiğinde
        // planı ONAY durumuna aldıktan hemen sonra bunu çağırır. secilenSicil boşsa hata fırlatır.
        int CreateOnayKaydi(string planTuru, string planKodu, string secenSicil, string secilenSicil);

        TemizlikOnayDurumDto GetDurum(string planTuru, string planKodu);
        TemizlikOnayDetayDto GetDetay(int onayId, string sicilNo);
        void Kaydet(int onayId, string sicilNo, SaveTemizlikOnayRequest request);
        void Reddet(int onayId, string sicilNo, string aciklama);
        IEnumerable<TemizlikOnayBekleyenDto> GetBekleyenlerim(string sicilNo);
    }
}
