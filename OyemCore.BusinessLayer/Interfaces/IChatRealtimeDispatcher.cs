using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Gerçek-zamanlı dağıtım soyutlaması. Implementasyonu Backend katmanında
    // IHubContext<ChatHub> ile yapılır (BusinessLayer, SignalR'a bağımlı olmasın diye).
    public interface IChatRealtimeDispatcher
    {
        // Verilen sicillere ait tüm bağlantılara bir hub metodu gönderir (receiveMessage, reloadSidebar, receiveNotification ...).
        void SendToSicils(IEnumerable<string> sicils, string method, params object[] args);
        // Bir sicil çevrimiçi mi (aktif bağlantısı var mı).
        bool IsOnline(string sicilNo);
    }
}
