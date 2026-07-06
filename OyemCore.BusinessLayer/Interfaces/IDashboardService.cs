using System.Collections.Generic;
using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IDashboardService
    {
        Task<object> GetWeatherAsync(string city);
        Task<object> GetCurrenciesAsync();
        IEnumerable<object> GetBirthdays();
        IEnumerable<object> GetTrainings();
        IEnumerable<object> GetContacts();
        IEnumerable<object> GetNews();
        object GetNewsDetail(int id);
        IEnumerable<object> GetMenu(int userId);
        object DbDebug();
    }
}
