using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IPushNotificationService
    {
        Task SendToUserBySicilNoAsync(string sicilNo, string title, string body, object data = null);
        Task SendToUserByKullaniciIdAsync(int kullaniciId, string title, string body, object data = null);

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

        // Ticketing (Ticket)
        Task NotifyNewTicketAsync(int ticketId);
        Task NotifyTicketSorumluAtandiAsync(int ticketId);
        Task NotifyTicketStatusChangedAsync(int ticketId, string oldStatus, string newStatus, int actionUserId);
        Task NotifyTicketGelismeAsync(int ticketId, int actionUserId, string comment);
    }
}

