using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DepoController : ControllerBase
    {
        private readonly IYbsDbContext _context;

        public DepoController(IYbsDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Kullanici kimligi dogrulanamadi.");
        }

        private string GetAdminBelgeTur() => User.FindFirst("AdminBelgeTur")?.Value ?? "";
        private bool HasStokAdmin() => GetAdminBelgeTur().ToUpperInvariant().Contains("ADMIN");

        // Kullanicinin yetkili oldugu depo kodlari (web GetAuthorizedWarehouses karsiligi).
        // Depo erisimi HERKES icin (STOKADMIN dahil) yalnizca tb_DepoSorumlusu atamalarindan gelir;
        // admin bypass'i YOKTUR (referansla ayni). Depo yetkisi Admin/DepoSorumlulari uzerinden atanir.
        private List<string> GetAuthorizedWarehouses()
        {
            var sicil = _context.tb_Kullanici.AsNoTracking()
                .Where(u => u.KullaniciID == GetCurrentUserId())
                .Select(u => u.SicilNo).FirstOrDefault();
            if (string.IsNullOrEmpty(sicil)) return new List<string>();

            return _context.tb_DepoSorumlusu.AsNoTracking()
                .Where(o => o.SorumluSicilNo == sicil)
                .Select(o => o.DepoKodu).ToList();
        }

        // Yetkili depolar (dropdown / filtre)
        [HttpGet("list")]
        public IActionResult GetList([FromQuery] bool sadeceAktif = false)
        {
            try
            {
                var auth = GetAuthorizedWarehouses();
                var query = _context.tb_Depo.AsNoTracking().Where(d => auth.Contains(d.DepoKodu));
                if (sadeceAktif) query = query.Where(d => d.Aktif == true);

                var list = query.OrderBy(d => d.DepoKodu)
                    .Select(d => new { ID = d.DepoKodu, DepoKod = d.DepoKodu, DepoAd = d.DepoAdi, DepoTipi = d.DepoTipiKodu, Aktif = d.Aktif })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        public class DepoSaveRequest
        {
            public string Kodu { get; set; }
            public string Adi { get; set; }
            public string Tipi { get; set; }
            public bool Aktif { get; set; } = true;
        }

        [HttpPost("save")]
        public IActionResult Save([FromBody] DepoSaveRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Kodu) || string.IsNullOrWhiteSpace(req.Adi))
                return BadRequest(new { message = "Depo kodu ve adi zorunludur." });
            try
            {
                var d = _context.tb_Depo.FirstOrDefault(o => o.DepoKodu == req.Kodu);
                if (d == null)
                {
                    d = new tb_Depo { DepoKodu = req.Kodu };
                    _context.tb_Depo.Add(d);
                }
                d.DepoAdi = req.Adi;
                d.DepoTipiKodu = req.Tipi;
                d.Aktif = req.Aktif;
                _context.SaveChanges();
                return Ok(new { success = true, message = "Islem basarili." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpDelete("{kodu}")]
        public IActionResult Delete(string kodu)
        {
            try
            {
                // Bakiye kontrolu: depoda stok varsa silinemez
                if (_context.tb_DepoMalzeme.Any(o => o.DepoKodu == kodu && o.Miktar > 0))
                    return BadRequest(new { message = "Bu depoda stok bulunmaktadir, silinemez." });

                var depo = _context.tb_Depo.FirstOrDefault(o => o.DepoKodu == kodu);
                if (depo == null) return NotFound(new { message = "Depo bulunamadi." });

                _context.tb_Depo.Remove(depo);
                _context.SaveChanges();
                return Ok(new { success = true, message = "Depo silindi." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }
    }
}
