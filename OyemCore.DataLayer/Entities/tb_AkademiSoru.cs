using System;

namespace OyemCore.DataLayer.Entities
{
    // Akademi sinav soru bankasi — bir egitime bagli, havuzdan rastgele orneklenir (Faz 2).
    public class tb_AkademiSoru
    {
        public int SoruID { get; set; }
        public int AkademiEgitimID { get; set; }
        public string SoruMetni { get; set; } = null!;
        public string SecenekA { get; set; } = null!;
        public string SecenekB { get; set; } = null!;
        public string? SecenekC { get; set; }
        public string? SecenekD { get; set; }
        public string DogruSecenek { get; set; } = null!; // "A" | "B" | "C" | "D"
        public int Puan { get; set; }
        public bool AktifMi { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
