using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        // "tenantId|SicilNo" -> aktif ConnectionId kümesi (büyük/küçük harf duyarsız).
        // KOK NEDEN (2026-09-23): Bu sözlük eskiden SADECE SicilNo ile tutuluyordu — api.oyemsoft.com
        // host'unu oyemsoft/adore/ashley AYNI ANDA paylaştığından, iki farklı şirkette AYNI SicilNo
        // varsa (ör. ikisinde de "SCL0001-00") aramalar/mesajlar birbirine KARIŞIYORDU (canlıda
        // doğrulandı). Artık her anahtar tenant ile nitelenir — bkz. TenantKey().
        public static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> Users =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);

        private static string TenantKey(string tenantId, string sicilNo)
            => (tenantId ?? "").Trim().ToUpperInvariant() + "|" + (sicilNo ?? "").Trim();

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
        private readonly ITenantService _tenant;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        public ChatHub(IDailyCallService daily, IPushNotificationService push, IYbsDbContext db,
            ITenantService tenant, IConfiguration configuration, IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory)
        {
            _daily = daily;
            _push = push;
            _db = db;
            _tenant = tenant;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
        }

        // Mobilden başlatılan bir arama, hedef kişinin WebPortal'da açık bir sekmesi varsa oraya
        // da anlık ulaşsın diye — WebPortal'ın kendi ChatHub'ı (ASP.NET classic SignalR) ile bu
        // hub TAMAMEN AYRI sunucular, bu yüzden HTTP köprüsü gerekiyor (WebPortal'ın mobile push
        // tetiklerken yaptığı çağrının ters yönü). Tenant'ın WebPortal'ı yoksa (GetWebPortalBaseUrl
        // null) veya çağrı başarısız olursa sessizce atlanır — mobil push zaten kendi yolundan gider.
        private async Task RelayToWebPortalAsync(string targetSicilNo, string callerSicilNo, string callerName, string roomUrl, string callType)
        {
            string webPortalUrl = _tenant.GetWebPortalBaseUrl();
            if (string.IsNullOrEmpty(webPortalUrl)) return;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                string url = webPortalUrl.TrimEnd('/') + "/Chat/WebServiceChat.asmx/RelayIncomingCall";
                var formData = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["targetSicilNo"] = targetSicilNo,
                    ["callerSicilNo"] = callerSicilNo,
                    ["callerName"] = callerName ?? "",
                    ["roomUrl"] = roomUrl ?? "",
                    ["callType"] = callType ?? "video",
                });
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = formData };
                request.Headers.Add("X-Internal-Api-Key", _configuration["Internal:ApiKey"]);
                await client.SendAsync(request);
            }
            catch
            {
                // Sessizce atla — WebPortal'a ulaşılamaması mobil-mobil/mobil-push akışını etkilememeli.
            }
        }

        private string GetSicilNo()
        {
            var q = Context.GetHttpContext()?.Request.Query["sicilNo"].ToString();
            return string.IsNullOrEmpty(q) ? null : q.Trim();
        }

        // KOK NEDEN (2026-09-24): ITenantService.GetCurrentTenantId() -> GetCurrentTenant(),
        // IHttpContextAccessor.HttpContext'e bakiyor — bu, SignalR hub METOD cagrilarinda (baglanti
        // KURULDUKTAN SONRAKI StartCall/AcceptCall/... invoke'lari) GUVENILIR DOLU DEGIL (SignalR'in
        // kendi dispatch'i standart middleware pipeline'indan gecmiyor, IHttpContextAccessor'i her
        // invoke'ta yeniden doldurmuyor) — bu yuzden tum baglantilar sessizce "bos tenant" anahtarina
        // (TenantKey(null,...)) dusup GENE birbirine karisiyordu (isiktarim WebPortal<->WebPortal bile
        // tetiklenmedi). GetSicilNo() ile AYNI, kanitlanmis guvenilir yol kullanilir: Context.GetHttpContext()
        // (Hub'in KENDI Context'i, IHttpContextAccessor DEGIL) — bu her invoke'ta dogru dolu.
        private string GetTenantId()
        {
            var q = Context.GetHttpContext()?.Request.Query["tenantId"].ToString();
            return string.IsNullOrEmpty(q) ? null : q.Trim();
        }

        // Bu bağlantının kendi tenant'ı içinde bir SicilNo'nun bağlantılarını bulur — bkz. TenantKey/
        // static ConnectionsFor(tenantId, sicilNo) üzerindeki kök neden açıklaması.
        private List<string> ConnectionsForCurrentTenant(string sicilNo)
            => ConnectionsFor(GetTenantId(), sicilNo);

        public override async Task OnConnectedAsync()
        {
            string sicilNo = GetSicilNo();
            if (!string.IsNullOrEmpty(sicilNo))
            {
                string key = TenantKey(GetTenantId(), sicilNo);
                var connections = Users.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>());
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
            string key = TenantKey(GetTenantId(), sicilNo);
            if (!string.IsNullOrEmpty(sicilNo) && Users.TryGetValue(key, out var connections))
            {
                connections.TryRemove(Context.ConnectionId, out _);
                if (connections.IsEmpty)
                {
                    Users.TryRemove(key, out _);
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
                .Where(u => u.SicilNo == cleanSender).Select(u => u.AdSoyad).FirstOrDefault() ?? "Arayan";

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
                foreach (var connId in ConnectionsForCurrentTenant(cleanSender))
                    await Clients.Client(connId).SendAsync("receiveNotification", new { description = "Arama Başlatılamadı: " + ex.Message });
                return;
            }

            // Hedefe mobil push bildirimi (uygulama kapalı/arka planda olsa da arama gelsin).
            // KARAR (2026-09-23): Ozel channelId="incoming_call_v2" + priority="high" + custom ses
            // kombinasyonu gercek cihazda (uygulama kapaliyken) HICBIR ZAMAN banner olarak gorunmuyordu
            // (uzun teshis: cihaz ayarlari dogru, Expo/APNs "ok" diyordu, kok neden kesinlesemedi).
            // Kullanici karariyla TUM DIGER bildirimlerle AYNI, kanitlanmis guvenilir normal push
            // yoluna alindi — ozel kanal/ses kaldirildi. Bildirime dokununca yine de uygulama acilip
            // tam ekran CallRingOverlay gosterilir (data.type=="call", triggerIncomingCallNotification).
            try
            {
                await _push.SendToUserBySicilNoAsync(cleanTarget, gonderenAdSoyad, "📞 Görüntülü Arama Geliyor...",
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
            foreach (var connId in ConnectionsForCurrentTenant(cleanTarget))
                await Clients.Client(connId).SendAsync("incomingCall", cleanSender, gonderenAdSoyad, roomUrl, callType, gonderenResim);

            // Hedefin WebPortal'da açık bir sekmesi varsa oraya da anlık ilet (bkz. RelayToWebPortalAsync).
            try
            {
                await RelayToWebPortalAsync(cleanTarget, cleanSender, gonderenAdSoyad, roomUrl, callType);
            }
            catch { }
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
            foreach (var connId in ConnectionsForCurrentTenant(callerSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callAccepted", cleanSender, roomUrl);

            foreach (var connId in ConnectionsForCurrentTenant(cleanSender))
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
            foreach (var connId in ConnectionsForCurrentTenant(callerSicilNo.Trim()))
                await Clients.Client(connId).SendAsync("callRejected", cleanSender, reason);

            foreach (var connId in ConnectionsForCurrentTenant(cleanSender))
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
            foreach (var connId in ConnectionsForCurrentTenant(targetSicilNo.Trim()))
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
                foreach (var connId in ConnectionsForCurrentTenant(user))
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

        // Bir tenant+sicile ait tüm bağlantı id'leri (dağıtım için). tenantId ZORUNLU — bkz. TenantKey
        // üzerindeki kök neden açıklaması (aksi halde başka bir şirketin aynı SicilNo'suna karışabilir).
        public static List<string> ConnectionsFor(string tenantId, string sicilNo)
        {
            if (!string.IsNullOrEmpty(sicilNo) && Users.TryGetValue(TenantKey(tenantId, sicilNo), out var conns))
                return conns.Keys.ToList();
            return new List<string>();
        }

        public static bool IsOnline(string tenantId, string sicilNo)
        {
            return !string.IsNullOrEmpty(sicilNo) && Users.ContainsKey(TenantKey(tenantId, sicilNo));
        }
    }
}
