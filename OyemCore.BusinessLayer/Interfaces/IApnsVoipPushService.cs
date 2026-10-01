using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    // iOS'ta uygulama kapalıyken/arka plandayken native CallKit tam ekran gelen arama arayüzünü
    // uyandırmak için ham PushKit VoIP push gönderir (Apple'ın Expo push (exp.host) relay'i VoIP
    // push tipini desteklemediği için bu, o sistemin TAMAMEN dışında, doğrudan APNs'e giden ayrı
    // bir yol — FCM VoIP push'un (Android) iOS karşılığı).
    public interface IApnsVoipPushService
    {
        // isProduction: tb_UserDevices.DeviceType == "ApnsVoipProduction" ise true, "ApnsVoipSandbox" ise false.
        // Donus degeri: normal ILogger ciktisi bu ortamda gorunur/erisilebilir degildi (bkz.
        // PushNotificationService.LogPush) — cagiran taraf (PushNotificationService) sonucu
        // tb_Log'a yazabilsin diye basari/detay burada acikca donduruluyor.
        Task<(bool Success, string Detail)> SendCallWakeAsync(string deviceToken, bool isProduction, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage);
    }
}
