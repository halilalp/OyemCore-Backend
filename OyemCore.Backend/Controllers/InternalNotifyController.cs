using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Interfaces;
using OyemCore.Backend.Hubs;

namespace OyemCore.Backend.Controllers
{
    // Sunucudan sunucuya (server-to-server) çağrılar için — eski web uygulamasının (YBSSolution1)
    // ticket/talep oluşturma noktalarından mobil push bildirimi tetiklemesi amacıyla eklendi.
    // Kullanıcı oturumu gerektirmez; X-Internal-Api-Key header'ı ile korunur.
    // Modül başına tek action: içerideki EventType alanına göre ilgili PushNotificationService
    // metoduna yönlendirilir — HTTP yüzeyini gereksiz yere büyütmemek için.
    [Route("api/internal/notify")]
    [ApiController]
    [AllowAnonymous]
    public class InternalNotifyController : ControllerBase
    {
        private readonly IPushNotificationService _pushService;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ChatHub> _chatHub;

        public InternalNotifyController(IPushNotificationService pushService, IConfiguration configuration, IHubContext<ChatHub> chatHub)
        {
            _pushService = pushService;
            _configuration = configuration;
            _chatHub = chatHub;
        }

        // TEŞHİS: DB/loglama hiç devreye girmeden, sadece deploy edilen kodun GERÇEKTEN güncel
        // olup olmadığını doğrulamak için — oturum/yetki gerektirmez, hiçbir DB'ye dokunmaz.
        [HttpGet("version")]
        [AllowAnonymous]
        public IActionResult Version()
        {
            return Ok(new { version = "BUILD-2026-09-15-AKADEMI-FAZ2", serverTimeUtc = DateTime.UtcNow });
        }

        // TEŞHİS: tb_Log/EF Core/arka plan görevi gibi ARA KATMANLARA hiç güvenmeden — token bulma
        // ve Expo'ya gönderme işini burada DOĞRUDAN yapıp SONUCU HTTP CEVABINDA döndürür. Hiçbir
        // şey loglamaya bağımlı değil, her adım response'ta görünür.
        [HttpGet("chat-debug")]
        [AllowAnonymous]
        public async Task<IActionResult> ChatDebug([FromQuery] string sicilNo, [FromServices] IServiceProvider serviceProvider)
        {
            var result = new System.Collections.Generic.Dictionary<string, object>();
            result["sicilNo"] = sicilNo;
            try
            {
                var db = serviceProvider.GetService(typeof(IYbsDbContext)) as IYbsDbContext;
                if (db == null)
                {
                    result["sonuc"] = "IYbsDbContext COZULEMEDI (DI hatasi)";
                    return Ok(result);
                }

                string dbConnStr = db.Database.GetConnectionString();
                result["dbServer"] = dbConnStr?.Split(';').FirstOrDefault(s => s.Trim().StartsWith("Data Source", StringComparison.OrdinalIgnoreCase)) ?? "(bulunamadi)";

                var devicesTokens = await db.tb_UserDevices.AsNoTracking().Where(d => d.SicilNo == sicilNo).Select(d => d.PushToken).ToListAsync();
                result["tb_UserDevices_tokenSayisi"] = devicesTokens.Count;
                result["tb_UserDevices_tokenlar"] = devicesTokens;

                var user = await db.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.SicilNo == sicilNo);
                result["tb_Kullanici_bulundu"] = user != null;
                result["tb_Kullanici_pushToken"] = user?.PushToken;

                string token = devicesTokens.FirstOrDefault() ?? user?.PushToken;
                result["kullanilacakToken"] = token;

                if (string.IsNullOrEmpty(token))
                {
                    result["sonuc"] = "TOKEN BULUNAMADI";
                    return Ok(result);
                }

                var payload = new { to = token, title = "ChatDebug Test", body = "Dogrudan diagnostik test", sound = "default" };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var httpClientFactory = serviceProvider.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
                var client = httpClientFactory != null ? httpClientFactory.CreateClient() : new HttpClient();
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://exp.host/--/api/v2/push/send", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                result["expoHttpStatus"] = (int)response.StatusCode;
                result["expoResponseBody"] = responseBody;
                result["sonuc"] = "TAMAMLANDI";
            }
            catch (Exception ex)
            {
                result["exception"] = ex.ToString();
                result["sonuc"] = "HATA";
            }
            return Ok(result);
        }

        private bool IsAuthorized()
        {
            if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided))
            {
                return false;
            }

            var expected = _configuration["Internal:ApiKey"];
            return !string.IsNullOrEmpty(expected) && provided.ToString() == expected;
        }

        [HttpPost("ticket")]
        public async Task<IActionResult> Ticket([FromBody] TicketNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewTicketAsync(dto.TicketId);
                    break;
                case "sorumluatandi":
                    await _pushService.NotifyTicketSorumluAtandiAsync(dto.TicketId, dto.ActionUserId);
                    break;
                case "statuschanged":
                    await _pushService.NotifyTicketStatusChangedAsync(dto.TicketId, dto.OldStatus, dto.NewStatus, dto.ActionUserId);
                    break;
                case "gelisme":
                    await _pushService.NotifyTicketGelismeAsync(dto.TicketId, dto.ActionUserId, dto.Comment);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("talep")]
        public async Task<IActionResult> Talep([FromBody] TalepNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewTalepAsync(dto.TalepId);
                    break;
                case "sorumluatandi":
                    await _pushService.NotifyTalepSorumluAtandiAsync(dto.TalepId);
                    break;
                case "gelisme":
                    await _pushService.NotifyTalepGelismeAsync(dto.TalepId, dto.ActionUserId, dto.Description);
                    break;
                case "closed":
                    await _pushService.NotifyTalepClosedAsync(dto.TalepId);
                    break;
                case "onayagonderildi":
                    await _pushService.NotifyTalepOnayaGonderildiAsync(dto.TalepId, dto.OnayciSicil);
                    break;
                case "onaylandi":
                    await _pushService.NotifyTalepOnaylandiAsync(dto.TalepId, dto.ActionUserId);
                    break;
                case "reddedildi":
                    await _pushService.NotifyTalepReddedildiAsync(dto.TalepId, dto.ActionUserId, dto.Description);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("tedarikci")]
        public async Task<IActionResult> Tedarikci([FromBody] TedarikciNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewTedarikciDegerlendirmeAsync(dto.BelgeNo);
                    break;
                case "completed":
                    await _pushService.NotifyTedarikciDegerlendirmeCompletedAsync(dto.BelgeNo);
                    break;
                case "cancelled":
                    await _pushService.NotifyTedarikciDegerlendirmeCancelledAsync(dto.BelgeNo);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("izin")]
        public async Task<IActionResult> Izin([FromBody] IzinNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewLeaveRequestAsync(dto.IzinOnayId);
                    break;
                case "amironaylaricompleted":
                    await _pushService.NotifyLeaveManagerApprovalsCompletedAsync(dto.IzinOnayId);
                    break;
                case "rejected":
                    await _pushService.NotifyLeaveRequestRejectedAsync(dto.IzinOnayId, dto.ActionUserId);
                    break;
                case "completed":
                    await _pushService.NotifyLeaveRequestCompletedAsync(dto.IzinOnayId, dto.ActionUserId);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            await _pushService.SendToUserBySicilNoAsync(
                dto.AliciSicilNo,
                dto.Baslik,
                dto.MesajMetni,
                new { type = "chat", screen = "ChatDetailScreen", sicilNo = dto.GonderenSicilNo, groupCode = dto.GroupCode }
            );

            return Ok(new { success = true });
        }

        // WebPortal'da (WebServiceAkademi.cs > AkademiAta) yeni bir Akademi eğitimi atandığında
        // mobile push bildirimi göndermek için. tb_Egitim/WebServiceEgitim'in "chat" gibi ayrı bir
        // amacı yok, bu yüzden basit passthrough yeterli.
        [HttpPost("akademi")]
        public async Task<IActionResult> Akademi([FromBody] AkademiNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null || string.IsNullOrEmpty(dto.SicilNo)) return BadRequest();

            await _pushService.SendToUserBySicilNoAsync(
                dto.SicilNo,
                dto.Baslik ?? "Yeni Akademi Eğitimi",
                dto.Mesaj ?? "",
                new { type = "akademi", screen = "AkademiDetay", atamaID = dto.AtamaID }
            );

            return Ok(new { success = true });
        }

        // WebPortal'da (WebServiceAkademi.cs > AkademiSinavTekrarTalepEt) bir personel ek sınav
        // hakkı talep ettiğinde AKADEMI admin belge türüne sahip yöneticilere push göndermek için.
        // "screen" KASITLI verilmiyor: onay/red işlemi sadece WebPortal'da yapılıyor, mobilde
        // karşılık gelen bir ekran yok — navigateFromNotificationData zaten screen yoksa sessizce
        // hiçbir yere yönlendirmiyor.
        [HttpPost("akademi-sinav-talep")]
        public async Task<IActionResult> AkademiSinavTalep([FromBody] AkademiSinavTalepNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null || string.IsNullOrEmpty(dto.SicilNo)) return BadRequest();

            await _pushService.SendToUserBySicilNoAsync(
                dto.SicilNo,
                dto.Baslik ?? "Ek Sınav Hakkı Talebi",
                dto.Mesaj ?? "",
                new { type = "akademiSinavTalep" }
            );

            return Ok(new { success = true });
        }

        // WebPortal'ın kendi (eski) ChatHub'ından başlattığı görüntülü/sesli aramayı, mobile doğru
        // şekilde (gerçek arama olarak) ulaştırmak için kullanılır — genel "chat" endpoint'i "sohbet
        // mesajı" şeklinde push gönderdiğinden ChatHub.StartCall'daki gerçek arama zilini tetiklemiyordu.
        [HttpPost("call")]
        public async Task<IActionResult> Call([FromBody] CallNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null || string.IsNullOrEmpty(dto.TargetSicilNo) || string.IsNullOrEmpty(dto.CallerSicilNo)) return BadRequest();

            string cleanTarget = dto.TargetSicilNo.Trim();
            string cleanCaller = dto.CallerSicilNo.Trim();

            await _pushService.SendToUserBySicilNoAsync(
                cleanTarget,
                dto.CallerName ?? "Kullanıcı",
                "📞 Görüntülü Arama Geliyor...",
                new {
                    screen = "IncomingCall",
                    type = "call",
                    callerSicilNo = cleanCaller,
                    callerName = dto.CallerName,
                    isGroup = false,
                    gonderenSicilNo = cleanCaller,
                    roomUrl = dto.RoomUrl,
                    callType = dto.CallType
                },
                channelId: "incoming_call_v2"
            );

            // Android'de uygulama tamamen kapalıyken de native tam ekran arama arayüzünü açabilmek
            // için ayrıca data-only FCM mesajı (Expo push'un YANINDA, onun yerine değil).
            try
            {
                await _pushService.SendCallWakeAsync(cleanTarget, cleanCaller, dto.CallerName, dto.RoomUrl, dto.CallType, cleanCaller + ".jpg");
            }
            catch { }

            // Mobil uygulama o an açık/bağlıysa push'u beklemeden anında zil çalsın diye kendi hub'ındaki
            // bağlantılara da ilet (bkz. ChatHub.StartCall aynı davranış).
            foreach (var connId in ChatHub.ConnectionsFor(cleanTarget))
                await _chatHub.Clients.Client(connId).SendAsync("incomingCall", cleanCaller, dto.CallerName, dto.RoomUrl, dto.CallType, cleanCaller + ".jpg");

            return Ok(new { success = true });
        }

        // Bir taraf konuşmayı okuduğunda karşı tarafın açık sekmelerine anlık "okundu" bildirimi iletir
        // (WebPortal/mobil sayfayı yenilemeden çift-tik güncellensin diye). Push bildirimi tetiklemez,
        // sadece o an bağlı SignalR bağlantılarına yayın yapar.
        [HttpPost("chatread")]
        public async Task<IActionResult> ChatRead([FromBody] ChatReadNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null || string.IsNullOrEmpty(dto.NotifyTargetSicilNo) || string.IsNullOrEmpty(dto.ReaderSicilNo)) return BadRequest();

            var conversationCode = string.IsNullOrEmpty(dto.ConversationCode) ? dto.ReaderSicilNo : dto.ConversationCode;

            foreach (var connId in ChatHub.ConnectionsFor(dto.NotifyTargetSicilNo))
            {
                await _chatHub.Clients.Client(connId).SendAsync("messagesRead", dto.ReaderSicilNo, conversationCode);
            }

            return Ok(new { success = true });
        }

        [HttpPost("zimmet")]
        public async Task<IActionResult> Zimmet([FromBody] ZimmetNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "zimmetlendi":
                    await _pushService.NotifyAssetAssignedAsync(dto.AygitPersonelId);
                    break;
                case "teslimedildi":
                    await _pushService.NotifyAssetReturnedAsync(dto.AygitPersonelId, dto.ActionUserId);
                    break;
                case "silindi":
                    await _pushService.NotifyAssetRemovedAsync(dto.PersonelSicil, dto.AygitId, dto.ActionUserAdSoyad);
                    break;
                case "hatabildirildi":
                    await _pushService.NotifyAssetFaultReportedAsync(dto.AdminSicilNo, dto.AygitId, dto.ReporterAdSoyad, dto.Description);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("temizlikonay")]
        public async Task<IActionResult> TemizlikOnay([FromBody] TemizlikOnayNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyTemizlikOnayCreatedAsync(dto.OnayId);
                    break;
                case "completed":
                    await _pushService.NotifyTemizlikOnayCompletedAsync(dto.OnayId);
                    break;
                case "rejected":
                    await _pushService.NotifyTemizlikOnayRejectedAsync(dto.OnayId);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }
    }

    // eventType: "created" | "completed" | "rejected"
    public class TemizlikOnayNotifyDto
    {
        public string EventType { get; set; }
        public int OnayId { get; set; }
    }

    // eventType: "zimmetlendi" | "teslimEdildi" | "silindi" | "hataBildirildi"
    // - zimmetlendi / teslimEdildi: AygitPersonelId zorunlu (teslimEdildi icin ayrica ActionUserId)
    // - silindi: PersonelSicil + AygitId + ActionUserAdSoyad zorunlu (atama kaydi zaten silinmis olabilir)
    // - hataBildirildi: AdminSicilNo + AygitId + ReporterAdSoyad + Description zorunlu
    public class ZimmetNotifyDto
    {
        public string EventType { get; set; }
        public int AygitPersonelId { get; set; }
        public int AygitId { get; set; }
        public int ActionUserId { get; set; }
        public string PersonelSicil { get; set; }
        public string ActionUserAdSoyad { get; set; }
        public string AdminSicilNo { get; set; }
        public string ReporterAdSoyad { get; set; }
        public string Description { get; set; }
    }

    // eventType: "created" | "amirOnaylariCompleted" | "rejected" | "completed"
    public class IzinNotifyDto
    {
        public string EventType { get; set; }
        public int IzinOnayId { get; set; }
        public int ActionUserId { get; set; }
    }

    // eventType: "created" | "sorumluAtandi" | "statusChanged" | "gelisme"
    public class TicketNotifyDto
    {
        public string EventType { get; set; }
        public int TicketId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public int ActionUserId { get; set; }
        public string Comment { get; set; }
    }

    // eventType: "created" | "sorumluAtandi" | "gelisme" | "closed" | "onayaGonderildi" | "onaylandi" | "reddedildi"
    // - onayaGonderildi: OnayciSicil doldurulmalı (onayı verecek kişi)
    // - reddedildi: Description alanı red sebebi olarak kullanılır
    public class TalepNotifyDto
    {
        public string EventType { get; set; }
        public int TalepId { get; set; }
        public int ActionUserId { get; set; }
        public string Description { get; set; }
        public string OnayciSicil { get; set; }
    }

    // eventType: "created" | "completed" | "cancelled"
    public class TedarikciNotifyDto
    {
        public string EventType { get; set; }
        public string BelgeNo { get; set; }
    }

    public class ChatNotifyDto
    {
        public string GonderenSicilNo { get; set; }
        public string AliciSicilNo { get; set; }
        public string Baslik { get; set; }
        public string MesajMetni { get; set; }
        public string GroupCode { get; set; }
    }

    public class AkademiNotifyDto
    {
        public string SicilNo { get; set; }
        public string Baslik { get; set; }
        public string Mesaj { get; set; }
        public int AtamaID { get; set; }
    }

    public class AkademiSinavTalepNotifyDto
    {
        public string SicilNo { get; set; }
        public string Baslik { get; set; }
        public string Mesaj { get; set; }
    }

    public class CallNotifyDto
    {
        public string CallerSicilNo { get; set; }
        public string TargetSicilNo { get; set; }
        public string CallerName { get; set; }
        public string RoomUrl { get; set; }
        public string CallType { get; set; }
    }

    // NotifyTargetSicilNo: okunduğu haber verilecek kişi (mesajı gönderen taraf).
    // ReaderSicilNo: konuşmayı okuyan taraf. ConversationCode: grup ise grup kodu, değilse boş bırakılabilir
    // (bu durumda istemci tarafında ReaderSicilNo konuşma anahtarı olarak kullanılır).
    public class ChatReadNotifyDto
    {
        public string NotifyTargetSicilNo { get; set; }
        public string ReaderSicilNo { get; set; }
        public string ConversationCode { get; set; }
    }
}
