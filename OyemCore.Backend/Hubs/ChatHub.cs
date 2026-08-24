using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Hubs
{
    // Gerçek-zamanlı chat & bildirim hub'ı. referans: webportal ChatHub (ASP.NET SignalR),
    // .NET 7 ASP.NET Core SignalR'a uyarlandı. Bağlantı sicilNo query string ile eşlenir:
    //   /hubs/chat?sicilNo=SCL0001-00
    // Mesaj dağıtımı (persist + relay) sunucu tarafında ChatService üzerinden IHubContext ile yapılır.
    // Görüntülü görüşme (Daily.co) sinyalleşmesi bu hub üzerinden yapılır:
    //   StartCall / AcceptCall / RejectCall / EndCall  →  incomingCall / callAccepted / callRejected / callEnded
    public class ChatHub : Hub
    {
        // SicilNo -> aktif ConnectionId kümesi (büyük/küçük harf duyarsız).
        public static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> Users =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);

        private readonly IDailyCallService _daily;
        private readonly IPushNotificationService _push;
        private readonly IYbsDbContext _db;

        public ChatHub(IDailyCallService daily, IPushNotificationService push, IYbsDbContext db)
        {
            _daily = daily;
            _push = push;
            _db = db;
        }

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

        // ── Görüntülü/sesli arama sinyalleşmesi (Daily.co) ──

        // Arama başlat: Daily odası aç, hedefe incomingCall gönder + çevrimdışıysa push.
        // referans: ChatHub.StartCall
        public async Task StartCall(string targetSicilNo, string callType)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(targetSicilNo)) return;

            string cleanSender = senderSicilNo.Trim();
            string cleanTarget = targetSicilNo.Trim();

            string gonderenAdSoyad = _db.tb_Kullanici
                .Where(u => u.SicilNo == cleanSender).Select(u => u.AdSoyad).FirstOrDefault() ?? "Kullanıcı";

            // Profil resimleri disk üzerinde SicilNo.jpg olarak saklanır.
            string gonderenResim = cleanSender + ".jpg";

            string roomUrl;
            try
            {
                roomUrl = await _daily.CreateRoomAsync();
            }
            catch (Exception ex)
            {
                // Arayan tarafa hata bildir, aramayı iptal et.
                foreach (var connId in ConnectionsFor(cleanSender))
                    await Clients.Client(connId).SendAsync("receiveNotification", new { description = "Arama Başlatılamadı: " + ex.Message });
                return;
            }

            // Hedefe mobil push bildirimi (uygulama kapalı/arka planda olsa da arama gelsin).
            try
            {
                _ = _push.SendToUserBySicilNoAsync(cleanTarget, gonderenAdSoyad, "📞 Görüntülü Arama Geliyor...",
                    new { 
                        screen = "IncomingCall", 
                        type = "call", 
                        callerSicilNo = cleanSender, 
                        callerName = gonderenAdSoyad, 
                        isGroup = false, 
                        gonderenSicilNo = cleanSender, 
                        roomUrl, 
                        callType 
                    });
            }
            catch { }

            // Hedefin tüm bağlantılarına gelen arama bildirimi ilet.
            foreach (var connId in ConnectionsFor(cleanTarget))
                await Clients.Client(connId).SendAsync("incomingCall", cleanSender, gonderenAdSoyad, roomUrl, callType, gonderenResim);
        }

        // Aramayı kabul et: arayanın bağlantılarına callAccepted ilet.
        // referans: ChatHub.AcceptCall
        public async Task AcceptCall(string callerSicilNo, string roomUrl)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(callerSicilNo)) return;

            string cleanSender = senderSicilNo.Trim();
            foreach (var connId in ConnectionsFor(callerSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callAccepted", cleanSender, roomUrl);
        }

        // Aramayı reddet: arayanın bağlantılarına callRejected ilet.
        // referans: ChatHub.RejectCall
        public async Task RejectCall(string callerSicilNo, string reason)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(callerSicilNo)) return;

            string cleanSender = senderSicilNo.Trim();
            foreach (var connId in ConnectionsFor(callerSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callRejected", cleanSender, reason);
        }

        // Aramayı sonlandır: karşı tarafın bağlantılarına callEnded ilet.
        // referans: ChatHub.EndCall
        public async Task EndCall(string targetSicilNo, string roomUrl)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(targetSicilNo)) return;

            string cleanSender = senderSicilNo.Trim();
            foreach (var connId in ConnectionsFor(targetSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callEnded", cleanSender, roomUrl);
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
