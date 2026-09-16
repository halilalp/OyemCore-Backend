using System.Threading.Tasks;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface INotificationService
    {
        Task SendMailAsync(string uygulama, string konu, string icerik, string eposta);

        // Referans: WebPortal DataLayer/ClsMail.SmsGonder — doğrudan gateway çağrısı yapmaz,
        // tb_Sms kuyruğuna kayıt atar; gerçek gönderimi mevcut Windows Servis üstlenir.
        // Bu yapı bilerek korunuyor (kullanıcı talebi), gateway entegrasyonu burada tekrarlanmaz.
        Task SendSmsAsync(string uygulama, string konu, string icerik, string tlf, string adSoyad);
    }
}