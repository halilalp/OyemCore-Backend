using System;

namespace OyemCore.DataLayer.Entities
{
    // Bakım planına ait malzeme sarfiyatı (referans: WebServiceBakimPlani.BakimSarfiyat*).
    public class tb_BakimSarfiyat
    {
        public int ID { get; set; }
        public string PlanKodu { get; set; }
        public string MalzemeKodu { get; set; }
        public decimal Miktar { get; set; }
        public string MakineKodu { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
