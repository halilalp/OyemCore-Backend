using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Uygulama-içi bildirim merkezi (zil ikonu). referans: WebServiceBildirim
    public interface IBildirimService
    {
        // Sayfalı bildirim listesi (kullanıcının sicilNo'suna göre, yeniden eskiye).
        (int total, IEnumerable<object> data) GetNotifications(int kullaniciID, int pageIndex, int pageSize);
        bool MarkAsRead(int kullaniciID, int notificationId);
        bool MarkAllAsRead(int kullaniciID);
        bool DeleteNotification(int kullaniciID, int notificationId);
        int GetUnreadCount(int kullaniciID);

        // Diğer modüllerin (Talep, Ticket, Zimmet vb.) bildirim üretmesi için.
        // referans: WebServiceBildirim.AddNotification
        void AddNotification(string sicilNo, string baslik, string aciklama, string linkUrl, string kategori, string referansId, int? actionUserId = null);
    }
}
