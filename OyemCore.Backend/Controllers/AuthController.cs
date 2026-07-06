using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Sistemdeki şirketlerin (tenant) listesini döner.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("sirketler")]
        public IActionResult GetTenants()
        {
            try
            {
                var tenants = _authService.GetTenants();
                return Ok(tenants);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kullanicinin giris yapmasini saglar ve basarili ise JWT token d?ner.
        /// </summary>
        /// <param name="request">Kullanici adi ve sifre bilgilerini i?eren model.</param>
        /// <returns>Giris basarili ise JWT token, degilse yetkisiz hatasi d?ner.</returns>
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Kullanici adi ve sifre bos olamaz." });
            }

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Bilinmiyor";
            if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                ipAddress = forwardedFor.ToString().Split(',')[0].Trim();
            }

            string userAgent = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Bilinmiyor";

            var result = _authService.Authenticate(request.Username, request.Password, ipAddress, userAgent);

            if (result.Success)
            {
                return Ok(new { token = result.Token, message = result.Message });
            }

            return Unauthorized(new { message = result.Message });
        }

        /// <summary>
        /// Kullanicinin sifresini sifirlamak i?in talep g?nderir.
        /// </summary>
        /// <param name="request">??ifre sifirlama i?in gerekli sicil no ve kullanici adini i?eren model.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.SicilNo) || string.IsNullOrEmpty(request.Username))
            {
                return BadRequest(new { message = "Sicil numarasi ve kullanici adi bos olamaz." });
            }

            var result = _authService.ResetPassword(request.SicilNo, request.Username);

            if (result.Success)
            {
                return Ok(new { message = result.Message });
            }

            return BadRequest(new { message = result.Message });
        }

        /// <summary>
        /// Giris yapmis olan kullanicinin mobil bildirim (push notification) token bilgisini kaydeder.
        /// </summary>
        /// <param name="request">Kaydedilecek push token bilgisini i?eren model.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [Authorize]
        [HttpPost("push-token")]
        public IActionResult SavePushToken([FromBody] PushTokenRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new { message = "Push token bos olamaz." });
            }

            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                _authService.SavePushToken(userId, request.Token);
                return Ok(new { success = true, message = "Push token kaydedildi." });
            }

            return Unauthorized();
        }

        /// <summary>
        /// Giris yapmis olan kullanicinin mobil bildirim (push notification) token bilgisini temizler.
        /// </summary>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [Authorize]
        [HttpPost("clear-push-token")]
        public IActionResult ClearPushToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                _authService.ClearPushToken(userId);
                return Ok(new { success = true, message = "Push token temizlendi." });
            }

            return Unauthorized();
        }

        /// <summary>
        /// Test ama?li push notification g?nderir.
        /// </summary>
        [HttpGet("test-push")]
        public async System.Threading.Tasks.Task<IActionResult> TestPush(
            [FromServices] IPushNotificationService pushNotificationService,
            [FromQuery] string sicilNo,
            [FromQuery] string title = "Test Bildirimi",
            [FromQuery] string body = "Bu bir test bildirimdir. Tebrikler!")
        {
            if (string.IsNullOrEmpty(sicilNo))
            {
                return BadRequest(new { message = "sicilNo parametresi zorunludur." });
            }

            await pushNotificationService.SendToUserBySicilNoAsync(sicilNo, title, body, new { screen = "HomeScreen" });
            return Ok(new { message = $"SicilNo {sicilNo} i?in push bildirim tetiklendi." });
        }

        [AllowAnonymous]
        [HttpGet("encrypt-config")]
        public IActionResult EncryptConfig([FromQuery] string plainText, [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(plainText)) return BadRequest("plainText bos olamaz");
            var key = configuration["Encryption:TenantKey"];
            var encrypted = OyemCore.BusinessLayer.Common.SecurityHelper.EncryptString(plainText, key);
            return Ok(new { plainText, encrypted, key });
        }

        [AllowAnonymous]
        [HttpGet("test-db-connection")]
        public IActionResult TestDbConnection([FromServices] OyemCore.DataLayer.Contexts.MasterDbContext masterDbContext)
        {
            try
            {
                var count = masterDbContext.Tenants.Count();
                var connStr = masterDbContext.Database.GetDbConnection().ConnectionString;
                // Mask password in conn string for security
                var maskedConnStr = System.Text.RegularExpressions.Regex.Replace(connStr, @"Password=[^;]+", "Password=***");
                return Ok(new { success = true, count, connectionString = maskedConnStr });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message, detail = ex.ToString() });
            }
        }

        [AllowAnonymous]
        [HttpGet("test-tenant-connection")]
        public IActionResult TestTenantConnection([FromQuery] string tenantId, [FromServices] OyemCore.BusinessLayer.Interfaces.ITenantService tenantService, [FromServices] OyemCore.DataLayer.Contexts.MasterDbContext masterDbContext)
        {
            try
            {
                var tenant = masterDbContext.Tenants.FirstOrDefault(t => t.TenantId == tenantId);
                if (tenant == null) return NotFound(new { message = $"Tenant {tenantId} bulunamadi" });

                var encryptionKey = masterDbContext.Database.GetDbConnection().ConnectionString; // wait, no, configuration
                // Let's just use the tenantService to decrypt it
                HttpContext.Request.Headers["X-Tenant-Id"] = tenantId;
                var connStr = tenantService.GetCurrentConnectionString();

                if (string.IsNullOrEmpty(connStr))
                {
                    return Ok(new { success = false, message = "Connection string cozulemedi veya bos." });
                }

                var maskedConnStr = System.Text.RegularExpressions.Regex.Replace(connStr, @"Password=[^;]+", "Password=***");
                Console.WriteLine($"[DIAGNOSTIC] Decrypted ConnString: '{connStr}' (Masked: '{maskedConnStr}')");

                // Try to open connection
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM tb_Kullanici";
                        var count = cmd.ExecuteScalar();
                        return Ok(new { success = true, userCount = count, connectionString = maskedConnStr });
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message, detail = ex.ToString() });
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string SirketKodu { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string SicilNo { get; set; }
        public string Username { get; set; }
    }

    public class PushTokenRequest
    {
        public string Token { get; set; }
    }
}
