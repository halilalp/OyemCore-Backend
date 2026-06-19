using System.Threading.Tasks;

namespace WebPortalSpace.BusinessLayer.Interfaces
{
    public interface IPushNotificationService
    {
        Task SendToUserBySicilNoAsync(string sicilNo, string title, string body, object data = null);
        Task SendToUserByKullaniciIdAsync(int kullaniciId, string title, string body, object data = null);

        // Leave Requests (İzin Talep)
        Task NotifyNewLeaveRequestAsync(int leaveRequestId);
        Task NotifyLeaveManagerApprovalsCompletedAsync(int leaveRequestId);
        Task NotifyLeaveRequestRejectedAsync(int leaveRequestId, int actionUserId);
        Task NotifyLeaveRequestCompletedAsync(int leaveRequestId, int actionUserId);

        // IT, ERP, Maintenance Requests (Talepler)
        Task NotifyNewTalepAsync(int talepId);
        Task NotifyTalepSorumluAtandiAsync(int talepId);
        Task NotifyTalepGelismeAsync(int talepId, int actionUserId, string description);
        Task NotifyTalepClosedAsync(int talepId);

        // Asset/Zimmet Operations
        Task NotifyAssetAssignedAsync(int aygitPersonelId);
        Task NotifyAssetReturnedAsync(int aygitPersonelId, int actionUserId);

        // Ticketing (Ticket)
        Task NotifyNewTicketAsync(int ticketId);
        Task NotifyTicketSorumluAtandiAsync(int ticketId);
        Task NotifyTicketStatusChangedAsync(int ticketId, string oldStatus, string newStatus);
        Task NotifyTicketGelismeAsync(int ticketId, int actionUserId, string comment);
    }
}

