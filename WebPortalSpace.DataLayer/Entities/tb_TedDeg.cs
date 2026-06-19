using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TedDeg
    {
        public int TedDegID { get; set; }
        public string BelgeNo { get; set; }
        public string TedarikciKodu { get; set; }
        public string TurKod { get; set; }
        public DateTime? IstekTar { get; set; }
        public DateTime? GelisTar { get; set; }
        public short? MahsulYil { get; set; }
        public DateTime? KayitTar { get; set; }
        public string Aciklama { get; set; }
        public string KayitSicil { get; set; }
        public bool? Durum { get; set; }
        public double? KalitePuani { get; set; }
        public double? FiyatPuani { get; set; }
        public double? TerminPuani { get; set; }
        public double? BelgePuani { get; set; }
        public double? ToplamPuan { get; set; }
        public string Sinif { get; set; }
        public string RiskDurum { get; set; }
        public int? TerminGunFark { get; set; }
    }
}
