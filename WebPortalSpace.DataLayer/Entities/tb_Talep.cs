using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Talep
    {
        public int TalepID { get; set; }
        public string TalepTurKodu { get; set; }
        public string TalepKodu { get; set; }
        public int? KategoriID { get; set; }
        public int? AltKategoriID { get; set; }
        public string Konu { get; set; }
        public string Aciklama { get; set; }
        public string OnemSeviye { get; set; }
        public string KayitSicil { get; set; }
        public string KayitEposta { get; set; }
        public DateTime? KayitTar { get; set; }
        public string DosyaUrl { get; set; }
        public string SorumluSicil { get; set; }
        public string SorumluEposta { get; set; }
        public bool? Durum { get; set; }
        public DateTime? KapanmaTar { get; set; }
        public double? IslemSure { get; set; }
        public double? Skor { get; set; }
        public bool? Kilitli { get; set; }
        public DateTime? KilitTarihi { get; set; }
        public double? KilitSure { get; set; }
        public int? TalepPuan { get; set; }
        public int? MttrTamamSure { get; set; }
        public int? MtbfAralikSure { get; set; }
        public int? DurusSure { get; set; }
    }
}
