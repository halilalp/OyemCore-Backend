using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IEgitimService
    {
        IEnumerable<object> GetEgitimList(int kullaniciID, string search);
        IEnumerable<object> GetEgitimCategories();
        bool SaveEgitim(int kullaniciID, string konu, string aciklama, int kategoriID, string dosyaUrl);
        bool UpdateEgitim(int kullaniciID, int egitimID, string konu, string aciklama, int kategoriID, string dosyaUrl);
        bool DeleteEgitim(int kullaniciID, int egitimID);
    }
}
