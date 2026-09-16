using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Daily.co görüntülü görüşme odası oluşturma. referans: ChatHub.CreateDailyRoom (webportal).
    public interface IDailyCallService
    {
        // Yeni bir Daily.co odası oluşturur ve katılım URL'sini döndürür.
        Task<string> CreateRoomAsync();
    }
}
