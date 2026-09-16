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

                                    // Dinamik şirket adı, logosu ve tema rengi belirleme
                                    var sirketAdi = "OYEMCORE";
                                    var sirketLogo = "https://www.isiktarim.com/imza/imza_logo.png"; // Varsayılan/ortak imza logo
                                    var temaRengi = "#1e3a8a"; // Koyu Mavi

                                    var gondericiAdres = ayar.Adres?.Trim().ToLowerInvariant() ?? "";
                                    if (gondericiAdres.Contains("@isiktarim.com"))
                                    {
                                        sirketAdi = "IŞIK TARIM";
                                        sirketLogo = "https://www.isiktarim.com/imza/imza_logo.png";
                                        temaRengi = "#006324"; // Koyu Yeşil
                                    }
                                    else if (gondericiAdres.Contains("@oyemsoft.com"))
                                    {
                                        sirketAdi = "OYEMSOFT";
                                        sirketLogo = "https://www.isiktarim.com/imza/imza_logo.png";
                                        temaRengi = "#1e3a8a"; // Mavi
                                    }
                                    else if (gondericiAdres.Contains("@alfemo.com"))
                                    {
                                        sirketAdi = "ALFEMO";
                                        sirketLogo = "https://www.isiktarim.com/imza/imza_logo.png";
                                        temaRengi = "#e11d48"; // Rose/Kırmızı
                                    }

                                    // Modern şablonla mail gövdesini sarmala
                                    string modernBody = GetModernMailTemplate(uygulama, konu, icerik, mailId.ToString(), sirketAdi, sirketLogo, temaRengi);

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
                                            mailMsg.Body = modernBody;
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

        private string GetModernMailTemplate(string gonderen, string konu, string icerik, string mailId, string sirketAdi, string sirketLogo, string temaRengi)
        {
            return $@"
<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""utf-8"" />
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>{konu}</title>
    <style>
        body {{
            margin: 0;
            padding: 0;
            background-color: #f3f4f6;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            -webkit-font-smoothing: antialiased;
        }}
        .wrapper {{
            width: 100%;
            background-color: #f3f4f6;
            padding: 24px 0;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 16px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
            overflow: hidden;
            border: 1px solid #e5e7eb;
        }}
        .header {{
            background-color: {temaRengi};
            padding: 24px;
            color: #ffffff;
        }}
        .header-title {{
            margin: 0;
            font-size: 20px;
            font-weight: 700;
            letter-spacing: -0.5px;
        }}
        .content {{
            padding: 32px 24px;
            color: #1f2937;
        }}
        .content-subject {{
            margin-top: 0;
            margin-bottom: 16px;
            font-size: 18px;
            font-weight: 600;
            color: #111827;
        }}
        .content-body {{
            font-size: 15px;
            line-height: 1.6;
            color: #4b5563;
        }}
        .content-body b, .content-body strong {{
            color: #111827;
        }}
        .footer {{
            background-color: #f9fafb;
            padding: 24px;
            text-align: center;
            border-top: 1px solid #f3f4f6;
            color: #6b7280;
        }}
        .footer-text {{
            font-size: 13px;
            margin: 0 0 8px 0;
            font-weight: 600;
            color: #4b5563;
        }}
        .footer-subtext {{
            font-size: 12px;
            margin: 0 0 4px 0;
        }}
        .footer-id {{
            font-size: 11px;
            color: #9ca3af;
            margin: 0;
        }}
        .signature {{
            padding: 24px 0 8px 0;
            text-align: center;
        }}
        .signature img {{
            height: 70px;
            object-fit: contain;
        }}
    </style>
</head>
<body>
    <div class=""wrapper"">
        <div class=""container"">
            <div class=""header"">
                <h1 class=""header-title"">{gonderen}</h1>
            </div>
            <div class=""content"">
                <h2 class=""content-subject"">{konu}</h2>
                <div class=""content-body"">
                    {icerik}
                </div>
            </div>
            <div class=""footer"">
                <p class=""footer-text"">{sirketAdi} Mail Servisi - Bilgi İşlem</p>
                <p class=""footer-subtext"">{DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>
                <p class=""footer-id"">İşlem No: {mailId}</p>
            </div>
            <div class=""signature"">
                <img src=""{sirketLogo}"" alt=""{sirketAdi} Signature"" />
            </div>
        </div>
    </div>
</body>
</html>";
        }
    }
}
