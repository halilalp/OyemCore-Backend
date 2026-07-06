using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface INotificationService
    {
        Task SendMailAsync(string uygulama, string konu, string icerik, string eposta);
    }
}