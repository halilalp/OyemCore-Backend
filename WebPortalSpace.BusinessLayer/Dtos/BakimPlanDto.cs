using System;

namespace WebPortalSpace.BusinessLayer.Dtos
{
    public class BakimPlanDto
    {
        public int ID { get; set; }
        public string PlanKodu { get; set; }
        public string HatKodu { get; set; }
        public string HatAdi { get; set; }
        public string BolumAdi { get; set; }
        public string SirketAdi { get; set; }
        public string BakimTuru { get; set; }
        public string Durum { get; set; }
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
