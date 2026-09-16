using System;

namespace OyemCore.DataLayer.Entities
{
    // Kelime oyunu kullanıcı skoru (günlük oyun kaydı). referans: webportal tb_GameScore
    public class tb_GameScore
    {
        public int Id { get; set; }
        public string SicilNo { get; set; }
        public string AdSoyad { get; set; }
        public string Departman { get; set; }
        public DateTime GameDate { get; set; }        // date (gün)
        public int PlayNumber { get; set; }
        public int Score { get; set; }
        public int AttemptsCount { get; set; }
        public string Guesses { get; set; }           // virgülle ayrık tahminler (BÜYÜK harf)
        public bool IsCompleted { get; set; }
        public DateTime PlayDate { get; set; }
        public string TargetWord { get; set; }        // şifreli (ClsEncryption)
        public string TargetMeaning { get; set; }     // düz metin anlam
    }
}
