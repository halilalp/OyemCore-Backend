using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace OyemCore.Backend.Hubs
{
    // Gerçek-zamanlı chat & bildirim hub'ı. referans: webportal ChatHub (ASP.NET SignalR),
    // .NET 7 ASP.NET Core SignalR'a uyarlandı. Bağlantı sicilNo query string ile eşlenir:
    //   /hubs/chat?sicilNo=SCL0001-00
    // Mesaj dağıtımı (persist + relay) sunucu tarafında ChatService üzerinden IHubContext ile yapılır.
    public class ChatHub : Hub
    {
        // SicilNo -> aktif ConnectionId kümesi (büyük/küçük harf duyarsız).
        public static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> Users =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);

        private string GetSicilNo()
        {
            var q = Context.GetHttpContext()?.Request.Query["sicilNo"].ToString();
            return string.IsNullOrEmpty(q) ? null : q.Trim();
        }

        public override async Task OnConnectedAsync()
        {
            string sicilNo = GetSicilNo();
            if (!string.IsNullOrEmpty(sicilNo))
            {
                var connections = Users.GetOrAdd(sicilNo, _ => new ConcurrentDictionary<string, byte>());
                bool isFirst = connections.IsEmpty;
                connections[Context.ConnectionId] = 0;
                if (isFirst)
                    await Clients.All.SendAsync("userStatusChanged", sicilNo, true);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string sicilNo = GetSicilNo();
            if (!string.IsNullOrEmpty(sicilNo) && Users.TryGetValue(sicilNo, out var connections))
            {
                connections.TryRemove(Context.ConnectionId, out _);
                if (connections.IsEmpty)
                {
                    Users.TryRemove(sicilNo, out _);
                    await Clients.All.SendAsync("userStatusChanged", sicilNo, false);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Bir sicile ait tüm bağlantı id'leri (dağıtım için).
        public static List<string> ConnectionsFor(string sicilNo)
        {
            if (!string.IsNullOrEmpty(sicilNo) && Users.TryGetValue(sicilNo.Trim(), out var conns))
                return conns.Keys.ToList();
            return new List<string>();
        }

        public static bool IsOnline(string sicilNo)
        {
            return !string.IsNullOrEmpty(sicilNo) && Users.ContainsKey(sicilNo.Trim());
        }
    }
}
