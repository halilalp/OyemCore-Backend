using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // referans: webportal ChatHub.CreateDailyRoom — .NET 7 HttpClient portu.
    // Daily.co REST API ile public bir oda açar, 2 saat sonra süresi dolar.
    public class DailyCallService : IDailyCallService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public DailyCallService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<string> CreateRoomAsync()
        {
            string apiKey = _config["Daily:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_DAILY_API_KEY_HERE")
                throw new Exception("appsettings.json içinde Daily:ApiKey tanımlı değil.");

            string roomName = "IsikTarim_" + Guid.NewGuid().ToString("N");
            long expEpoch = (long)(DateTime.UtcNow.AddHours(2) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            // Oda özellikleri: public, kamera+mikrofon açık, Daily prebuilt chat kapalı.
            var body = new
            {
                name = roomName,
                privacy = "public",
                properties = new
                {
                    exp = expEpoch,
                    enable_chat = false,
                    start_audio_off = false,
                    start_video_off = false
                }
            };

            using (var req = new HttpRequestMessage(HttpMethod.Post, "https://api.daily.co/v1/rooms"))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(req);
                string responseText = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Daily.co API Hatası: " + responseText);

                using (var doc = JsonDocument.Parse(responseText))
                {
                    if (doc.RootElement.TryGetProperty("url", out var urlProp))
                        return urlProp.GetString();
                }
                throw new Exception("Daily.co API yanıtından 'url' alanı okunamadı.");
            }
        }
    }
}
