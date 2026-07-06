using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IHaberService
    {
        IEnumerable<object> GetHaberList(int kullaniciID, string search, string startDate, string endDate);
        object GetHaberDetail(int id);
        bool SaveHaber(int kullaniciID, string konu, string aciklama, string profilUrl);
        bool UpdateHaber(int kullaniciID, int haberID, string konu, string aciklama, string profilUrl);
        bool DeleteHaber(int kullaniciID, int haberID);
    }
}
