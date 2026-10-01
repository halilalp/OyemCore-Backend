using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IPushNotificationService
    {
        // channelId: gecmiste Android bildirim kanali secimi icin kullanilirdi (ozel arama zil sesi/
        // titresim) — 2026-09-23'te arama push'u da diger bildirimlerle ayni normal yola alindiginda
        // kullanimdan kalkti. Parametre geriye donuk uyumluluk icin duruyor, artik hep null geciliyor.
        Task SendToUserBySicilNoAsync(string sicilNo, string title, string body, object data = null, string channelId = null);
        Task SendToUserByKullaniciIdAsync(int kullaniciId, string title, string body, object data = null, string channelId = null);

        // Android'de native tam ekran gelen arama arayüzünü uygulama kapalıyken de uyandırmak için
        // (tb_UserDevices'ta DeviceType="FcmVoip" olan cihazlara) data-only FCM mesajı gönderir.
        // Expo push'un (yukarıdaki iki metod) YANINDA, ek bir yol — onu değiştirmez.
        Task SendCallWakeAsync(string sicilNo, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage);

        // Leave Requests (Izin Talep)
        Task NotifyNewLeaveRequestAsync(int leaveRequestId);
        Task NotifyLeaveManagerApprovalsCompletedAsync(int leaveRequestId);
        Task NotifyLeaveRequestRejectedAsync(int leaveRequestId, int actionUserId);
        Task NotifyLeaveRequestCompletedAsync(int leaveRequestId, int actionUserId);

        // IT, ERP, Maintenance Requests (Talepler)
        Task NotifyNewTalepAsync(int talepId);
        Task NotifyTalepSorumluAtandiAsync(int talepId);
        Task NotifyTalepGelismeAsync(int talepId, int actionUserId, string description);
        Task NotifyTalepClosedAsync(int talepId);

        // Talep - İşlem Onay alt-süreci
        Task NotifyTalepOnayaGonderildiAsync(int talepId, string onayciSicil);
        Task NotifyTalepOnaylandiAsync(int talepId, int actionUserId);
        Task NotifyTalepReddedildiAsync(int talepId, int actionUserId, string sebep);

        // Tedarikçi Değerlendirme
        Task NotifyNewTedarikciDegerlendirmeAsync(string belgeNo);
        Task NotifyTedarikciDegerlendirmeCompletedAsync(string belgeNo);
        Task NotifyTedarikciDegerlendirmeCancelledAsync(string belgeNo);

        // Asset/Zimmet Operations
        Task NotifyAssetAssignedAsync(int aygitPersonelId);
        Task NotifyAssetReturnedAsync(int aygitPersonelId, int actionUserId);
        Task NotifyAssetRemovedAsync(string personelSicil, int aygitId, string actionUserAdSoyad);
        Task NotifyAssetFaultReportedAsync(string adminSicilNo, int aygitId, string reporterAdSoyad, string description);

        // Bakım Planı / Periyodik Kontrol Planı - Temizlik Onay Formu
        Task NotifyTemizlikOnayCreatedAsync(int onayId);
        Task NotifyTemizlikOnayCompletedAsync(int onayId);
        Task NotifyTemizlikOnayRejectedAsync(int onayId);

        // Ticketing (Ticket)
        Task NotifyNewTicketAsync(int ticketId);
        Task NotifyTicketSorumluAtandiAsync(int ticketId, int actionUserId = 0);
        Task NotifyTicketStatusChangedAsync(int ticketId, string oldStatus, string newStatus, int actionUserId);
        Task NotifyTicketGelismeAsync(int ticketId, int actionUserId, string comment);
        Task NotifyTicketDeletedAsync(string kayitSicil, string sorumluSicil, string takipKodu, string baslik, string silenAdSoyad, string silenSicil);
    }
}

