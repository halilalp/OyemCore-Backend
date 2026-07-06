using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_BakimPerKontrolSarfiyat
    {
        public int ID { get; set; }
        public string KontrolKodu { get; set; }
        public string MalzemeKodu { get; set; }
        public decimal Miktar { get; set; }
        public string MakineKodu { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
