using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_TalepSoruCevap
    {
        public int TalepSoruCevapID { get; set; }
        public string TalepKodu { get; set; }
        public int? SoruTalepGelismeID { get; set; }
        public string Sicil { get; set; }
        public string Eposta { get; set; }
        public int? CevapTalepGelismeID { get; set; }
        public double? Sure { get; set; }
    }
}
