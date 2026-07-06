using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.Backend.Controllers
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
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        /// <summary>
        /// Giris yapmis kullanicinin kendi izin taleplerini ve kalan izin g?n bakiyesini getirir.
        /// </summary>
        /// <returns>Izin talepleri listesi ve kalan g?n bakiyesini i?eren nesne d?ner.</returns>
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
        /// Giris yapmis kullanicinin onaylamasi (amir olarak) bekleyen izin taleplerini getirir.
        /// </summary>
        /// <returns>Onay bekleyen izin talepleri listesini d?ner.</returns>
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
        /// Yeni bir izin talebi olusturur ve onaya g?nderir.
        /// </summary>
        /// <param name="request">Olusturulacak izin talebi detaylarini i?eren nesne.</param>
        /// <returns>Islemin basari durumunu ve sonucunu d?ner.</returns>
        [HttpPost]
        public IActionResult SubmitRequest([FromBody] tb_IzinOnay request)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _izinService.SaveIzinRequest(userId, request);
                return Ok(new { success, message = "Izin talebi basariyla olusturuldu." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kendisine onay i?in g?nderilmis bir izin talebini onaylar.
        /// </summary>
        /// <param name="id">Onaylanacak izin talebinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
        [HttpPost("{id}/approve")]
        public IActionResult Approve(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool success = _izinService.ApproveIzinRequest(userId, id);
                return Ok(new { success, message = "Talep basariyla onaylandi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kendisine onay i?in g?nderilmis bir izin talebini reddeder.
        /// </summary>
        /// <param name="id">Reddedilecek izin talebinin ID degeri.</param>
        /// <returns>Islemin basari durumunu d?ner.</returns>
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
        /// Belge numarasina ait izin onay tarih?esini getirir.
        /// </summary>
        /// <param name="belgeNo">Izin talebinin belge numarasi.</param>
        /// <returns>Izin talebi tarih?e listesini d?ner.</returns>
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
