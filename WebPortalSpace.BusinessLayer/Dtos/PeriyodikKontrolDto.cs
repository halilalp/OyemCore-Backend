using System;

namespace WebPortalSpace.BusinessLayer.Dtos
{
    public class PeriyodikKontrolDto
    {
        public int ID { get; set; }
        public string KontrolKodu { get; set; }
        public string BolumKodu { get; set; }
        public string BolumAdi { get; set; }
        public string SirketAdi { get; set; }
        public string KontrolTuru { get; set; }
        public string Durum { get; set; }
        public string Aciklama { get; set; }
        public string KayitSicil { get; set; }
        public string KayitPersonelAd { get; set; }
        public string IslemSicil { get; set; }
        public string IslemPersonelAd { get; set; }
        public string HedefBaslangicStr { get; set; }
        public string HedefBitisStr { get; set; }
        public string BaslamaTarStr { get; set; }
        public string BitisTarStr { get; set; }
        public string KayitTarStr { get; set; }
    }
}
