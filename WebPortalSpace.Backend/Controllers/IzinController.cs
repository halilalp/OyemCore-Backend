using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;

namespace WebPortalSpace.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class IzinController : ControllerBase
    {
        private readonly IIzinService _izinService;

        public IzinController(IIzinService izinService)
        {
            _izinService = izinService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("Giriş yapan kullanıcı kimliği doğrulanamadı.");
        }

        /// <summary>
        /// Giriş yapmış kullanıcının kendi izin taleplerini ve kalan izin gün bakiyesini getirir.
        /// </summary>
        /// <returns>İzin talepleri listesi ve kalan gün bakiyesini içeren nesne döner.</returns>
        [HttpGet]
        public IActionResult GetRequests()
        {
            try
            {
                int userId = GetCurrentUserId();
                var (requests, balance) = _izinService.GetIzinRequests(userId);
                return Ok(new { requests, balance });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Giriş yapmış kullanıcının onaylaması (amir olarak) bekleyen izin taleplerini getirir.
        /// </summary>
        /// <returns>Onay bekleyen izin talepleri listesini döner.</returns>
        [HttpGet("approvals")]
        public IActionResult GetApprovals()
        {
            try
            {
                int userId = GetCurrentUserId();
                var approvals = _izinService.GetIzinApprovals(userId);
                return Ok(approvals);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir izin talebi oluşturur ve onaya gönderir.
        /// </summary>
        /// <param name="request">Oluşturulacak izin talebi detaylarını içeren nesne.</param>
        /// <returns>İşlemin başarı durumunu ve sonucunu döner.</returns>
        [HttpPost]
        public IActionResult SubmitRequest([FromBody] tb_IzinOnay request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _izinService.SaveIzinRequest(userId, request);
                return Ok(new { success, message = "İzin talebi başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kendisine onay için gönderilmiş bir izin talebini onaylar.
        /// </summary>
        /// <param name="id">Onaylanacak izin talebinin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/approve")]
        public IActionResult Approve(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _izinService.ApproveIzinRequest(userId, id);
                return Ok(new { success, message = "Talep başarıyla onaylandı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kendisine onay için gönderilmiş bir izin talebini reddeder.
        /// </summary>
        /// <param name="id">Reddedilecek izin talebinin ID değeri.</param>
        /// <returns>İşlemin başarı durumunu döner.</returns>
        [HttpPost("{id}/reject")]
        public IActionResult Reject(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _izinService.RejectIzinRequest(userId, id);
                return Ok(new { success, message = "Talep reddedildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Belge numarasına ait izin onay tarihçesini getirir.
        /// </summary>
        /// <param name="belgeNo">İzin talebinin belge numarası.</param>
        /// <returns>İzin talebi tarihçe listesini döner.</returns>
        [HttpGet("{belgeNo}/history")]
        public IActionResult GetHistory(string belgeNo)
        {
            try
            {
                var history = _izinService.GetIzinHistory(belgeNo);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
