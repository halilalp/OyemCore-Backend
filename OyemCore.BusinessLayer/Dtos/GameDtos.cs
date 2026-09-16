namespace OyemCore.BusinessLayer.Dtos
{
    // Liderlik tablosu satırı. referans: WebServiceGames.GetLeaderboards
    public class LeaderboardEntryDto
    {
        public string AdSoyad { get; set; }
        public string Departman { get; set; }
        public string SicilNo { get; set; }
        public int TotalScore { get; set; }
        public int GamesPlayed { get; set; }
        public double SuccessRate { get; set; }   // kazanma yüzdesi
        public double AverageScore { get; set; }  // yalnızca "average" tablosunda kullanılır
    }
}
