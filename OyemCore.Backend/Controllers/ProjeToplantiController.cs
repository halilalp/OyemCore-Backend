using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Proje / Toplantı yönetimi. Faz 1 — liste + detay.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProjeToplantiController : ControllerBase
    {
        private readonly IProjeToplantiService _service;

        public ProjeToplantiController(IProjeToplantiService service)
        {
            _service = service;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        [HttpGet]
        public IActionResult GetList([FromQuery] string konu, [FromQuery] string durum, [FromQuery] string tur)
        {
            try { return Ok(new { data = _service.GetList(GetUserId(), konu, durum, tur) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("{id}")]
        public IActionResult GetDetail(int id)
        {
            try
            {
                var detay = _service.GetDetail(GetUserId(), id);
                if (detay == null) return NotFound(new { message = "Kayıt bulunamadı." });
                return Ok(detay);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost]
        public IActionResult Create([FromBody] ProjeCreateRequest req)
        {
            try
            {
                int id = _service.Create(GetUserId(), req.Tur, req.ProjeTur, req.Konu, req.Aciklama,
                    req.BasTarih, req.BitTarih, req.Katilimcilar);
                return Ok(new { success = true, id });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id}/durum")]
        public IActionResult UpdateDurum(int id, [FromBody] DurumRequest req)
        {
            try { _service.UpdateDurum(GetUserId(), id, req.Durum); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id}/gorev")]
        public IActionResult AddGorev(int id, [FromBody] GorevRequest req)
        {
            try
            {
                int gorevId = _service.AddGorev(GetUserId(), id, req.Aciklama, req.SorumluEposta,
                    req.TerminTar, req.BaslamaTar, req.Trl);
                return Ok(new { success = true, id = gorevId });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("gorev/{gorevId}/tamamla")]
        public IActionResult CompleteGorev(int gorevId)
        {
            try { _service.CompleteGorev(GetUserId(), gorevId); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("gorev/{gorevId}")]
        public IActionResult DeleteGorev(int gorevId)
        {
            try { _service.DeleteGorev(GetUserId(), gorevId); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id}/katilimci")]
        public IActionResult AddKatilimci(int id, [FromBody] KatilimciRequest req)
        {
            try { _service.AddKatilimci(GetUserId(), id, req.Eposta); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("katilimci/{katilimciId}")]
        public IActionResult RemoveKatilimci(int katilimciId)
        {
            try { _service.RemoveKatilimci(GetUserId(), katilimciId); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("personeller")]
        public IActionResult GetAktifPersoneller([FromQuery] string arama)
        {
            try { return Ok(_service.GetAktifPersoneller(arama)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id}/dosya")]
        public IActionResult AddDosya(int id, [FromBody] DosyaRequest req)
        {
            try { _service.AddDosya(GetUserId(), id, req.Baslik, req.DosyaUrl); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("dosya/{dosyaId}")]
        public IActionResult DeleteDosya(int dosyaId)
        {
            try { _service.DeleteDosya(GetUserId(), dosyaId); return Ok(new { success = true }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
    }

    public class ProjeCreateRequest
    {
        public string Tur { get; set; }
        public string ProjeTur { get; set; }
        public string Konu { get; set; }
        public string Aciklama { get; set; }
        public string BasTarih { get; set; }
        public string BitTarih { get; set; }
        public System.Collections.Generic.List<string> Katilimcilar { get; set; }
    }

    public class DurumRequest { public bool Durum { get; set; } }
    public class KatilimciRequest { public string Eposta { get; set; } }
    public class DosyaRequest { public string Baslik { get; set; } public string DosyaUrl { get; set; } }
    public class GorevRequest
    {
        public string Aciklama { get; set; }
        public string SorumluEposta { get; set; }
        public string TerminTar { get; set; }
        public string BaslamaTar { get; set; }
        public string Trl { get; set; }
    }
}
