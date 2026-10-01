using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Common;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.BusinessLayer.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        // TEŞHİS: InternalNotifyController.Version()'dan da okunuyor — Controller (OyemCore.Backend.dll)
        // ve BusinessLayer (bu dosyanın derlendiği DLL) AYRI dosyalar olduğu için, deploy sırasında
        // biri güncellenip diğeri unutulabiliyor (2026-09-22'de tam bu yaşandı). Tek istekle ikisinin
        // de gerçekten güncel olup olmadığını ayırt edebilmek için.
        public const string BuildMarker = "BL-2026-09-24-CHATHUB-QUERYSTRING-TENANT";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly IFcmVoipPushService _fcmVoip;
        private readonly IApnsVoipPushService _apnsVoip;

        public PushNotificationService(IServiceScopeFactory scopeFactory, HttpClient httpClient, ILogger<PushNotificationService> logger, IFcmVoipPushService fcmVoip, IApnsVoipPushService apnsVoip)
        {
            _scopeFactory = scopeFactory;
            _httpClient = httpClient;
            _logger = logger;
            _fcmVoip = fcmVoip;
            _apnsVoip = apnsVoip;
        }

        public async Task SendCallWakeAsync(string sicilNo, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage)
        {
            if (string.IsNullOrEmpty(sicilNo)) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();

                    // KESIN KURAL: pasif kullaniciya push gitmemeli — gorusme arama-uyandirma push'u da dahil.
                    var aliciDurum = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.SicilNo == sicilNo)
                        .Select(u => (bool?)u.Durum)
                        .FirstOrDefaultAsync();
                    if (aliciDurum != true)
                    {
                        LogPush(context, "PUSH-NOT-SKIP-PASIF", $"SicilNo {sicilNo} pasif (veya bulunamadi) oldugu icin arama-uyandirma push'u gonderilmedi.");
                        return;
                    }

                    List<(string PushToken, string DeviceType)> devices;
                    try
                    {
                        devices = await context.tb_UserDevices
                            .AsNoTracking()
                            .Where(d => d.SicilNo == sicilNo &&
                                        (d.DeviceType == "FcmVoip" || d.DeviceType == "ApnsVoipProduction" || d.DeviceType == "ApnsVoipSandbox"))
                            .Select(d => new { d.PushToken, d.DeviceType })
                            .ToListAsync()
                            .ContinueWith(t => t.Result.Select(d => (d.PushToken, d.DeviceType)).ToList());
                    }
                    catch (Exception exDevices)
                    {
                        _logger.LogWarning(exDevices, "PushNotificationService.SendCallWakeAsync: tb_UserDevices sorgusu basarisiz. SicilNo {SicilNo}", sicilNo);
                        devices = new List<(string, string)>();
                    }

                    if (devices.Count == 0)
                    {
                        // "Görüntülü arama bildirimi kapalıyken hiç gelmiyor" teşhisinde en sık atlanan
                        // ihtimal budur: cihazin VoIP token'i hic kayitli degil (native taraf henuz
                        // kaydetmemis/eski build) — bu satir olmadan sessizce hicbir sey gonderilmiyordu.
                        LogPush(context, "CALL-PUSH-SKIP", $"SicilNo {sicilNo} icin FcmVoip/ApnsVoipProduction/ApnsVoipSandbox turunde kayitli cihaz yok.");
                    }

                    foreach (var device in devices)
                    {
                        if (string.IsNullOrEmpty(device.PushToken)) continue;

                        (bool Success, string Detail) result;
                        if (device.DeviceType == "FcmVoip")
                        {
                            result = await _fcmVoip.SendCallWakeAsync(device.PushToken, callerSicilNo, callerName, roomUrl, callType, callerImage);
                        }
                        else
                        {
                            bool isProduction = device.DeviceType == "ApnsVoipProduction";
                            result = await _apnsVoip.SendCallWakeAsync(device.PushToken, isProduction, callerSicilNo, callerName, roomUrl, callType, callerImage);
                        }

                        // VoIP push dusuk hacimli/kritik oldugundan basarili denemeler de loglaniyor
                        // (normal Expo push'taki "sadece hata" kuralindan farkli olarak) — gorusme
                        // bildirimi gelmedi sikayetinde gercekte gonderilip gonderilmedigini gormek icin.
                        LogPush(context, result.Success ? "CALL-PUSH-OK" : "CALL-PUSH-ERROR",
                            $"SicilNo: {sicilNo}, DeviceType: {device.DeviceType}, Arayan: {callerSicilNo}, Detay: {result.Detail}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendCallWakeAsync failed for SicilNo {SicilNo}", sicilNo);
                // KOK NEDEN (2026-09-22): bu dis catch SADECE ILogger'a yaziyordu (bu ortamda
                // gorunmez) — SendToUserBySicilNoAsync'in dis catch'inden farkli olarak tb_Log'a hic
                // dusmuyordu. WebPortal'dan (ChatHub.StartCall, SignalR hub cagrisi icinden) tetiklenen
                // aramalarda burada SESSIZCE patlayip hicbir iz birakmiyordu — ayni HTTP path'ten
                // (/call controller) cagrildiginda calistigi icin hic yakalanamamisti. Artik ayni
                // guvenlik agi (yeni scope + LogPush) burada da var.
                try
                {
                    using var errScope = _scopeFactory.CreateScope();
                    var errContext = errScope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    LogPush(errContext, "CALL-PUSH-ERROR", $"SendCallWakeAsync disaridaki catch'e dustu. SicilNo: {sicilNo}, Hata: {ex.GetType().FullName}: {ex.Message}, StackTrace: {ex.StackTrace}");
                }
                catch { }
            }
        }

        public async Task SendToUserBySicilNoAsync(string sicilNo, string title, string body, object data = null, string channelId = null)
        {
            if (string.IsNullOrEmpty(sicilNo)) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();

                    // KESIN KURAL: pasif (Durum != true) kullaniciya HICBIR sekilde push gitmemeli —
                    // token aramaya bile gerek yok, en basta kes. Merkezi burada yapiliyor ki her
                    // Notify* metodu (Talep, Ticket, Izin, Akademi, ...) tek tek kontrol etmek zorunda kalmasin.
                    var aliciDurum = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.SicilNo == sicilNo)
                        .Select(u => (bool?)u.Durum)
                        .FirstOrDefaultAsync();
                    if (aliciDurum != true)
                    {
                        LogPush(context, "PUSH-NOT-SKIP-PASIF", $"SicilNo {sicilNo} pasif (veya bulunamadi) oldugu icin push gonderilmedi. Baslik: {title}");
                        return;
                    }

                    // tb_UserDevices bazı tenant veritabanlarında (ör. IşıkTarım) henüz yok — bu tabloyu
                    // sorgulamak SQL hatası fırlatıp SENDİRME İŞLEMİNİN TAMAMINI sessizce iptal ediyordu
                    // (tb_Kullanici.PushToken yedek yoluna hiç sıra gelmeden). Artık bu sorgu KENDİ
                    // try/catch'inde — tablo yoksa/hata verirse boş liste kabul edilip yedek yola düşülür.
                    List<string> tokens;
                    try
                    {
                        // KOK NEDEN: tb_UserDevices, gorunutulu arama (VoIP wake) icin native/ham cihaz
                        // token'lari da (DeviceType: FcmVoip/ApnsVoipProduction/ApnsVoipSandbox) ayni
                        // tabloda tutuyor — bunlar Expo formatinda DEGIL, normal push icin kullanilamaz.
                        // Bu satirlar filtrelenmezse, kullanici VoIP'e kayit olur olmaz normal push'lari
                        // (chat/talep/vs.) SessizCE bozuluyordu (2026-09-22'de canli tespit edildi).
                        tokens = await context.tb_UserDevices
                            .AsNoTracking()
                            .Where(d => d.SicilNo == sicilNo
                                        && d.DeviceType != "FcmVoip"
                                        && d.DeviceType != "ApnsVoipProduction"
                                        && d.DeviceType != "ApnsVoipSandbox")
                            .Select(d => d.PushToken)
                            .ToListAsync();
                    }
                    catch (Exception exDevices)
                    {
                        _logger.LogWarning(exDevices, "PushNotificationService: tb_UserDevices sorgusu basarisiz (tablo eksik olabilir), tb_Kullanici.PushToken yedegine dusuluyor. SicilNo {SicilNo}", sicilNo);
                        LogPush(context, "PUSH-NOT-ERROR", $"tb_UserDevices sorgusu basarisiz. SicilNo: {sicilNo}, Hata: {exDevices.Message}");
                        tokens = new List<string>();
                    }

                    if (tokens.Count == 0)
                    {
                        var user = await context.tb_Kullanici
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.SicilNo == sicilNo);

                        if (user != null && !string.IsNullOrEmpty(user.PushToken))
                        {
                            tokens.Add(user.PushToken);
                        }
                    }

                    if (tokens.Count == 0)
                    {
                        // "Push kaydi var ama telefona gelmiyor" sikayetinin en sessiz nedeni budur:
                        // hedef kullanicinin ne tb_UserDevices'ta ne tb_Kullanici.PushToken'da KAYITLI
                        // hicbir token'i yoktu — gonderilecek hicbir sey yok, ama bu durum daha once
                        // hic loglanmiyordu.
                        LogPush(context, "PUSH-NOT-ERROR", $"SicilNo {sicilNo} icin kayitli hicbir push token yok (tb_UserDevices ve tb_Kullanici.PushToken ikisi de bos). Baslik: {title}");
                    }

                    foreach (var token in tokens)
                    {
                        if (!string.IsNullOrEmpty(token))
                        {
                            await SendExpoNotificationAsync(token, title, body, data, channelId, context, sicilNo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendToUserBySicilNoAsync failed for SicilNo {SicilNo}", sicilNo);
                try
                {
                    using var errScope = _scopeFactory.CreateScope();
                    var errContext = errScope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    LogPush(errContext, "PUSH-NOT-ERROR", $"SendToUserBySicilNoAsync disaridaki catch'e dustu. SicilNo: {sicilNo}, Hata: {ex.Message}, StackTrace: {ex.StackTrace}");
                }
                catch { }
            }
        }

        public async Task SendToUserByKullaniciIdAsync(int kullaniciId, string title, string body, object data = null, string channelId = null)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var user = await context.tb_Kullanici
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.KullaniciID == kullaniciId);

                    // KESIN KURAL: pasif kullaniciya push gitmemeli (bkz. SendToUserBySicilNoAsync'teki ayni kural).
                    if (user != null && user.Durum != true)
                    {
                        LogPush(context, "PUSH-NOT-SKIP-PASIF", $"KullaniciID {kullaniciId} (SicilNo {user.SicilNo}) pasif oldugu icin push gonderilmedi. Baslik: {title}");
                        return;
                    }

                    if (user != null)
                    {
                        List<string> tokens;
                        try
                        {
                            // bkz. SendToUserBySicilNoAsync'teki ayni VoIP-token filtreleme aciklamasi.
                            tokens = await context.tb_UserDevices
                                .AsNoTracking()
                                .Where(d => d.SicilNo == user.SicilNo
                                            && d.DeviceType != "FcmVoip"
                                            && d.DeviceType != "ApnsVoipProduction"
                                            && d.DeviceType != "ApnsVoipSandbox")
                                .Select(d => d.PushToken)
                                .ToListAsync();
                        }
                        catch (Exception exDevices)
                        {
                            _logger.LogWarning(exDevices, "PushNotificationService: tb_UserDevices sorgusu basarisiz (tablo eksik olabilir), tb_Kullanici.PushToken yedegine dusuluyor. KullaniciID {KullaniciId}", kullaniciId);
                            tokens = new List<string>();
                        }

                        if (tokens.Count == 0 && !string.IsNullOrEmpty(user.PushToken))
                        {
                            tokens.Add(user.PushToken);
                        }

                        foreach (var token in tokens)
                        {
                            if (!string.IsNullOrEmpty(token))
                            {
                                await SendExpoNotificationAsync(token, title, body, data, channelId, context, user.SicilNo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendToUserByKullaniciIdAsync failed for KullaniciID {KullaniciId}", kullaniciId);
            }
        }

        // TEŞHİS: normal ILogger çıktısı bu ortamda görünür/erişilebilir değildi (WebPortal'daki
        // fire-and-forget sorununa benzer bir belirsizlik) — bu yüzden aynı SicilNo/YBS DB'sindeki
        // tb_Log'a da yazıyoruz ki kullanıcı SQL ile doğrudan gerçek Expo cevabını görebilsin.
        // TEŞHİS: EF Core üzerinden (context.tb_Log.Add + SaveChanges) log yazma denemesi canlıda
        // hiçbir iz bırakmıyordu (ne başarı ne hata) — bu, EF Core'un DbContext tracking/concurrency
        // gibi bir nedenle sessizce başarısız olabileceğine işaret ediyor. Ham ADO.NET ile (ayrı,
        // bağımsız bir SqlConnection) INSERT yaparak EF Core'u tamamen devre dışı bırakıyoruz —
        // bu en güvenilir yol.
        private void LogPush(IYbsDbContext context, string konu, string aciklama)
        {
            try
            {
                if (context == null) return;
                string connStr = context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connStr)) return;

                // KOK NEDEN (2026-09-22'de bulundu): tb_Log.Cihaz nvarchar(10) — "backend-raw" (11
                // karakter) bile bu siniri asiyordu, SQL Server "String or binary data would be
                // truncated" hatasiyla INSERT'i tamamen reddediyordu. Bu yuzden LogPush BUGUNE KADAR
                // canlida BIR KEZ BILE basarili yazmamisti (Cihaz='backend-raw' icin sifir satir).
                // Konu/Aciklama de kolon sinirlarina (nvarchar(50)/(300)) gore guvenlik payiyla kesiliyor.
                string konuSafe = string.IsNullOrEmpty(konu) ? konu : (konu.Length > 50 ? konu.Substring(0, 50) : konu);
                string aciklamaSafe = string.IsNullOrEmpty(aciklama) ? aciklama : (aciklama.Length > 300 ? aciklama.Substring(0, 300) : aciklama);

                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                conn.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "INSERT INTO tb_Log (SicilNo, Eposta, Konu, Aciklama, Cihaz, KayitTar) VALUES (@SicilNo, @Eposta, @Konu, @Aciklama, @Cihaz, @KayitTar)",
                    conn);
                cmd.Parameters.AddWithValue("@SicilNo", "SYSTEM");
                cmd.Parameters.AddWithValue("@Eposta", "system@oyemsoft.com");
                cmd.Parameters.AddWithValue("@Konu", konuSafe);
                cmd.Parameters.AddWithValue("@Aciklama", (object)aciklamaSafe ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Cihaz", "Backend");
                cmd.Parameters.AddWithValue("@KayitTar", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "PushNotificationService: LogPush (raw ADO.NET) basarisiz. Konu: {Konu}", konu);
            }
        }

        private async Task SendExpoNotificationAsync(string pushToken, string title, string body, object data, string channelId = null, IYbsDbContext context = null, string sicilNo = null)
        {
            if (!pushToken.StartsWith("ExponentPushToken["))
            {
                _logger.LogWarning("PushNotificationService: Invalid Expo push token format: {Token}", pushToken);
                LogPush(context, "PUSH-NOT-ERROR", $"Gecersiz token formati: {pushToken}");
                return;
            }

            try
            {
                // KARAR (2026-09-23): Arama push'u ozel bir kanal/ses/oncelik (channelId=incoming_call_v2,
                // sound=incoming_call.wav, priority=high) kullaniyordu — Expo/APNs kabul edip receipt "ok"
                // dese de gercek cihazda uygulama kapaliyken HICBIR ZAMAN banner olarak gorunmuyordu (uzun
                // teshis: cihaz bildirim ayarlari dogru, payload manuel test edildiginde ayni yapida bile
                // calisiyordu — kok neden kesinlesemedi). Kullanici karariyla arama push'u da TUM DIGER
                // bildirimlerle AYNI, kanitlanmis guvenilir normal push yoluna alindi — ozel zil sesi/kanal
                // kaldirildi. Bildirime dokununca yine de tam ekran CallRingOverlay aciliyor (data.type=="call").
                object payload = new
                {
                    to = pushToken,
                    title = title,
                    body = body,
                    sound = "default",
                    data = data
                };

                // Türkçe karakterlerin bozulmadan gitmesi için gerçek UTF-8 JSON gönderilir
                // (varsayılan encoder yerine UnsafeRelaxedJsonEscaping + explicit UTF-8 content).
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = System.Text.Json.JsonSerializer.Serialize(payload, jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://exp.host/--/api/v2/push/send", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                // ÖNEMLİ: Expo, bilet (ticket) seviyesinde hata olsa bile (ör. DeviceNotRegistered,
                // MessageTooBig, InvalidCredentials) HTTP 200 dönebiliyor — gerçek durum JSON içindeki
                // data.status alanında. Sadece HTTP status koduna bakmak yanlış "başarılı" izlenimi
                // veriyordu — bu yüzden artık gövdeyi de kontrol ediyoruz.
                bool ticketError = false;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl))
                    {
                        // Expo tekil istek için "data" bir obje, toplu istekte dizi döner — ikisini de karşıla.
                        var statusEl = dataEl.ValueKind == System.Text.Json.JsonValueKind.Array
                            ? (dataEl.GetArrayLength() > 0 ? dataEl[0] : default)
                            : dataEl;
                        if (statusEl.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            statusEl.TryGetProperty("status", out var statusProp) &&
                            statusProp.GetString() == "error")
                        {
                            ticketError = true;
                        }
                    }
                }
                catch { }

                if (!response.IsSuccessStatusCode || ticketError)
                {
                    _logger.LogError("PushNotificationService: Expo push basarisiz. HTTP: {StatusCode}, TicketError: {TicketError}, Response: {Response}", response.StatusCode, ticketError, responseBody);
                    LogPush(context, "PUSH-NOT-ERROR", $"Expo hata (HTTP {response.StatusCode}, TicketError: {ticketError}). Response: {responseBody}, Payload: {json}");
                }
                else
                {
                    _logger.LogInformation("PushNotificationService: Notification sent successfully to token {Token}", pushToken);
                    // Basari da logluyoruz — "push kaydi var ama telefona gelmiyor" sikayetinde Expo'nun
                    // ticket'i kabul ettigini (ID'siyle) gormek, sorunun Expo/APNs sonrasinda oldugunu
                    // (cihaz/OS tarafi) kanitlamak icin sart. SicilNo burada kimin ALICI oldugunu gosterir.
                    LogPush(context, "PUSH-NOT-OK", $"SicilNo: {sicilNo}, Baslik: {title}, Token: {pushToken}, ExpoResponse: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendExpoNotificationAsync failed for token {Token}", pushToken);
                LogPush(context, "PUSH-NOT-ERROR", $"SicilNo: {sicilNo}, Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Bildirim alıcılarını iş kuralına göre çözer:
        /// 1) İşlemi yapan kişiye bildirim GİTMEZ.
        /// 2) Kalan ilgililere (kayıt sahibi / sorumlu) gider.
        /// 3) Hiç ilgili kalmazsa modül admin'lerine gider
        ///    (tb_Kullanici.AdminBelgeTur içinde modül kodu geçenler, ör. TICKET).
        /// </summary>
        private async Task<List<string>> ResolveRecipientsAsync(
            IYbsDbContext context, string actorSicil, string moduleCode, params string[] ilgililer)
        {
            var actor = (actorSicil ?? "").Trim();

            var list = (ilgililer ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !string.Equals(s, actor, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count > 0) return list;

            // İlgili yoksa modül admin'lerine düş
            var mod = (moduleCode ?? "").ToUpperInvariant();
            if (string.IsNullOrEmpty(mod)) return list;

            var adminRows = await context.tb_Kullanici.AsNoTracking()
                .Where(k => k.AdminBelgeTur != null)
                .Select(k => new { k.SicilNo, k.AdminBelgeTur })
                .ToListAsync();
            var admins = adminRows
                .Where(k => AdminBelgeTuruHelper.HasYetki(k.AdminBelgeTur, mod))
                .Select(k => k.SicilNo)
                .ToList();

            return admins
                .Where(s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s.Trim(), actor, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // --------------------------------------------------------------------
        // Leave Requests (Izin Talep)
        // --------------------------------------------------------------------

        public async Task NotifyNewLeaveRequestAsync(int leaveRequestId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var leave = await context.tb_IzinOnay.FirstOrDefaultAsync(l => l.IzinOnayID == leaveRequestId);
                    if (leave == null || string.IsNullOrEmpty(leave.BekleyenOnay)) return;

                    var requesterName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == leave.KayitSicil)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? leave.KayitSicil;

                    await SendToUserBySicilNoAsync(
                        leave.BekleyenOnay,
                        "Yeni Izin Talebi",
                        $"{requesterName} yeni bir izin talebi olusturdu ({leave.BelgeNo}). Onayiniz bekleniyor.",
                        new { type = "izin", screen = "IzinScreen", code = leave.BelgeNo }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyNewLeaveRequestAsync failed for ID {ID}", leaveRequestId);
            }
        }

        public async Task NotifyLeaveManagerApprovalsCompletedAsync(int leaveRequestId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var leave = await context.tb_IzinOnay.FirstOrDefaultAsync(l => l.IzinOnayID == leaveRequestId);
                    if (leave == null) return;

                    // Notify the requester
                    await SendToUserBySicilNoAsync(
                        leave.KayitSicil,
                        "Amir Onaylari Tamamlandi",
                        $"İzin talebinizin ({leave.BelgeNo}) amir onay süreci tamamlandı, İK işlemi bekleniyor.",
                        new { type = "izin", screen = "IzinScreen", code = leave.BelgeNo }
                    );

                    // Notify HR ("IK") users
                    var hrUsersAll = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.AdminBelgeTur != null)
                        .ToListAsync();
                    var hrUsers = hrUsersAll.Where(u => AdminBelgeTuruHelper.HasYetki(u.AdminBelgeTur, "IK")).ToList();

                    foreach (var hrUser in hrUsers)
                    {
                        if (hrUser.SicilNo == leave.KayitSicil) continue; // Exclude creator
                        await SendToUserBySicilNoAsync(
                            hrUser.SicilNo,
                            "IK Onayi Bekleyen Izin",
                            $"Yeni bir izin talebi ({leave.BelgeNo}) IK onayinizi bekliyor.",
                            new { type = "izin", screen = "IzinScreen", code = leave.BelgeNo }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyLeaveManagerApprovalsCompletedAsync failed for ID {ID}", leaveRequestId);
            }
        }

        public async Task NotifyLeaveRequestRejectedAsync(int leaveRequestId, int actionUserId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var leave = await context.tb_IzinOnay.AsNoTracking().FirstOrDefaultAsync(l => l.IzinOnayID == leaveRequestId);
                    if (leave == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    var actionUserName = actionUser?.AdSoyad ?? "Yönetici";

                    await SendToUserBySicilNoAsync(
                        leave.KayitSicil,
                        "Izin Talebiniz Reddedildi",
                        $"Izin talebiniz ({leave.BelgeNo}) {actionUserName} tarafindan reddedildi.",
                        new { type = "izin", screen = "IzinScreen", code = leave.BelgeNo }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyLeaveRequestRejectedAsync failed for ID {ID}", leaveRequestId);
            }
        }

        public async Task NotifyLeaveRequestCompletedAsync(int leaveRequestId, int actionUserId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var leave = await context.tb_IzinOnay.AsNoTracking().FirstOrDefaultAsync(l => l.IzinOnayID == leaveRequestId);
                    if (leave == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    var actionUserName = actionUser?.AdSoyad ?? "IK Yetkilisi";

                    await SendToUserBySicilNoAsync(
                        leave.KayitSicil,
                        "Izin Talebiniz Tamamlandi",
                        $"Izin talebiniz ({leave.BelgeNo}) {actionUserName} tarafindan onaylandi.",
                        new { type = "izin", screen = "IzinScreen", code = leave.BelgeNo }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyLeaveRequestCompletedAsync failed for ID {ID}", leaveRequestId);
            }
        }

        // --------------------------------------------------------------------
        // IT, ERP, Maintenance Requests (Talepler)
        // --------------------------------------------------------------------

        public async Task NotifyNewTalepAsync(int talepId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null) return;

                    var requesterName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == talep.KayitSicil)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? talep.KayitSicil;

                    string tur = talep.TalepTurKodu.ToUpper();
                    List<string> targetSicilNos = new List<string>();

                    // Kullanicinin ACIKCA belirttigi kural (baska bir yorum/yedek yol EKLENMEDI):
                    // - IT/ERP: tb_TalepAyar'da bu KATEGORIYE ait YoneticiMi=true olan HERKES —
                    //   sirket kodu eslesmesi ARANMAZ.
                    // - BAKIM: tb_TalepAyar'da bu KATEGORIYE ait YoneticiMi=true OLAN VE o kisinin
                    //   tb_TalepAyar.SirketKodu'su, bu spesifik talebin tb_TalepBakim.SirketKodu'suyla
                    //   ESLESEN kisiler.
                    if (talep.KategoriID.HasValue)
                    {
                        if (tur == "BAKIM")
                        {
                            string bakimSirketKodu = await context.tb_TalepBakim
                                .AsNoTracking()
                                .Where(tb => tb.TalepKodu == talep.TalepKodu)
                                .Select(tb => tb.SirketKodu)
                                .FirstOrDefaultAsync();

                            if (!string.IsNullOrEmpty(bakimSirketKodu))
                            {
                                targetSicilNos = await context.tb_TalepAyar
                                    .AsNoTracking()
                                    .Where(ta => ta.KategoriID == talep.KategoriID && ta.YoneticiMi == true && ta.SirketKodu == bakimSirketKodu)
                                    .Select(ta => ta.SicilNo)
                                    .Distinct()
                                    .ToListAsync();
                            }
                        }
                        else
                        {
                            targetSicilNos = await context.tb_TalepAyar
                                .AsNoTracking()
                                .Where(ta => ta.KategoriID == talep.KategoriID && ta.YoneticiMi == true)
                                .Select(ta => ta.SicilNo)
                                .Distinct()
                                .ToListAsync();
                        }
                    }

                    var users = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => targetSicilNos.Contains(u.SicilNo))
                        .Select(u => new { u.SicilNo, u.Eposta })
                        .ToListAsync();

                    // TESHIS: "push kaydi var ama telefona gelmiyor" sikayetinde asil bilinmeyen
                    // hedef cozumlemesiydi — targetSicilNos bos/yanlis kisiye dusuyor olabilirdi,
                    // bunu goremiyorduk. Artik cozumlenen liste (ve "yaratan" filtresinden once/sonra)
                    // acikca loglaniyor.
                    LogPush(context, "TALEP-PUSH-TARGET",
                        $"TalepID: {talepId} ({talep.TalepKodu}), KayitSicil (yaratan): {talep.KayitSicil}, " +
                        $"CozumlenenHedefler: [{string.Join(", ", targetSicilNos)}], " +
                        $"tb_KullanicidaBulunanlar: [{string.Join(", ", users.Select(u => u.SicilNo))}]");

                    string typeLabel = tur == "BAKIM" ? "Bakım" : (tur == "ERP" ? "ERP" : "IT");
                    string title = $"Yeni {typeLabel} Talebi";
                    string body = $"{requesterName} tarafından yeni bir {typeLabel.ToLower()} talebi ({talep.TalepKodu}) açıldı.";

                    var notificationService = scope.ServiceProvider.GetService<INotificationService>();

                    foreach (var u in users)
                    {
                        if (u.SicilNo == talep.KayitSicil)
                        {
                            LogPush(context, "TALEP-PUSH-SKIP", $"TalepID: {talepId}, SicilNo {u.SicilNo} yaratan oldugu icin atlandi.");
                            continue; // Skip creator
                        }

                        // 1. Push Bildirim Gönder
                        await SendToUserBySicilNoAsync(
                            u.SicilNo,
                            title,
                            body,
                            new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                        );

                        // 2. Mail Gönder
                        if (notificationService != null && !string.IsNullOrEmpty(u.Eposta))
                        {
                            string mailKonu = $"{talep.TalepKodu} Nolu Yeni {typeLabel} Talebi Alındı";
                            string mailIcerik = $@"Merhaba,<br/><br/>
Yeni bir {typeLabel.ToLower()} talebi oluşturulmuştur ve yetki/sorumluluk alanınızdadır.<br/><br/>
<b>Talep Kodu:</b> {talep.TalepKodu}<br/>
<b>Açan Kişi:</b> {requesterName}<br/>
<b>Konu:</b> {talep.Konu}<br/>
<b>Açıklama:</b> {talep.Aciklama}<br/><br/>
İyi çalışmalar dileriz.";

                            _ = notificationService.SendMailAsync(
                                $"OyemCore {talep.TalepTurKodu}",
                                mailKonu,
                                mailIcerik,
                                u.Eposta
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyNewTalepAsync failed for ID {ID}", talepId);
            }
        }

        public async Task NotifyTalepSorumluAtandiAsync(int talepId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null || string.IsNullOrEmpty(talep.SorumluSicil)) return;

                    var sorumluName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == talep.SorumluSicil)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? talep.SorumluSicil;

                    string typeLabel = talep.TalepTurKodu == "BAKIM" ? "Bakim" : talep.TalepTurKodu;
                    
                    await SendToUserBySicilNoAsync(
                        talep.KayitSicil,
                        $"{typeLabel} Talebinize Uzman Atandı",
                        $"'{talep.Konu}' konulu talebinize ({talep.TalepKodu}) sorumlu uzman atandı: {sorumluName}.",
                        new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                    );

                    await SendToUserBySicilNoAsync(
                        talep.SorumluSicil,
                        $"Yeni {typeLabel} Talebi Atandı",
                        $"'{talep.Konu}' konulu talep ({talep.TalepKodu}) size atandı.",
                        new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTalepSorumluAtandiAsync failed for ID {ID}", talepId);
            }
        }

        public async Task NotifyTalepGelismeAsync(int talepId, int actionUserId, string description)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    if (actionUser == null) return;

                    string typeLabel = talep.TalepTurKodu == "BAKIM" ? "Bakim" : talep.TalepTurKodu;
                    string title = $"{typeLabel} Talebi Gelismesi";
                    string body = $"'{talep.Konu}' konulu talebe ({talep.TalepKodu}) yeni bir gelisme eklendi.";

                    if (actionUser.SicilNo == talep.KayitSicil)
                    {
                        if (!string.IsNullOrEmpty(talep.SorumluSicil))
                        {
                            await SendToUserBySicilNoAsync(
                                talep.SorumluSicil,
                                title,
                                body,
                                new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                            );
                        }
                    }
                    else if (actionUser.SicilNo == talep.SorumluSicil)
                    {
                        await SendToUserBySicilNoAsync(
                            talep.KayitSicil,
                            title,
                            body,
                            new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                        );
                    }
                    else
                    {
                        await SendToUserBySicilNoAsync(
                            talep.KayitSicil,
                            title,
                            body,
                            new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                        );

                        if (!string.IsNullOrEmpty(talep.SorumluSicil))
                        {
                            await SendToUserBySicilNoAsync(
                                talep.SorumluSicil,
                                title,
                                body,
                                new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTalepGelismeAsync failed for ID {ID}", talepId);
            }
        }

        public async Task NotifyTalepClosedAsync(int talepId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null) return;

                    string typeLabel = talep.TalepTurKodu == "BAKIM" ? "Bakim" : talep.TalepTurKodu;
                    string title = $"{typeLabel} Talebiniz Tamamlandi";
                    string body = talep.TalepTurKodu == "BAKIM"
                        ? $"Bakım talebiniz ({talep.TalepKodu}) tamamlandı, fakat sürecin tamamlanması için onay vermelisiniz."
                        : $"'{talep.Konu}' konulu talebiniz ({talep.TalepKodu}) tamamlandi.";

                    await SendToUserBySicilNoAsync(
                        talep.KayitSicil,
                        title,
                        body,
                        new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTalepClosedAsync failed for ID {ID}", talepId);
            }
        }

        // --------------------------------------------------------------------
        // Talep - İşlem Onay alt-süreci (ör. Bakım talebi kapanmadan önce
        // kayıt sahibinin/amirin onayına gönderilmesi; onaylanırsa süreç
        // tamamlanır, reddedilirse sorumluya geri döner)
        // --------------------------------------------------------------------

        public async Task NotifyTalepOnayaGonderildiAsync(int talepId, string onayciSicil)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null || string.IsNullOrEmpty(onayciSicil)) return;

                    string typeLabel = talep.TalepTurKodu == "BAKIM" ? "Bakim" : talep.TalepTurKodu;

                    await SendToUserBySicilNoAsync(
                        onayciSicil,
                        $"{typeLabel} Talebi Onayınızı Bekliyor",
                        $"'{talep.Konu}' konulu talep ({talep.TalepKodu}) işlem onayınıza gönderildi.",
                        new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTalepOnayaGonderildiAsync failed for ID {ID}", talepId);
            }
        }

        public async Task NotifyTalepOnaylandiAsync(int talepId, int actionUserId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    var actionUserName = actionUser?.AdSoyad ?? "Yetkili";
                    string typeLabel = talep.TalepTurKodu == "BAKIM" ? "Bakim" : talep.TalepTurKodu;

                    if (!string.IsNullOrEmpty(talep.SorumluSicil))
                    {
                        await SendToUserBySicilNoAsync(
                            talep.SorumluSicil,
                            $"{typeLabel} Talebi Onayı Verildi",
                            $"'{talep.Konu}' konulu talep ({talep.TalepKodu}) için işlem onayı {actionUserName} tarafından verildi.",
                            new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTalepOnaylandiAsync failed for ID {ID}", talepId);
            }
        }

        public async Task NotifyTalepReddedildiAsync(int talepId, int actionUserId, string sebep)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var talep = await context.tb_Talep.AsNoTracking().FirstOrDefaultAsync(t => t.TalepID == talepId);
                    if (talep == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    var actionUserName = actionUser?.AdSoyad ?? "Yetkili";
                    string typeLabel = talep.TalepTurKodu == "BAKIM" ? "Bakim" : talep.TalepTurKodu;

                    if (!string.IsNullOrEmpty(talep.SorumluSicil))
                    {
                        await SendToUserBySicilNoAsync(
                            talep.SorumluSicil,
                            $"{typeLabel} Talebi Onayı Reddedildi",
                            $"'{talep.Konu}' konulu talep ({talep.TalepKodu}) {actionUserName} tarafından reddedildi. Sebep: {sebep}",
                            new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTalepReddedildiAsync failed for ID {ID}", talepId);
            }
        }

        // --------------------------------------------------------------------
        // Tedarikçi Değerlendirme
        // --------------------------------------------------------------------

        public async Task NotifyNewTedarikciDegerlendirmeAsync(string belgeNo)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var deg = await context.tb_TedDeg.AsNoTracking().FirstOrDefaultAsync(d => d.BelgeNo == belgeNo);
                    if (deg == null) return;

                    var requesterName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == deg.KayitSicil)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? deg.KayitSicil;

                    // Sorumlu bir kişi ataması olmadığı için, TEDARIKCI veya ADMIN yetkili kullanıcılar bilgilendirilir.
                    // NOT: "TEDARIKCI" katalogda tanımlı bir kod degil (muhtemelen "KALITE" olmali,
                    // bkz. ClsAdmin.TumListe — Tedarikçi Değerlendirme KALITE yetkisi altında) — bu
                    // guvenlik temizliginin kapsami disinda, davranis degistirilmeden ayni kod
                    // birebir korunuyor.
                    var yetkililerAll = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.AdminBelgeTur != null)
                        .ToListAsync();
                    var yetkililer = yetkililerAll.Where(u => AdminBelgeTuruHelper.HasYetki(u.AdminBelgeTur, "TEDARIKCI")).ToList();

                    foreach (var yetkili in yetkililer)
                    {
                        if (yetkili.SicilNo == deg.KayitSicil) continue;
                        await SendToUserBySicilNoAsync(
                            yetkili.SicilNo,
                            "Yeni Tedarikçi Değerlendirme Talebi",
                            $"{requesterName} tarafından yeni bir tedarikçi değerlendirme kaydı ({deg.BelgeNo}) açıldı.",
                            new { type = "tedarikci", screen = "TedarikciScreen", code = deg.BelgeNo }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyNewTedarikciDegerlendirmeAsync failed for BelgeNo {BelgeNo}", belgeNo);
            }
        }

        public async Task NotifyTedarikciDegerlendirmeCompletedAsync(string belgeNo)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var deg = await context.tb_TedDeg.AsNoTracking().FirstOrDefaultAsync(d => d.BelgeNo == belgeNo);
                    if (deg == null || string.IsNullOrEmpty(deg.KayitSicil)) return;

                    await SendToUserBySicilNoAsync(
                        deg.KayitSicil,
                        "Tedarikçi Değerlendirmeniz Tamamlandı",
                        $"Talep ettiğiniz tedarikçi değerlendirmesi ({deg.BelgeNo}) tamamlandı.",
                        new { type = "tedarikci", screen = "TedarikciScreen", code = deg.BelgeNo }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTedarikciDegerlendirmeCompletedAsync failed for BelgeNo {BelgeNo}", belgeNo);
            }
        }

        public async Task NotifyTedarikciDegerlendirmeCancelledAsync(string belgeNo)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var deg = await context.tb_TedDeg.AsNoTracking().FirstOrDefaultAsync(d => d.BelgeNo == belgeNo);
                    if (deg == null || string.IsNullOrEmpty(deg.KayitSicil)) return;

                    await SendToUserBySicilNoAsync(
                        deg.KayitSicil,
                        "Tedarikçi Değerlendirmeniz İptal Edildi",
                        $"Talep ettiğiniz tedarikçi değerlendirmesi ({deg.BelgeNo}) iptal edildi.",
                        new { type = "tedarikci", screen = "TedarikciScreen", code = deg.BelgeNo }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTedarikciDegerlendirmeCancelledAsync failed for BelgeNo {BelgeNo}", belgeNo);
            }
        }

        // --------------------------------------------------------------------
        // Asset/Zimmet Operations
        // --------------------------------------------------------------------

        public async Task NotifyAssetAssignedAsync(int aygitPersonelId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var log = await context.tb_AygitPersonel.AsNoTracking().FirstOrDefaultAsync(ap => ap.AygitPersonelID == aygitPersonelId);
                    if (log == null) return;

                    var asset = await context.tb_Aygit.AsNoTracking().FirstOrDefaultAsync(a => a.AygitID == log.AygitID);
                    if (asset == null) return;

                    var senderName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == log.TeslimEdenSicil)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? log.TeslimEdenSicil;

                    string demirbasKod = !string.IsNullOrEmpty(asset.DemirbasKodu) ? asset.DemirbasKodu : asset.AygitID.ToString();

                    await SendToUserBySicilNoAsync(
                        log.PersonelSicil,
                        "Üzerinize Yeni Zimmet Atandı",
                        $"{senderName} tarafından üzerinize '{asset.Tanim}' ({demirbasKod}) demirbaşı zimmetlendi.",
                        new { type = "zimmet", screen = "ZimmetlerimScreen", id = log.AygitID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyAssetAssignedAsync failed for ID {ID}", aygitPersonelId);
            }
        }

        public async Task NotifyAssetReturnedAsync(int aygitPersonelId, int actionUserId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var log = await context.tb_AygitPersonel.AsNoTracking().FirstOrDefaultAsync(ap => ap.AygitPersonelID == aygitPersonelId);
                    if (log == null) return;

                    var asset = await context.tb_Aygit.AsNoTracking().FirstOrDefaultAsync(a => a.AygitID == log.AygitID);
                    if (asset == null) return;

                    var receiverUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    var receiverName = receiverUser?.AdSoyad ?? "Zimmet Sorumlusu";

                    string demirbasKod = !string.IsNullOrEmpty(asset.DemirbasKodu) ? asset.DemirbasKodu : asset.AygitID.ToString();

                    await SendToUserBySicilNoAsync(
                        log.PersonelSicil,
                        "Zimmet Iade Alindi",
                        $"Üzerinizdeki '{asset.Tanim}' ({demirbasKod}) demirbaşı {receiverName} tarafından iade alındı ve zimmetiniz düşürüldü.",
                        new { type = "zimmet", screen = "ZimmetlerimScreen", id = log.AygitID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyAssetReturnedAsync failed for ID {ID}", aygitPersonelId);
            }
        }

        public async Task NotifyAssetRemovedAsync(string personelSicil, int aygitId, string actionUserAdSoyad)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var asset = await context.tb_Aygit.AsNoTracking().FirstOrDefaultAsync(a => a.AygitID == aygitId);
                    if (asset == null) return;

                    string demirbasKod = !string.IsNullOrEmpty(asset.DemirbasKodu) ? asset.DemirbasKodu : asset.AygitID.ToString();

                    await SendToUserBySicilNoAsync(
                        personelSicil,
                        "Zimmet Kaldırıldı",
                        $"{actionUserAdSoyad} tarafından üzerinizdeki '{asset.Tanim}' ({demirbasKod}) demirbaş zimmeti kaldırıldı.",
                        new { type = "zimmet", screen = "ZimmetlerimScreen", id = aygitId }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyAssetRemovedAsync failed for AygitID {ID}", aygitId);
            }
        }

        public async Task NotifyAssetFaultReportedAsync(string adminSicilNo, int aygitId, string reporterAdSoyad, string description)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var asset = await context.tb_Aygit.AsNoTracking().FirstOrDefaultAsync(a => a.AygitID == aygitId);
                    if (asset == null) return;

                    string demirbasKod = !string.IsNullOrEmpty(asset.DemirbasKodu) ? asset.DemirbasKodu : asset.AygitID.ToString();

                    await SendToUserBySicilNoAsync(
                        adminSicilNo,
                        "Demirbaş Arıza Bildirimi",
                        $"{reporterAdSoyad} tarafından '{asset.Tanim}' ({demirbasKod}) demirbaş için arıza bildirildi: {description}",
                        new { type = "zimmet", screen = "DemirbasDetayScreen", id = aygitId }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyAssetFaultReportedAsync failed for AygitID {ID}", aygitId);
            }
        }

        // --------------------------------------------------------------------
        // Bakım Planı / Periyodik Kontrol Planı - Temizlik Onay Formu
        // --------------------------------------------------------------------

        public async Task NotifyTemizlikOnayCreatedAsync(int onayId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var onay = await context.tb_BakimPlanTemizlikOnay.AsNoTracking().FirstOrDefaultAsync(o => o.OnayID == onayId);
                    if (onay == null) return;

                    await SendToUserBySicilNoAsync(
                        onay.SecilenSicil,
                        "Temizlik ve Kontrol Formu Onay Bekliyor",
                        $"#{onay.PlanKodu} nolu işlem tamamlanmıştır. Lütfen makine temizlik ve kontrol formunu doldurarak onaylayınız.",
                        new { type = "temizlikonay", planTuru = onay.PlanTuru, planKodu = onay.PlanKodu, onayId = onay.OnayID, screen = "TemizlikOnayForm", id = onay.OnayID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTemizlikOnayCreatedAsync failed for ID {ID}", onayId);
            }
        }

        public async Task NotifyTemizlikOnayCompletedAsync(int onayId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var onay = await context.tb_BakimPlanTemizlikOnay.AsNoTracking().FirstOrDefaultAsync(o => o.OnayID == onayId);
                    if (onay == null) return;

                    var doldurgan = await context.tb_Personel.AsNoTracking().Where(p => p.SicilNo == onay.SecilenSicil).Select(p => p.AdSoyad).FirstOrDefaultAsync() ?? onay.SecilenSicil;

                    await SendToUserBySicilNoAsync(
                        onay.SecenSicil,
                        "Temizlik Onay Formu Dolduruldu",
                        $"{doldurgan} tarafından #{onay.PlanKodu} nolu işlemin temizlik onay formu dolduruldu.",
                        new { type = "temizlikonay", planTuru = onay.PlanTuru, planKodu = onay.PlanKodu, onayId = onay.OnayID, screen = onay.PlanTuru == "PERIYODIK" ? "PeriyodikKontrol" : "BakimPlan", code = onay.PlanKodu }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTemizlikOnayCompletedAsync failed for ID {ID}", onayId);
            }
        }

        public async Task NotifyTemizlikOnayRejectedAsync(int onayId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var onay = await context.tb_BakimPlanTemizlikOnay.AsNoTracking().FirstOrDefaultAsync(o => o.OnayID == onayId);
                    if (onay == null) return;

                    var reddeden = await context.tb_Personel.AsNoTracking().Where(p => p.SicilNo == onay.SecilenSicil).Select(p => p.AdSoyad).FirstOrDefaultAsync() ?? onay.SecilenSicil;

                    await SendToUserBySicilNoAsync(
                        onay.SecenSicil,
                        "İşlem Tamamlanmadı Olarak Geri Gönderildi",
                        $"{reddeden} tarafından #{onay.PlanKodu} nolu işlem temizlik/kontrol açısından uygun bulunmadı.",
                        new { type = "temizlikonay", planTuru = onay.PlanTuru, planKodu = onay.PlanKodu, onayId = onay.OnayID, screen = onay.PlanTuru == "PERIYODIK" ? "PeriyodikKontrol" : "BakimPlan", code = onay.PlanKodu }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTemizlikOnayRejectedAsync failed for ID {ID}", onayId);
            }
        }

        // --------------------------------------------------------------------
        // Ticketing (Ticket)
        // --------------------------------------------------------------------

        public async Task NotifyNewTicketAsync(int ticketId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var ticket = await context.tb_Ticket.AsNoTracking().FirstOrDefaultAsync(t => t.ID == ticketId);
                    if (ticket == null) return;

                    var requesterName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == ticket.KayitSicilNo)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? ticket.KayitSicilNo;

                    var adminsAll = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.AdminBelgeTur != null)
                        .ToListAsync();
                    var admins = adminsAll.Where(u => AdminBelgeTuruHelper.HasYetki(u.AdminBelgeTur, "TICKET")).ToList();

                    foreach (var admin in admins)
                    {
                        if (admin.SicilNo == ticket.KayitSicilNo) continue; // Exclude creator
                        await SendToUserBySicilNoAsync(
                            admin.SicilNo,
                            "Yeni Destek Talebi (Ticket)",
                            $"{requesterName} tarafından '{ticket.Baslik}' başlıklı yeni bir ticket ({ticket.TakipKodu}) açıldı.",
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyNewTicketAsync failed for ID {ID}", ticketId);
            }
        }

        public async Task NotifyTicketSorumluAtandiAsync(int ticketId, int actionUserId = 0)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var ticket = await context.tb_Ticket.AsNoTracking().FirstOrDefaultAsync(t => t.ID == ticketId);
                    if (ticket == null) return;

                    // İşlemi yapan kişi (varsa) — kendisine bildirim gitmeyecek.
                    var actorSicil = actionUserId > 0
                        ? (await context.tb_Kullanici.AsNoTracking()
                            .Where(u => u.KullaniciID == actionUserId)
                            .Select(u => u.SicilNo).FirstOrDefaultAsync() ?? "")
                        : "";

                    var sorumluName = string.IsNullOrEmpty(ticket.SorumluSicilNo)
                        ? ""
                        : (await context.tb_Personel.AsNoTracking()
                            .Where(p => p.SicilNo == ticket.SorumluSicilNo)
                            .Select(p => p.AdSoyad).FirstOrDefaultAsync() ?? ticket.SorumluSicilNo);

                    // Atanan kişiye özel bildirim (işlemi kendisi yaptıysa gitmez)
                    if (!string.IsNullOrEmpty(ticket.SorumluSicilNo)
                        && !string.Equals(ticket.SorumluSicilNo, actorSicil, StringComparison.OrdinalIgnoreCase))
                    {
                        await SendToUserBySicilNoAsync(
                            ticket.SorumluSicilNo,
                            "Size Yeni Ticket Atandı",
                            $"'{ticket.Baslik}' başlıklı ticket ({ticket.TakipKodu}) size atandı.",
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );
                    }

                    // Kayıt sahibine bilgi; sahip de yoksa/işlemi yapan ise TICKET admin'lerine düş
                    var digerAlicilar = await ResolveRecipientsAsync(context, actorSicil, "TICKET", ticket.KayitSicilNo);
                    foreach (var sicil in digerAlicilar)
                    {
                        if (string.Equals(sicil, ticket.SorumluSicilNo, StringComparison.OrdinalIgnoreCase)) continue;
                        await SendToUserBySicilNoAsync(
                            sicil,
                            "Ticketınıza Sorumlu Atandı",
                            $"'{ticket.Baslik}' başlıklı destek talebine ({ticket.TakipKodu}) sorumlu atandı: {sorumluName}.",
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTicketSorumluAtandiAsync failed for ID {ID}", ticketId);
            }
        }

        public async Task NotifyTicketStatusChangedAsync(int ticketId, string oldStatus, string newStatus, int actionUserId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var ticket = await context.tb_Ticket.AsNoTracking().FirstOrDefaultAsync(t => t.ID == ticketId);
                    if (ticket == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    if (actionUser == null) return;

                    string title = "Ticket Durumu Güncellendi";
                    string body = $"'{ticket.Baslik}' baslikli ticketinizin ({ticket.TakipKodu}) durumu '{newStatus}' olarak güncellendi.";

                    if (actionUser.SicilNo == ticket.KayitSicilNo)
                    {
                        // Kayıt sahibi işlem yaptı → sorumluya; sorumlu yoksa TICKET admin'lerine
                        var alicilar = await ResolveRecipientsAsync(
                            context, actionUser.SicilNo, "TICKET", ticket.SorumluSicilNo);
                        foreach (var sicil in alicilar)
                        {
                            await SendToUserBySicilNoAsync(
                                sicil,
                                title,
                                body,
                                new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                            );
                        }
                    }
                    else if (actionUser.SicilNo == ticket.SorumluSicilNo)
                    {
                        await SendToUserBySicilNoAsync(
                            ticket.KayitSicilNo,
                            title,
                            body,
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );
                    }
                    else
                    {
                        await SendToUserBySicilNoAsync(
                            ticket.KayitSicilNo,
                            title,
                            body,
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );

                        if (!string.IsNullOrEmpty(ticket.SorumluSicilNo))
                        {
                            await SendToUserBySicilNoAsync(
                                ticket.SorumluSicilNo,
                                title,
                                body,
                                new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTicketStatusChangedAsync failed for ID {ID}", ticketId);
            }
        }

        public async Task NotifyTicketGelismeAsync(int ticketId, int actionUserId, string comment)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var ticket = await context.tb_Ticket.AsNoTracking().FirstOrDefaultAsync(t => t.ID == ticketId);
                    if (ticket == null) return;

                    var actionUser = await context.tb_Kullanici.AsNoTracking().FirstOrDefaultAsync(u => u.KullaniciID == actionUserId);
                    if (actionUser == null) return;

                    string title = "Destek Talebi Gelismesi (Ticket)";
                    string body = $"'{ticket.Baslik}' baslikli destek talebine ({ticket.TakipKodu}) yeni bir yorum/gelisme eklendi.";

                    if (actionUser.SicilNo == ticket.KayitSicilNo)
                    {
                        // Kayıt sahibi işlem yaptı → sorumluya; sorumlu yoksa TICKET admin'lerine
                        var alicilar = await ResolveRecipientsAsync(
                            context, actionUser.SicilNo, "TICKET", ticket.SorumluSicilNo);
                        foreach (var sicil in alicilar)
                        {
                            await SendToUserBySicilNoAsync(
                                sicil,
                                title,
                                body,
                                new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                            );
                        }
                    }
                    else if (actionUser.SicilNo == ticket.SorumluSicilNo)
                    {
                        await SendToUserBySicilNoAsync(
                            ticket.KayitSicilNo,
                            title,
                            body,
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );
                    }
                    else
                    {
                        await SendToUserBySicilNoAsync(
                            ticket.KayitSicilNo,
                            title,
                            body,
                            new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                        );

                        if (!string.IsNullOrEmpty(ticket.SorumluSicilNo))
                        {
                            await SendToUserBySicilNoAsync(
                                ticket.SorumluSicilNo,
                                title,
                                body,
                                new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTicketGelismeAsync failed for ID {ID}", ticketId);
            }
        }

        // Ticket silindiğinde: kayıt eden ve sorumluya bildirim (silen kişi hariç).
        // Ticket kaydı silindiği için gerekli bilgiler doğrudan parametreyle gelir.
        public async Task NotifyTicketDeletedAsync(string kayitSicil, string sorumluSicil, string takipKodu, string baslik, string silenAdSoyad, string silenSicil)
        {
            try
            {
                string title = "Destek Talebi Silindi (Ticket)";
                string body = $"'{baslik}' başlıklı destek talebi ({takipKodu}) {silenAdSoyad} tarafından silindi.";
                var data = new { type = "ticket", screen = "TicketScreen" };

                if (!string.IsNullOrEmpty(kayitSicil) && kayitSicil != silenSicil)
                    await SendToUserBySicilNoAsync(kayitSicil, title, body, data);

                if (!string.IsNullOrEmpty(sorumluSicil) && sorumluSicil != silenSicil && sorumluSicil != kayitSicil)
                    await SendToUserBySicilNoAsync(sorumluSicil, title, body, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyTicketDeletedAsync failed for {Kod}", takipKodu);
            }
        }
    }
}

