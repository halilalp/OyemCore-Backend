using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // Kelime oyunu (oyna + liderlik). referans: WebServiceGames — .NET 7 API portu.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IGameService _game;

        public GamesController(IGameService game)
        {
            _game = game;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        // Bugünkü oyun durumu (aktif / yeni / tamamlanmış).
        [HttpGet("state")]
        public IActionResult GetGameState() => Ok(_game.GetGameState(GetCurrentUserId()));

        // Bir tahmin gönder.
        [HttpPost("guess")]
        public IActionResult SubmitGuess([FromBody] GuessRequest req) => Ok(_game.SubmitGuess(GetCurrentUserId(), req?.Guess));

        // Liderlik tabloları.
        [HttpGet("leaderboards")]
        public IActionResult GetLeaderboards() => Ok(_game.GetLeaderboards());

        public class GuessRequest { public string Guess { get; set; } }
    }
}
