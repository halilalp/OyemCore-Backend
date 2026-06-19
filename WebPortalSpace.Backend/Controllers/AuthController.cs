using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Interfaces;

namespace WebPortalSpace.Backend.Controllers
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
        /// Kullanıcının giriş yapmasını sağlar ve başarılı ise JWT token döner.
        /// </summary>
        /// <param name="request">Kullanıcı adı ve şifre bilgilerini içeren model.</param>
        /// <returns>Giriş başarılı ise JWT token, değilse yetkisiz hatası döner.</returns>
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Kullanıcı adı ve şifre boş olamaz." });
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
        /// Kullanıcının şifresini sıfırlamak için talep gönderir.
        /// </summary>
        /// <param name="request">Şifre sıfırlama için gerekli sicil no ve kullanıcı adını içeren model.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.SicilNo) || string.IsNullOrEmpty(request.Username))
            {
                return BadRequest(new { message = "Sicil numarası ve kullanıcı adı boş olamaz." });
            }

            var result = _authService.ResetPassword(request.SicilNo, request.Username);

            if (result.Success)
            {
                return Ok(new { message = result.Message });
            }

            return BadRequest(new { message = result.Message });
        }

        /// <summary>
        /// Giriş yapmış olan kullanıcının mobil bildirim (push notification) token bilgisini kaydeder.
        /// </summary>
        /// <param name="request">Kaydedilecek push token bilgisini içeren model.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [Authorize]
        [HttpPost("push-token")]
        public IActionResult SavePushToken([FromBody] PushTokenRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new { message = "Push token boş olamaz." });
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
        /// Giriş yapmış olan kullanıcının mobil bildirim (push notification) token bilgisini temizler.
        /// </summary>
        /// <returns>İşlemin başarı durumunu döner.</returns>
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
        /// Test amaçlı push notification gönderir.
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
            return Ok(new { message = $"SicilNo {sicilNo} için push bildirim tetiklendi." });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
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
