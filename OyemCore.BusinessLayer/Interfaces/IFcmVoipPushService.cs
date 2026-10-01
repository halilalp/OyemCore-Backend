using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Android'de uygulama kapalıyken/arka plandayken native tam ekran gelen arama arayüzünü
    // (react-native-callkeep + ConnectionService) uyandırmak için data-only FCM mesajı gönderir.
    // Expo push'tan (IPushNotificationService) TAMAMEN AYRI bir yol — Expo push uygulamayı
    // force-quit durumunda güvenilir şekilde uyandıramaz, bu servis onun yerini almaz, tamamlar.
    public interface IFcmVoipPushService
    {
        Task<(bool Success, string Detail)> SendCallWakeAsync(string fcmToken, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage);
    }
}
