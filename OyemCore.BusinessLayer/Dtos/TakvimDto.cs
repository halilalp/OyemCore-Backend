using System;

namespace OyemCore.BusinessLayer.Dtos
{
    public class TakvimDto
    {
        public int TakvimID { get; set; }
        public int? AyarID { get; set; }
        public int? MasterID { get; set; }
        public string? Konu { get; set; }
        public string? KayitSicil { get; set; }
        public string? KayitAdSoyad { get; set; }   // KayitSicil → tb_Personel.AdSoyad (popup başlığında "(...)" için)
        public DateTime? BasTar { get; set; }
        public DateTime? BitTar { get; set; }
        public string? Katilimci { get; set; }
        public string? Aciklama { get; set; }
        
        // Joined details from tb_TakvimAyar (optional)
        public string? KategoriAdi { get; set; }   // tb_TakvimAyar.Konu — etkinlik türü (ör. "Yıllık İzin")
        public string? BgColor { get; set; }
        public string? BrColor { get; set; }

        // Recurrence options
        public string? Periyot { get; set; }
        public int? TekrarSayisi { get; set; }
    }
}
