using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_BakimPerKontrol
    {
        public int ID { get; set; }
        public string KontrolKodu { get; set; }
        public string BolumKodu { get; set; }
        public string KontrolTuru { get; set; }
        public DateTime? HedefBaslangic { get; set; }
        public DateTime? HedefBitis { get; set; }
        public string Durum { get; set; }
        public string Aciklama { get; set; }
        public DateTime? BaslamaTar { get; set; }
        public DateTime? BitisTar { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
        public string IslemSicil { get; set; }
    }
}
