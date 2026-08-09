using System;

namespace OyemCore.DataLayer.Entities
{
    // Mağaza avans talebi. referans: webportal tb_MagazaAvans / WebServiceAvansMasraf
    public class tb_MagazaAvans
    {
        public int AvansID { get; set; }
        public string BelgeNo { get; set; }
        public string TalepEdenSicil { get; set; }
        public DateTime TalepTarihi { get; set; }
        public decimal Tutar { get; set; }
        public string Aciklama { get; set; }
        public string SurecDurum { get; set; }   // ONAYDA / ONAYLANDI / REDDEDILDI / ODENDI / KAPATILDI
        public string BekleyenOnay { get; set; }  // bekleyen onaycı sicil
        public decimal KalanBakiye { get; set; }
    }
}
