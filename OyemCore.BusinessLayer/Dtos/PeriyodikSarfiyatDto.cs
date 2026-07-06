using System;

namespace OyemCore.BusinessLayer.Dtos
{
    public class PeriyodikSarfiyatDto
    {
        public int ID { get; set; }
        public string KontrolKodu { get; set; }
        public string MalzemeKodu { get; set; }
        public string MalzemeAdi { get; set; }
        public decimal Miktar { get; set; }
        public string Birim { get; set; }
        public string MakineKodu { get; set; }
        public string MakineAdi { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
