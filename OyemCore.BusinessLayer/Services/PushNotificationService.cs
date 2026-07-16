using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(IServiceScopeFactory scopeFactory, HttpClient httpClient, ILogger<PushNotificationService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task SendToUserBySicilNoAsync(string sicilNo, string title, string body, object data = null)
        {
            if (string.IsNullOrEmpty(sicilNo)) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var user = await context.tb_Kullanici
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.SicilNo == sicilNo);

                    string pushToken = user?.PushToken;

                    if (!string.IsNullOrEmpty(pushToken))
                    {
                        await SendExpoNotificationAsync(pushToken, title, body, data);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendToUserBySicilNoAsync failed for SicilNo {SicilNo}", sicilNo);
            }
        }

        public async Task SendToUserByKullaniciIdAsync(int kullaniciId, string title, string body, object data = null)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var user = await context.tb_Kullanici
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.KullaniciID == kullaniciId);

                    string pushToken = user?.PushToken;

                    if (!string.IsNullOrEmpty(pushToken))
                    {
                        await SendExpoNotificationAsync(pushToken, title, body, data);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendToUserByKullaniciIdAsync failed for KullaniciID {KullaniciId}", kullaniciId);
            }
        }

        private async Task SendExpoNotificationAsync(string pushToken, string title, string body, object data)
        {
            if (!pushToken.StartsWith("ExponentPushToken["))
            {
                _logger.LogWarning("PushNotificationService: Invalid Expo push token format: {Token}", pushToken);
                return;
            }

            try
            {
                var payload = new
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
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("PushNotificationService: Expo server returned error status code {StatusCode}. Response: {Response}", response.StatusCode, responseBody);
                }
                else
                {
                    _logger.LogInformation("PushNotificationService: Notification sent successfully to token {Token}", pushToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: SendExpoNotificationAsync failed for token {Token}", pushToken);
            }
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
                        $"{requesterName} yeni bir izin talebi olusturdu. Onayiniz bekleniyor.",
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
                        "İzin talebinizin amir onay süreci tamamlandı, İK işlemi bekleniyor.",
                        new { type = "izin", screen = "IzinScreen", code = leave.BelgeNo }
                    );

                    // Notify HR / "IZIN" roles users
                    var hrUsers = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.AdminBelgeTur != null && (u.AdminBelgeTur.Contains("IZIN") || u.AdminBelgeTur.Contains("IK") || u.AdminBelgeTur.Contains("ADMIN")))
                        .ToListAsync();

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

                    string companyCode = null;
                    if (talep.TalepTurKodu == "BAKIM")
                    {
                        companyCode = await context.tb_TalepBakim
                            .AsNoTracking()
                            .Where(tb => tb.TalepKodu == talep.TalepKodu)
                            .Select(tb => tb.SirketKodu)
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        companyCode = await context.tb_Personel
                            .AsNoTracking()
                            .Where(p => p.SicilNo == talep.KayitSicil)
                            .Select(p => p.SirketKodu)
                            .FirstOrDefaultAsync();
                    }

                    var requesterName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == talep.KayitSicil)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? talep.KayitSicil;

                    string tur = talep.TalepTurKodu.ToUpper();
                    
                    var managers = await (from u in context.tb_Kullanici
                                          join p in context.tb_Personel on u.SicilNo equals p.SicilNo
                                          where p.SirketKodu == companyCode &&
                                                u.AdminBelgeTur != null &&
                                                (u.AdminBelgeTur.ToUpper().Contains(tur) || u.AdminBelgeTur.ToUpper().Contains("ADMIN"))
                                          select u)
                                         .AsNoTracking()
                                         .ToListAsync();

                    if (!managers.Any())
                    {
                        managers = await context.tb_Kullanici
                            .AsNoTracking()
                            .Where(u => u.AdminBelgeTur != null && 
                                        (u.AdminBelgeTur.ToUpper().Contains(tur) || u.AdminBelgeTur.ToUpper().Contains("ADMIN")))
                            .ToListAsync();
                    }

                    string typeLabel = tur == "BAKIM" ? "Bakim" : tur;
                    string title = $"Yeni {typeLabel} Talebi";
                    string body = $"{requesterName} tarafından yeni bir {typeLabel.ToLower()} talebi ({talep.TalepKodu}) açıldı.";

                    foreach (var manager in managers)
                    {
                        if (manager.SicilNo == talep.KayitSicil) continue; // Skip creator
                        await SendToUserBySicilNoAsync(
                            manager.SicilNo,
                            title,
                            body,
                            new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                        );
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
                        $"{typeLabel} Talebinize Uzman Atandi",
                        $"'{talep.Konu}' konulu talebinize ({talep.TalepKodu}) sorumlu uzman atandi: {sorumluName}.",
                        new { type = talep.TalepTurKodu, screen = "TalepScreen", code = talep.TalepKodu, id = talep.TalepID }
                    );

                    await SendToUserBySicilNoAsync(
                        talep.SorumluSicil,
                        $"Yeni {typeLabel} Talebi Atandi",
                        $"'{talep.Konu}' konulu talep ({talep.TalepKodu}) size atandi.",
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
                    var yetkililer = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.AdminBelgeTur != null &&
                                    (u.AdminBelgeTur.ToUpper().Contains("TEDARIKCI") || u.AdminBelgeTur.ToUpper().Contains("ADMIN")))
                        .ToListAsync();

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

                    await SendToUserBySicilNoAsync(
                        log.PersonelSicil,
                        "Üzerinize Yeni Zimmet Atandı",
                        $"{senderName} tarafından üzerinize '{asset.Tanim}' demirbaşı zimmetlendi.",
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

                    await SendToUserBySicilNoAsync(
                        log.PersonelSicil,
                        "Zimmet Iade Alindi",
                        $"Üzerinizdeki '{asset.Tanim}' demirbaşı {receiverName} tarafından iade alındı ve zimmetiniz düşürüldü.",
                        new { type = "zimmet", screen = "ZimmetlerimScreen", id = log.AygitID }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationService: NotifyAssetReturnedAsync failed for ID {ID}", aygitPersonelId);
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

                    var admins = await context.tb_Kullanici
                        .AsNoTracking()
                        .Where(u => u.AdminBelgeTur != null && (u.AdminBelgeTur.Contains("TICKET") || u.AdminBelgeTur.Contains("ADMIN")))
                        .ToListAsync();

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

        public async Task NotifyTicketSorumluAtandiAsync(int ticketId)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IYbsDbContext>();
                    var ticket = await context.tb_Ticket.AsNoTracking().FirstOrDefaultAsync(t => t.ID == ticketId);
                    if (ticket == null || string.IsNullOrEmpty(ticket.SorumluSicilNo)) return;

                    var sorumluName = await context.tb_Personel
                        .AsNoTracking()
                        .Where(p => p.SicilNo == ticket.SorumluSicilNo)
                        .Select(p => p.AdSoyad)
                        .FirstOrDefaultAsync() ?? ticket.SorumluSicilNo;

                    await SendToUserBySicilNoAsync(
                        ticket.KayitSicilNo,
                        "Ticketiniza Sorumlu Atandi",
                        $"'{ticket.Baslik}' baslikli destek talebinize ({ticket.TakipKodu}) sorumlu atandi: {sorumluName}.",
                        new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                    );

                    await SendToUserBySicilNoAsync(
                        ticket.SorumluSicilNo,
                        "Size Yeni Ticket Atandi",
                        $"'{ticket.Baslik}' baslikli ticket ({ticket.TakipKodu}) size atandi.",
                        new { type = "ticket", screen = "TicketScreen", id = ticket.ID }
                    );
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

