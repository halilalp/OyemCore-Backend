using System.Collections.Generic;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.DataLayer.Entities;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IBakimService
    {
        IEnumerable<MakineDto> GetMakines(string sirketKodu, string bolumKodu, string aramaText);
        bool SaveMakine(tb_Makine makine);
        (IEnumerable<BakimPlanDto> Data, int TotalCount) GetBakimPlanList(string sirket, string bolum, string hat, string durum, string bakimTuru, string arama, int pageIndex, int pageSize);
        string SaveBakimPlan(string planKodu, string hatKodu, string bakimTuru, string basTar, string bitTar, string sicil);
        bool UpdateBakimPlanStatus(string planKodu, string durum, string note, string dosyaUrl, string sicil);
        IEnumerable<BakimPlanDetayDto> GetBakimNotlari(string planKodu);
        bool DeleteBakimPlan(string planKodu);
        bool DeleteBakimGelisme(int id);
        (IEnumerable<PeriyodikKontrolDto> Data, int TotalCount) GetPeriyodikKontrolList(string sirket, string bolum, string durum, string kontrolTuru, string arama, int pageIndex, int pageSize);
        string SavePeriyodikKontrol(string kontrolKodu, string bolumKodu, string kontrolTuru, string basTar, string bitTar, string aciklama, string sicil);
        bool UpdatePeriyodikStatus(string kontrolKodu, string status, string aciklama, string sicil);
        bool DeletePeriyodikKontrol(string kontrolKodu);
        IEnumerable<PeriyodikSarfiyatDto> GetPeriyodikSarfiyats(string kontrolKodu);
        bool SavePeriyodikSarfiyat(string kontrolKodu, string malzemeKodu, decimal miktar, string makineKodu, string sicil);
        bool DeletePeriyodikSarfiyat(int id);
        IEnumerable<BakimPlanDetayDto> GetPeriyodikGelismeler(string kontrolKodu);
        bool SavePeriyodikGelisme(string kontrolKodu, string aciklama, string dosyaUrl, string sicil);
        bool DeletePeriyodikGelisme(int id);
        (IEnumerable<MalzemeDto> Results, bool HasMore) SearchMalzemes(string term, int page, int pageSize, bool sarfOnly);
        BakimDropdownsDto GetBakimDropdowns(string sicilNo, string adminBelgeTur);
        IEnumerable<PersonelPerformansRaporuDto> GetPersonelPerformansRaporu(string yil, string ay, string sirket);
        IEnumerable<BakimDashboardStatsDto> GetBakimDashboardStats(string yillar, string sirket);
    }
}
