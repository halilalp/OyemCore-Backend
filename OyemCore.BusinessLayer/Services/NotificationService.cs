using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Contexts;
using OyemCore.DataLayer.Entities;
using Microsoft.Extensions.Logging;

namespace OyemCore.BusinessLayer.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IServiceScopeFactory scopeFactory, ILogger<NotificationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SendMailAsync(string uygulama, string konu, string icerik, string eposta)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var ybsContext = scope.ServiceProvider.GetRequiredService<YbsDbContext>();

                    // 1. Veritabanına log/kuyruk kaydı at (tb_Mail)
                    var mailLog = new tb_Mail
                    {
                        Gonderen = uygulama,
                        Konu = konu,
                        Icerik = icerik,
                        AlanEPosta = eposta,
                        Durum = false,
                        TryCount = 0,
                        KayitTarih = DateTime.Now
                    };
                    ybsContext.tb_Mail.Add(mailLog);
                    await ybsContext.SaveChangesAsync();
                    
                    var mailId = mailLog.MailID;

                    // 2. tb_Ayarlar tablosundan CalismaSekli parametresini kontrol et
                    var mailAyar = await ybsContext.tb_Ayarlar.FirstOrDefaultAsync();
                    
                    // Eğer CalismaSekli "DIREKT" ise arka planda doğrudan gönderimi başlat
                    if (mailAyar != null && mailAyar.CalismaSekli == "DIREKT")
                    {
                        // DIREKT modunda mailleri asıl akışı bloklamadan (fire and forget) arka planda gönderiyoruz.
                        _ = Task.Run(async () =>
                        {
                            using (var bgScope = _scopeFactory.CreateScope())
                            {
                                try
                                {
                                    var bgYbsContext = bgScope.ServiceProvider.GetRequiredService<YbsDbContext>();
                                    var ayar = await bgYbsContext.tb_Ayarlar.FirstOrDefaultAsync();
                                    if (ayar == null) return;

                                    using (var smtp = new SmtpClient())
                                    {
                                        smtp.Host = ayar.Smtp ?? "mail.kurumsaleposta.com";
                                        smtp.Port = ayar.Port ?? 587;
                                        smtp.UseDefaultCredentials = false;
                                        smtp.Credentials = new NetworkCredential(ayar.Adres?.Trim(), ayar.Sifre?.Trim());
                                        smtp.EnableSsl = ayar.SslDurum ?? false;
                                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                                        smtp.Timeout = 15000;

                                        using (var mailMsg = new MailMessage())
                                        {
                                            mailMsg.From = new MailAddress(ayar.Adres?.Trim(), uygulama);
                                            mailMsg.To.Add(eposta);
                                            mailMsg.Subject = konu;
                                            mailMsg.Body = icerik;
                                            mailMsg.IsBodyHtml = true;
                                            mailMsg.BodyEncoding = System.Text.Encoding.UTF8;
                                            mailMsg.SubjectEncoding = System.Text.Encoding.UTF8;

                                            await smtp.SendMailAsync(mailMsg);
                                        }
                                    }

                                    // Başarılı gönderim sonrası durumu ve tarihi güncelle
                                    var bgMailLog = await bgYbsContext.tb_Mail.FindAsync(mailId);
                                    if (bgMailLog != null)
                                    {
                                        bgMailLog.Durum = true;
                                        bgMailLog.GonTarih = DateTime.Now;
                                        await bgYbsContext.SaveChangesAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Background email sending failed for MailID: {MailID}", mailId);

                                    // Hata bilgisini veritabanına logla
                                    using (var errScope = _scopeFactory.CreateScope())
                                    {
                                        try
                                        {
                                            var errYbsContext = errScope.ServiceProvider.GetRequiredService<YbsDbContext>();
                                            var errMailLog = await errYbsContext.tb_Mail.FindAsync(mailId);
                                            if (errMailLog != null)
                                            {
                                                errMailLog.TryCount = (errMailLog.TryCount ?? 0) + 1;
                                                errMailLog.HataKodu = ex.Message.Length > 500 ? ex.Message.Substring(0, 500) : ex.Message;
                                                await errYbsContext.SaveChangesAsync();
                                            }
                                        }
                                        catch (Exception dbEx)
                                        {
                                            _logger.LogError(dbEx, "Failed to write email sending error to database for MailID: {MailID}", mailId);
                                        }
                                    }
                                }
                            }
                        });
                    }
                    // Eğer CalismaSekli "SERVIS" ise, hiçbir şey tetiklemeden doğrudan metottan çıkıyoruz (Windows Servis kuyruğu).
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendMailAsync initialization error");
            }
        }

        public async Task SendSmsAsync(string uygulama, string konu, string icerik, string tlf, string adSoyad)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var ybsContext = scope.ServiceProvider.GetRequiredService<YbsDbContext>();

                    var smsLog = new tb_Sms
                    {
                        Alan = adSoyad,
                        AlanTlf = tlf,
                        Gonderen = uygulama,
                        Icerik = icerik,
                        Konu = konu,
                        Durum = false,
                        TryCount = 0,
                        KayitTarih = DateTime.Now
                    };
                    ybsContext.tb_Sms.Add(smsLog);
                    await ybsContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSmsAsync error");
            }
        }
    }
}
