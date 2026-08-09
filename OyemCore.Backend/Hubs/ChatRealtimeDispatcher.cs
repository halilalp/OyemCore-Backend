using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Hubs
{
    // IChatRealtimeDispatcher implementasyonu — ChatHub bağlantılarına IHubContext ile gönderir.
    public class ChatRealtimeDispatcher : IChatRealtimeDispatcher
    {
        private readonly IHubContext<ChatHub> _hub;

        public ChatRealtimeDispatcher(IHubContext<ChatHub> hub)
        {
            _hub = hub;
        }

        public void SendToSicils(IEnumerable<string> sicils, string method, params object[] args)
        {
            if (sicils == null) return;
            foreach (var sicil in sicils)
            {
                foreach (var connId in ChatHub.ConnectionsFor(sicil))
                {
                    _hub.Clients.Client(connId).SendAsync(method, args);
                }
            }
        }

        public bool IsOnline(string sicilNo) => ChatHub.IsOnline(sicilNo);
    }
}
