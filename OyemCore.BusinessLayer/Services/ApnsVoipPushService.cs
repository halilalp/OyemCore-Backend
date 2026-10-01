using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Interfaces;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

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

        public async Task<(bool Success, string Detail)> SendCallWakeAsync(string deviceToken, bool isProduction, string callerSicilNo, string callerName, string roomUrl, string callType, string callerImage)
        {
            if (string.IsNullOrEmpty(deviceToken)) return (false, "deviceToken bos.");
            if (string.IsNullOrEmpty(_keyPath) || string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_teamId) || string.IsNullOrEmpty(_bundleId))
            {
                _logger.LogWarning("ApnsVoipPushService: ApplePush yapilandirmasi eksik, gonderim atlandi.");
                return (false, "ApplePush yapilandirmasi (KeyPath/KeyId/TeamId/BundleId) eksik.");
            }
            if (!File.Exists(_keyPath))
            {
                _logger.LogWarning("ApnsVoipPushService: anahtar dosyasi bulunamadi ({KeyPath}), gonderim atlandi.", _keyPath);
                return (false, $"Anahtar dosyasi bulunamadi: {_keyPath}");
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
                    return (false, $"APNs HTTP {(int)response.StatusCode} ({(isProduction ? "production" : "sandbox")}, topic {_bundleId}.voip). Response: {responseBody}, token: {deviceToken.Substring(0, Math.Min(12, deviceToken.Length))}...");
                }
                else
                {
                    _logger.LogInformation("ApnsVoipPushService: VoIP push gonderildi. SicilNo (arayan): {CallerSicilNo}", callerSicilNo);
                    return (true, $"APNs {(isProduction ? "production" : "sandbox")} kabul etti (HTTP {(int)response.StatusCode}).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApnsVoipPushService: SendCallWakeAsync basarisiz.");
                // TEŞHİS: File.Exists(_keyPath) TRUE donuyor ama sonrasinda "dosya bulunamadi" hatasi
                // aliniyordu (2026-09-22) — ex.Message tek basina yetersizdi, tam tip+stack ekleniyor.
                return (false, $"Exception ({ex.GetType().FullName}): {ex.Message} | CWD: {Directory.GetCurrentDirectory()} | KeyPath: {_keyPath} | Stack: {ex.StackTrace?.Substring(0, Math.Min(200, ex.StackTrace.Length))}");
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

                // KOK NEDEN (2026-09-22): System.Security.Cryptography.ECDsa, Windows'ta CNG
                // uzerinden calisiyor — bu da IIS Application Pool'un "Load User Profile" ayari
                // acik olmadan CryptographicException ("dosya bulunamadi") firlatiyordu. Paylasimli
                // hosting'lerde bu ayara erisim genelde yok. BouncyCastle, Windows CNG'ye hic
                // dokunmayan, saf yonetilen (managed) bir kripto kutuphanesi — ayni sorunu asla
                // yasamaz. "PLAIN-ECDSA" imzalayici, JWT ES256'nin gerektirdigi ham IEEE P1363
                // ("r || s" sabit uzunluk) formatini DOGRUDAN uretir — DER'den donusum gerekmez.
                ECPrivateKeyParameters privateKey;
                using (var reader = new StringReader(File.ReadAllText(_keyPath)))
                {
                    var pemReader = new PemReader(reader);
                    object pemObject = pemReader.ReadObject();
                    privateKey = pemObject as ECPrivateKeyParameters
                        ?? ((AsymmetricCipherKeyPair)pemObject).Private as ECPrivateKeyParameters;
                }

                var signer = SignerUtilities.GetSigner("SHA256withPLAIN-ECDSA");
                signer.Init(true, privateKey);
                byte[] signingInputBytes = Encoding.UTF8.GetBytes(signingInput);
                signer.BlockUpdate(signingInputBytes, 0, signingInputBytes.Length);
                byte[] signature = signer.GenerateSignature();

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
