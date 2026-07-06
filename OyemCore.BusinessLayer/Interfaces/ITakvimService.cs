using System.Collections.Generic;
using System.Threading.Tasks;
using OyemCore.BusinessLayer.Dtos;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface ITakvimService
    {
        Task<IEnumerable<TakvimDto>> GetTakvimEventsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<TakvimDto> GetTakvimEventByIdAsync(int id);
        Task<TakvimDto> CreateTakvimEventAsync(TakvimDto dto);
        Task<bool> UpdateTakvimEventAsync(int id, TakvimDto dto);
        Task<bool> DeleteTakvimEventAsync(int id);
        Task<IEnumerable<OyemCore.DataLayer.Entities.tb_TakvimAyar>> GetCategoriesAsync();
    }
}
