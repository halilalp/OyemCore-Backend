using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Hubs
{
    // IChatRealtimeDispatcher implementasyonu — ChatHub bağlantılarına IHubContext ile gönderir.
    public class ChatRealtimeDispatcher : IChatRealtimeDispatcher
    {
        private readonly IHubContext<ChatHub> _hub;
        private readonly ILogger<ChatRealtimeDispatcher> _logger;
        private readonly ITenantService _tenantService;

        public ChatRealtimeDispatcher(IHubContext<ChatHub> hub, ILogger<ChatRealtimeDispatcher> logger, ITenantService tenantService)
        {
            _hub = hub;
            _logger = logger;
            _tenantService = tenantService;
        }

        public void SendToSicils(IEnumerable<string> sicils, string method, params object[] args)
        {
            if (sicils == null) return;
            string tenantId = _tenantService.GetCurrentTenantId();
            foreach (var sicil in sicils)
            {
                foreach (var connId in ChatHub.ConnectionsFor(tenantId, sicil))
                {
                    // SendAsync bilerek await edilmiyor (bu metod void — arayanları bloklamasın),
                    // ama önceden Task'ı hiç gözlemlemiyorduk: bağlantı kopmuş/geçersiz olduğunda
                    // atılan hata sessizce yutuluyor, hiçbir iz bırakmıyordu (ör. "messagesRead"
                    // canlı bildirimi bazen ulaşmıyor şikayeti — kesin sebep buysa artık loglanacak).
                    var task = _hub.Clients.Client(connId).SendAsync(method, args);
                    task.ContinueWith(t =>
                    {
                        _logger.LogWarning(t.Exception, "ChatRealtimeDispatcher: SendAsync basarisiz. Method: {Method}, Sicil: {Sicil}, ConnId: {ConnId}", method, sicil, connId);
                    }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
                }
            }
        }

        public bool IsOnline(string sicilNo) => ChatHub.IsOnline(_tenantService.GetCurrentTenantId(), sicilNo);
    }
}
