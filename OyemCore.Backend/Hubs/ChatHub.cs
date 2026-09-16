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

        // roomUrl -> kabul edildi işareti. Aynı hesabın birden fazla bağlantısı (web'de birden fazla
        // sekme/pencere, ya da web+mobil) neredeyse aynı anda "kabul et" derse, hepsi sunucudan onay
        // beklemeden yerel olarak Daily odasına girmeye kalkışıyor ve tutarsız/takılı kalan durumlar
        // oluşuyordu (bkz. kullanıcı raporu: bir pencere "bağlanıyor"da takılı kalıyor, diğeri "arama
        // sonlandı" görüyor). Bu yüzden AcceptCall artık bool dönüyor — SADECE İLK çağrı true alır,
        // kaybedenler hiç Daily odasına girmemeli.
        private static readonly ConcurrentDictionary<string, byte> AcceptedRooms = new ConcurrentDictionary<string, byte>();

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
                    },
                    channelId: "incoming_call_v2");
            }
            catch { }

            // Android'de uygulama tamamen kapalıyken de native tam ekran arama arayüzünü açabilmek
            // için ayrıca data-only FCM mesajı (Expo push'un YANINDA, onun yerine değil).
            try
            {
                _ = _push.SendCallWakeAsync(cleanTarget, cleanSender, gonderenAdSoyad, roomUrl, callType, gonderenResim);
            }
            catch { }

            // Hedefin tüm bağlantılarına gelen arama bildirimi ilet.
            foreach (var connId in ConnectionsFor(cleanTarget))
                await Clients.Client(connId).SendAsync("incomingCall", cleanSender, gonderenAdSoyad, roomUrl, callType, gonderenResim);
        }

        // Aramayı kabul et: arayanın bağlantılarına callAccepted ilet.
        // Aynı kullanıcının AYNI aramayı gösteren DİĞER bağlantılarına (ör. diğer PC/tarayıcı sekmeleri,
        // aynı hesapla açık başka oturumlar) "callAnsweredElsewhere" gönderilir ki zil ekranını kapatıp
        // aynı Daily odasına ikinci bir katılımcı olarak girmeye çalışmasınlar (çoklu-oturum bağlantı
        // kilitlenmesi kök nedeni).
        // Dönüş değeri true/false: istemci SADECE true dönerse Daily odasına girmeli — aksi halde
        // (neredeyse eşzamanlı ikinci bir kabul denemesiyse) false döner ve istemci hiç katılmamalı.
        // referans: ChatHub.AcceptCall
        public async Task<bool> AcceptCall(string callerSicilNo, string roomUrl)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(callerSicilNo)) return false;

            // Aynı arama (roomUrl) için sadece İLK AcceptCall kazanır.
            if (!string.IsNullOrEmpty(roomUrl) && !AcceptedRooms.TryAdd(roomUrl, 0))
            {
                await Clients.Client(Context.ConnectionId).SendAsync("callAnsweredElsewhere", callerSicilNo.Trim());
                return false;
            }

            string cleanSender = senderSicilNo.Trim();
            foreach (var connId in ConnectionsFor(callerSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callAccepted", cleanSender, roomUrl);

            foreach (var connId in ConnectionsFor(cleanSender))
            {
                if (connId == Context.ConnectionId) continue;
                await Clients.Client(connId).SendAsync("callAnsweredElsewhere", callerSicilNo.Trim());
            }

            return true;
        }

        // Aramayı reddet: arayanın bağlantılarına callRejected ilet.
        // Aynı kullanıcının diğer bağlantılarına da zili kapatmaları için haber verilir (yukarıdaki
        // AcceptCall'daki gerekçeyle aynı).
        // referans: ChatHub.RejectCall
        public async Task RejectCall(string callerSicilNo, string reason)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(callerSicilNo)) return;

            string cleanSender = senderSicilNo.Trim();
            foreach (var connId in ConnectionsFor(callerSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callRejected", cleanSender, reason);

            foreach (var connId in ConnectionsFor(cleanSender))
            {
                if (connId == Context.ConnectionId) continue;
                await Clients.Client(connId).SendAsync("callAnsweredElsewhere", callerSicilNo.Trim());
            }
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

            if (!string.IsNullOrEmpty(roomUrl)) AcceptedRooms.TryRemove(roomUrl, out _);
        }

        // İstemcilerin (Web/Mobil) SignalR üzerinden anlık mesaj gönderebilmesini sağlar.
        public async Task SendMessage(string aliciSicilNo, string mesajMetni, object fileData, int? parentID, string parentMesajMetni, string parentGonderenAd, int? dbMessageID)
        {
            string senderSicilNo = GetSicilNo();
            if (string.IsNullOrEmpty(senderSicilNo) || string.IsNullOrEmpty(aliciSicilNo)) return;

            string cleanSender = senderSicilNo.Trim();
            string cleanReceiver = aliciSicilNo.Trim();

            string senderName = _db.tb_Kullanici
                .Where(u => u.SicilNo == cleanSender)
                .Select(u => u.AdSoyad)
                .FirstOrDefault() ?? "Kullanıcı";

            string timeStr = DateTime.Now.ToString("HH:mm");

            // Mesajı hem alıcının hem de gönderenin tüm bağlantılarına 10 parametreli eski web portal formatında ilet
            var targets = new List<string> { cleanReceiver, cleanSender };
            foreach (var user in targets)
            {
                foreach (var connId in ConnectionsFor(user))
                {
                    await Clients.Client(connId).SendAsync("receiveMessage", 
                        cleanSender, 
                        cleanReceiver, 
                        mesajMetni, 
                        fileData, 
                        timeStr, 
                        senderName, 
                        parentID, 
                        parentMesajMetni, 
                        parentGonderenAd, 
                        dbMessageID);
                }
            }
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
