using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Uygulama-içi bildirim merkezi. referans: WebServiceBildirim
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BildirimController : ControllerBase
    {
        private readonly IBildirimService _bildirimService;

        public BildirimController(IBildirimService bildirimService)
        {
            _bildirimService = bildirimService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        // Sayfalı bildirim listesi. referans: GetNotifications(pageIndex, pageSize)
        [HttpGet]
        public IActionResult GetNotifications([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (total, data) = _bildirimService.GetNotifications(GetCurrentUserId(), pageIndex, pageSize);
                return Ok(new { success = true, total, data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = true, message = ex.Message });
            }
        }

        // Okunmamış bildirim sayısı (rozet). referans: GetUnreadNotificationCount
        [HttpGet("unread-count")]
        public IActionResult GetUnreadCount()
        {
            try
            {
                int count = _bildirimService.GetUnreadCount(GetCurrentUserId());
                return Ok(new { success = true, count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = true, message = ex.Message });
            }
        }

        // Tek bildirimi okundu işaretle. referans: MarkAsRead
        [HttpPost("{id}/read")]
        public IActionResult MarkAsRead(int id)
        {
            try
            {
                _bildirimService.MarkAsRead(GetCurrentUserId(), id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = true, message = ex.Message });
            }
        }

        // Tümünü okundu işaretle. referans: MarkAllAsRead
        [HttpPost("read-all")]
        public IActionResult MarkAllAsRead()
        {
            try
            {
                _bildirimService.MarkAllAsRead(GetCurrentUserId());
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = true, message = ex.Message });
            }
        }

        // Bildirim sil. referans: DeleteNotification
        [HttpDelete("{id}")]
        public IActionResult DeleteNotification(int id)
        {
            try
            {
                _bildirimService.DeleteNotification(GetCurrentUserId(), id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = true, message = ex.Message });
            }
        }
    }
}
