using System;

namespace OyemCore.DataLayer.Entities
{
    // Masraf kalemi (fiş satırı). referans: webportal tb_MagazaMasrafDetay
    public class tb_MagazaMasrafDetay
    {
        public int DetayID { get; set; }
        public int MasrafID { get; set; }
        public string FisNo { get; set; }
        public string Firma { get; set; }
        public DateTime? Tarih { get; set; }
        public decimal Tutar { get; set; }
        public decimal KdvTutar { get; set; }
        public string Aciklama { get; set; }
        public string DosyaYolu { get; set; }
        public string OcrRawJson { get; set; }
    }
}
