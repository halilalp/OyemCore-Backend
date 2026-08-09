using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Mağaza avans-masraf onay iş akışı. referans: WebServiceAvansMasraf
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AvansMasrafController : ControllerBase
    {
        private readonly IAvansMasrafService _svc;

        public AvansMasrafController(IAvansMasrafService svc)
        {
            _svc = svc;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        // Avans oluştur/güncelle
        [HttpPost("avans")]
        public IActionResult AvansKaydet([FromBody] AvansRequest req)
            => Ok(_svc.AvansKaydet(GetCurrentUserId(), req.ID, req.Tutar, req.Aciklama));

        // Masraf oluştur/güncelle (kalemli)
        [HttpPost("masraf")]
        public IActionResult MasrafKaydet([FromBody] MasrafRequest req)
            => Ok(_svc.MasrafKaydet(GetCurrentUserId(), req.ID, req.ToplamTutar, req.Aciklama, req.IliskiliAvansID, req.Kalemler));

        // Kendi avans taleplerim
        [HttpGet("avanslar")]
        public IActionResult AvansListesi() => Ok(_svc.AvansListesiGetir(GetCurrentUserId()));

        // Kendi masraf taleplerim
        [HttpGet("masraflar")]
        public IActionResult MasrafListesi() => Ok(_svc.MasrafListesiGetir(GetCurrentUserId()));

        // Masraf detay (kalemler)
        [HttpGet("masraf/{masrafID}")]
        public IActionResult MasrafDetay(int masrafID) => Ok(_svc.MasrafDetayGetir(GetCurrentUserId(), masrafID));

        // Onay bekleyen talepler (onay kutum)
        [HttpGet("onay-bekleyenler")]
        public IActionResult OnayBekleyenler() => Ok(_svc.OnayBekleyenTaleplerGetir(GetCurrentUserId()));

        // Onayla / reddet
        [HttpPost("onayla-reddet")]
        public IActionResult OnaylaReddet([FromBody] OnayRequest req)
            => Ok(_svc.AvansMasrafOnaylaReddet(GetCurrentUserId(), req.Tip, req.ID, req.Onay, req.Aciklama));

        public class AvansRequest { public int ID { get; set; } public decimal Tutar { get; set; } public string Aciklama { get; set; } }
        public class MasrafRequest
        {
            public int ID { get; set; }
            public decimal ToplamTutar { get; set; }
            public string Aciklama { get; set; }
            public int? IliskiliAvansID { get; set; }
            public List<MasrafKalemDto> Kalemler { get; set; }
        }
        public class OnayRequest { public string Tip { get; set; } public int ID { get; set; } public bool Onay { get; set; } public string Aciklama { get; set; } }
    }
}
