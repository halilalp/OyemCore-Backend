using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_BakimPerKontrolDetay
    {
        public int ID { get; set; }
        public string KontrolKodu { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
        public string IslemNotu { get; set; }
        public string DosyaUrl { get; set; }
    }
}
