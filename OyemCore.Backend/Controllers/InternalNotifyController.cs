using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Sunucudan sunucuya (server-to-server) çağrılar için — eski web uygulamasının (YBSSolution1)
    // ticket/talep oluşturma noktalarından mobil push bildirimi tetiklemesi amacıyla eklendi.
    // Kullanıcı oturumu gerektirmez; X-Internal-Api-Key header'ı ile korunur.
    // Modül başına tek action: içerideki EventType alanına göre ilgili PushNotificationService
    // metoduna yönlendirilir — HTTP yüzeyini gereksiz yere büyütmemek için.
    [Route("api/internal/notify")]
    [ApiController]
    [AllowAnonymous]
    public class InternalNotifyController : ControllerBase
    {
        private readonly IPushNotificationService _pushService;
        private readonly IConfiguration _configuration;

        public InternalNotifyController(IPushNotificationService pushService, IConfiguration configuration)
        {
            _pushService = pushService;
            _configuration = configuration;
        }

        private bool IsAuthorized()
        {
            if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided))
            {
                return false;
            }

            var expected = _configuration["Internal:ApiKey"];
            return !string.IsNullOrEmpty(expected) && provided.ToString() == expected;
        }

        [HttpPost("ticket")]
        public async Task<IActionResult> Ticket([FromBody] TicketNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewTicketAsync(dto.TicketId);
                    break;
                case "sorumluatandi":
                    await _pushService.NotifyTicketSorumluAtandiAsync(dto.TicketId, dto.ActionUserId);
                    break;
                case "statuschanged":
                    await _pushService.NotifyTicketStatusChangedAsync(dto.TicketId, dto.OldStatus, dto.NewStatus, dto.ActionUserId);
                    break;
                case "gelisme":
                    await _pushService.NotifyTicketGelismeAsync(dto.TicketId, dto.ActionUserId, dto.Comment);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("talep")]
        public async Task<IActionResult> Talep([FromBody] TalepNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewTalepAsync(dto.TalepId);
                    break;
                case "sorumluatandi":
                    await _pushService.NotifyTalepSorumluAtandiAsync(dto.TalepId);
                    break;
                case "gelisme":
                    await _pushService.NotifyTalepGelismeAsync(dto.TalepId, dto.ActionUserId, dto.Description);
                    break;
                case "closed":
                    await _pushService.NotifyTalepClosedAsync(dto.TalepId);
                    break;
                case "onayagonderildi":
                    await _pushService.NotifyTalepOnayaGonderildiAsync(dto.TalepId, dto.OnayciSicil);
                    break;
                case "onaylandi":
                    await _pushService.NotifyTalepOnaylandiAsync(dto.TalepId, dto.ActionUserId);
                    break;
                case "reddedildi":
                    await _pushService.NotifyTalepReddedildiAsync(dto.TalepId, dto.ActionUserId, dto.Description);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("tedarikci")]
        public async Task<IActionResult> Tedarikci([FromBody] TedarikciNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewTedarikciDegerlendirmeAsync(dto.BelgeNo);
                    break;
                case "completed":
                    await _pushService.NotifyTedarikciDegerlendirmeCompletedAsync(dto.BelgeNo);
                    break;
                case "cancelled":
                    await _pushService.NotifyTedarikciDegerlendirmeCancelledAsync(dto.BelgeNo);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }

        [HttpPost("izin")]
        public async Task<IActionResult> Izin([FromBody] IzinNotifyDto dto)
        {
            if (!IsAuthorized()) return Unauthorized();
            if (dto == null) return BadRequest();

            switch ((dto.EventType ?? "").ToLowerInvariant())
            {
                case "created":
                    await _pushService.NotifyNewLeaveRequestAsync(dto.IzinOnayId);
                    break;
                case "amironaylaricompleted":
                    await _pushService.NotifyLeaveManagerApprovalsCompletedAsync(dto.IzinOnayId);
                    break;
                case "rejected":
                    await _pushService.NotifyLeaveRequestRejectedAsync(dto.IzinOnayId, dto.ActionUserId);
                    break;
                case "completed":
                    await _pushService.NotifyLeaveRequestCompletedAsync(dto.IzinOnayId, dto.ActionUserId);
                    break;
                default:
                    return BadRequest(new { message = $"Bilinmeyen eventType: {dto.EventType}" });
            }

            return Ok(new { success = true });
        }
    }

    // eventType: "created" | "amirOnaylariCompleted" | "rejected" | "completed"
    public class IzinNotifyDto
    {
        public string EventType { get; set; }
        public int IzinOnayId { get; set; }
        public int ActionUserId { get; set; }
    }

    // eventType: "created" | "sorumluAtandi" | "statusChanged" | "gelisme"
    public class TicketNotifyDto
    {
        public string EventType { get; set; }
        public int TicketId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public int ActionUserId { get; set; }
        public string Comment { get; set; }
    }

    // eventType: "created" | "sorumluAtandi" | "gelisme" | "closed" | "onayaGonderildi" | "onaylandi" | "reddedildi"
    // - onayaGonderildi: OnayciSicil doldurulmalı (onayı verecek kişi)
    // - reddedildi: Description alanı red sebebi olarak kullanılır
    public class TalepNotifyDto
    {
        public string EventType { get; set; }
        public int TalepId { get; set; }
        public int ActionUserId { get; set; }
        public string Description { get; set; }
        public string OnayciSicil { get; set; }
    }

    // eventType: "created" | "completed" | "cancelled"
    public class TedarikciNotifyDto
    {
        public string EventType { get; set; }
        public string BelgeNo { get; set; }
    }
}
