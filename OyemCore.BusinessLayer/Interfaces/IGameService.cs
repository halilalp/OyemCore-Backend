namespace OyemCore.BusinessLayer.Interfaces
{
    // Kelime oyunu (Wordle benzeri) — sadece oynama + liderlik. referans: WebServiceGames.
    // Admin / AI / kelime havuzu yönetimi web'de kalır; mobile aktarılmaz.
    public interface IGameService
    {
        // Kullanıcının bugünkü oyun durumu (aktif oyun / yeni oyun / tamamlanmış).
        object GetGameState(int kullaniciID);
        // Bir tahmin gönder; feedback + skor döner.
        object SubmitGuess(int kullaniciID, string guess);
        // Liderlik tabloları (günlük / aylık / genel / ortalama).
        object GetLeaderboards();
    }
}
