using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Satın Alma (SAT talep / SAS sipariş). Faz A: dashboard, talep listesi, taslak, kalem.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SatSasController : ControllerBase
    {
        private readonly ISatSasService _service;

        public SatSasController(ISatSasService service)
        {
            _service = service;
        }

        private string GetSicilNo()
        {
            var claim = User.FindFirst("SicilNo");
            if (claim != null && !string.IsNullOrEmpty(claim.Value)) return claim.Value;
            throw new UnauthorizedAccessException("Giris yapan kullanicinin Sicil Numarasi bulunamadi.");
        }

        private string GetAdminBelgeTur()
        {
            var claim = User.FindFirst("AdminBelgeTur");
            return claim != null ? claim.Value : "";
        }

        private string GetEposta()
        {
            var claim = User.FindFirst(ClaimTypes.Email);
            return claim != null ? claim.Value : "";
        }

        [HttpGet("dashboard")]
        public IActionResult GetDashboard()
        {
            try { return Ok(_service.GetDashboard(GetSicilNo(), GetAdminBelgeTur())); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("sat-requests")]
        public IActionResult GetSatRequests()
        {
            try { return Ok(new { data = _service.GetSatRequests(GetSicilNo(), GetAdminBelgeTur()) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("sat-draft")]
        public IActionResult CheckOrCreateSatDraft()
        {
            try { return Ok(_service.CheckOrCreateSatDraft(GetSicilNo(), GetEposta())); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("sat-detail/{belgeNo}")]
        public IActionResult GetSatDetail(string belgeNo)
        {
            try
            {
                var detay = _service.GetSatDetail(belgeNo);
                if (detay == null) return NotFound(new { message = "Talep bulunamadı." });
                return Ok(detay);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("sat-item")]
        public IActionResult AddSatItem([FromBody] AddSatItemRequest req)
        {
            try { return Ok(_service.AddSatItem(req.BelgeNo, req.MalzemeKodu, req.Miktar, req.BirimKodu, req.Neden)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("sat-item/{satKalemID}")]
        public IActionResult DeleteSatItem(int satKalemID)
        {
            try
            {
                bool ok = _service.DeleteSatItem(satKalemID);
                if (!ok) return NotFound(new { message = "Kalem bulunamadı." });
                return Ok(new { success = true });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("sat-header")]
        public IActionResult SaveSatHeader([FromBody] SatHeaderRequest req)
        {
            try { return Ok(new { success = _service.SaveSatHeader(GetSicilNo(), req.Konu, req.Aciklama) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("sat-submit")]
        public IActionResult SubmitSatRequest([FromBody] SatHeaderRequest req)
        {
            try { return Ok(new { success = _service.SubmitSatRequest(GetSicilNo(), req.Konu, req.Aciklama) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
    }

    public class AddSatItemRequest
    {
        public string BelgeNo { get; set; }
        public string MalzemeKodu { get; set; }
        public decimal Miktar { get; set; }
        public string BirimKodu { get; set; }
        public string Neden { get; set; }
    }

    public class SatHeaderRequest
    {
        public string Konu { get; set; }
        public string Aciklama { get; set; }
    }
}
