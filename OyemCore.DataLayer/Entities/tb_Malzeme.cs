using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Malzeme
    {
        public string MalzemeKodu { get; set; }
        public string MalzemeAdi { get; set; }
        public string BirimKodu { get; set; }
        public bool? Aktif { get; set; }
        public bool? SatinAlinabilir { get; set; }
        public string MalzemeTipKodu { get; set; }
        public string MalzemeGrupKodu { get; set; }

        // Malzeme yonetimi (mobil gecis) icin eklenen alanlar. Hepsi nullable -
        // mevcut bakim tarafi sorgularini (searchMalzemes) etkilemez.
        public string ParentMalzemeKodu { get; set; }
        public bool? Uretilebilir { get; set; }
        public bool? Satilabilir { get; set; }
        public bool? StokTakip { get; set; }
        public bool? LotTakibi { get; set; }
        public DateTime? KayitTar { get; set; }
        public int? KdvOrani { get; set; }
        public decimal? AlisFiyati { get; set; }
        public decimal? SatisFiyati { get; set; }
        public string ParaBirimi { get; set; }
        public string Barkod { get; set; }
        public decimal? En { get; set; }
        public decimal? Boy { get; set; }
        public decimal? Yukseklik { get; set; }
        public decimal? Agirlik { get; set; }
        public string Marka { get; set; }
        public string Model { get; set; }
        public string KoleksiyonKodu { get; set; }
        public string Ek1 { get; set; }
        public string Ek2 { get; set; }
        public string Ek3 { get; set; }
        public string Ek4 { get; set; }
    }
}
