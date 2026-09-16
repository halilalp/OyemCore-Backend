using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Anket — sadece oylama. referans: WebServiceAnket — .NET 7 API portu.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AnketController : ControllerBase
    {
        private readonly IAnketService _anket;

        public AnketController(IAnketService anket)
        {
            _anket = anket;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        // Aktif + katılınmamış anketler (ana ekran).
        [HttpGet("active")]
        public IActionResult GetActive() => Ok(_anket.GetActiveSurveys(GetCurrentUserId()));

        // Anketin oylama formu.
        [HttpGet("{anketID}/detail")]
        public IActionResult GetDetail(int anketID) => Ok(_anket.GetSurveyDetail(GetCurrentUserId(), anketID));

        // Oy gönder.
        [HttpPost("vote")]
        public IActionResult Vote([FromBody] VoteRequest req)
            => Ok(_anket.SubmitVote(GetCurrentUserId(), req?.AnketID ?? 0, req?.Answers));

        public class VoteRequest
        {
            public int AnketID { get; set; }
            public List<AnketAnswer> Answers { get; set; }
        }
    }
}
