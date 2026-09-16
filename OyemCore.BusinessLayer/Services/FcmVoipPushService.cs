using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class FcmVoipPushService : IFcmVoipPushService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FcmVoipPushService> _logger;
        private readonly string _projectId;
        private readonly GoogleCredential _credential;

        public FcmVoipPushService(HttpClient httpClient, IConfiguration configuration, ILogger<FcmVoipPushService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _projectId = configuration["Firebase:ProjectId"];

            // Servis hesabı JSON dosyası henüz sunucuya konulmamış olabilir (ör. Faz 1 kodu deploy
            // edildi ama kimlik bilgisi henüz kopyalanmadı) — bu durumda sessizce devre dışı kal,
            // diğer push yollarını (Expo) ETKİLEME. Her SendCallWakeAsync çağrısı bunu kontrol eder.
            var keyPath = configuration["Firebase:ServiceAccountKeyPath"];
            try
            {
                if (!string.IsNullOrEmpty(keyPath) && System.IO.File.Exists(keyPath))
                {
                    _credential = GoogleCredential.FromFile(keyPath)
                        .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                }
                else
                {
                    _logger.LogWarning("FcmVoipPushService: servis hesabi dosyasi bulunamadi ({KeyPath}) - FCM VoIP push devre disi.", keyPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FcmVoipPushService: GoogleCredential yuklenemedi.");
            }
        }

        public async Task SendCallWakeAsync(string fcmToken, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage)
        {
            if (_credential == null || string.IsNullOrEmpty(_projectId))
            {
                _logger.LogWarning("FcmVoipPushService: kimlik bilgisi/proje ID yok, gonderim atlandi.");
                return;
            }
            if (string.IsNullOrEmpty(fcmToken)) return;

            try
            {
                string accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

                // Data-only mesaj (notification alanı YOK) — Android'de sistemin kendi bildirimini
                // göstermesini istemiyoruz, react-native-callkeep kendi tam ekran arayüzünü açacak.
                var payload = new
                {
                    message = new
                    {
                        token = fcmToken,
                        data = new
                        {
                            type = "call",
                            callerSicilNo = callerSicilNo ?? "",
                            callerName = callerName ?? "",
                            roomUrl = roomUrl ?? "",
                            callType = callType ?? "video",
                            callerImage = callerImage ?? "",
                        },
                        android = new
                        {
                            priority = "high",
                        },
                    }
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(payload, jsonOptions);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FcmVoipPushService: FCM gonderimi basarisiz. HTTP: {StatusCode}, Response: {Response}", response.StatusCode, responseBody);
                }
                else
                {
                    _logger.LogInformation("FcmVoipPushService: Arama uyandirma mesaji gonderildi. SicilNo (arayan): {CallerSicilNo}", callerSicilNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FcmVoipPushService: SendCallWakeAsync basarisiz.");
            }
        }
    }
}
