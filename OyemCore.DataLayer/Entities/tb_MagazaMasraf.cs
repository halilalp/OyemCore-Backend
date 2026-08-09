using System;

namespace OyemCore.DataLayer.Entities
{
    // Mağaza masraf talebi. referans: webportal tb_MagazaMasraf
    public class tb_MagazaMasraf
    {
        public int MasrafID { get; set; }
        public string BelgeNo { get; set; }
        public string TalepEdenSicil { get; set; }
        public DateTime TalepTarihi { get; set; }
        public decimal ToplamTutar { get; set; }
        public string Aciklama { get; set; }
        public string SurecDurum { get; set; }   // ONAYDA / ONAYLANDI / REDDEDILDI / ODENDI / KAPATILDI
        public string BekleyenOnay { get; set; }
        public int? IliskiliAvansID { get; set; } // ilişkili avans (mahsup)
    }
}
