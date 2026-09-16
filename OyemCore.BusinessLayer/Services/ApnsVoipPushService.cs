using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class ApnsVoipPushService : IApnsVoipPushService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApnsVoipPushService> _logger;
        private readonly string _keyPath;
        private readonly string _keyId;
        private readonly string _teamId;
        private readonly string _bundleId;

        // Apple, aynı .p8 anahtarıyla üretilen JWT'yi 20 dakikadan sık yeniden üretmemeyi öneriyor
        // (rate limit'e takılmamak için) — 50 dakikaya kadar önbellekte tutup tekrar kullanıyoruz.
        private string _cachedJwt;
        private DateTime _cachedJwtAt = DateTime.MinValue;
        private readonly SemaphoreSlim _jwtLock = new SemaphoreSlim(1, 1);

        public ApnsVoipPushService(HttpClient httpClient, IConfiguration configuration, ILogger<ApnsVoipPushService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _keyPath = configuration["ApplePush:KeyPath"];
            _keyId = configuration["ApplePush:KeyId"];
            _teamId = configuration["ApplePush:TeamId"];
            _bundleId = configuration["ApplePush:BundleId"];
        }

        public async Task SendCallWakeAsync(string deviceToken, bool isProduction, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage)
        {
            if (string.IsNullOrEmpty(deviceToken)) return;
            if (string.IsNullOrEmpty(_keyPath) || string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_teamId) || string.IsNullOrEmpty(_bundleId))
            {
                _logger.LogWarning("ApnsVoipPushService: ApplePush yapilandirmasi eksik, gonderim atlandi.");
                return;
            }
            if (!File.Exists(_keyPath))
            {
                _logger.LogWarning("ApnsVoipPushService: anahtar dosyasi bulunamadi ({KeyPath}), gonderim atlandi.", _keyPath);
                return;
            }

            try
            {
                string jwt = await GetOrCreateJwtAsync();

                // VoIP push için apns-topic her zaman "<bundleId>.voip" (düz bundle ID DEĞİL) —
                // Apple bunu Faz 2'nin en kolay atlanan detaylarından biri olarak belgeliyor.
                string host = isProduction ? "https://api.push.apple.com" : "https://api.sandbox.push.apple.com";
                string url = $"{host}/3/device/{deviceToken}";

                var payload = new
                {
                    aps = new { },
                    type = "call",
                    callerSicilNo = callerSicilNo ?? "",
                    callerName = callerName ?? "",
                    roomUrl = roomUrl ?? "",
                    callType = callType ?? "video",
                    callerImage = callerImage ?? "",
                };
                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(payload, jsonOptions);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    // APNs SADECE HTTP/2 kabul ediyor.
                    Version = new Version(2, 0),
                    VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact,
                };
                request.Headers.Add("authorization", $"bearer {jwt}");
                request.Headers.Add("apns-topic", $"{_bundleId}.voip");
                request.Headers.Add("apns-push-type", "voip");
                request.Headers.Add("apns-priority", "10");
                request.Headers.Add("apns-expiration", "0");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("ApnsVoipPushService: APNs gonderimi basarisiz. HTTP: {StatusCode}, Response: {Response}", response.StatusCode, responseBody);
                }
                else
                {
                    _logger.LogInformation("ApnsVoipPushService: VoIP push gonderildi. SicilNo (arayan): {CallerSicilNo}", callerSicilNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApnsVoipPushService: SendCallWakeAsync basarisiz.");
            }
        }

        private async Task<string> GetOrCreateJwtAsync()
        {
            if (_cachedJwt != null && (DateTime.UtcNow - _cachedJwtAt) < TimeSpan.FromMinutes(50))
                return _cachedJwt;

            await _jwtLock.WaitAsync();
            try
            {
                if (_cachedJwt != null && (DateTime.UtcNow - _cachedJwtAt) < TimeSpan.FromMinutes(50))
                    return _cachedJwt;

                var header = new { alg = "ES256", kid = _keyId };
                var payload = new { iss = _teamId, iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

                string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
                string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
                string signingInput = $"{headerB64}.{payloadB64}";

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(File.ReadAllText(_keyPath));

                // JWT (RFC 7518) ES256 imzası "r || s" ham (IEEE P1363) formatında olmalı —
                // .NET'in varsayılan DER/ASN.1 formatı Apple tarafından reddedilir.
                byte[] signature = ecdsa.SignData(
                    Encoding.UTF8.GetBytes(signingInput),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

                string signatureB64 = Base64UrlEncode(signature);

                _cachedJwt = $"{signingInput}.{signatureB64}";
                _cachedJwtAt = DateTime.UtcNow;
                return _cachedJwt;
            }
            finally
            {
                _jwtLock.Release();
            }
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
