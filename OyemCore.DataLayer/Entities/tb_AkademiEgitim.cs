using System;

namespace OyemCore.DataLayer.Entities
{
    // Akademi'nin kendi içerik tablosu — tb_Egitim'le hiçbir ilişkisi yok, kasıtlı olarak ayrı.
    public class tb_AkademiEgitim
    {
        public int AkademiEgitimID { get; set; }
        public string Baslik { get; set; } = null!;
        public string? Aciklama { get; set; }
        public string? KategoriKodu { get; set; }
        public string IcerikTipi { get; set; } = null!; // "Video" | "Dokuman"
        public string DosyaUrl { get; set; } = null!;
        public int? SureSaniye { get; set; }
        public bool AktifMi { get; set; }
        public string OlusturanSicil { get; set; } = null!;
        public DateTime KayitTarihi { get; set; }

        // Faz 2 — sinav ayarlari (parametrik, yonetici atama sirasinda degil icerik
        // tanimlarken belirler; her egitimin en fazla bir sinavi olur).
        public bool SinavAktif { get; set; }
        public int? SinavSoruSayisi { get; set; } // havuzdan orneklenecek soru adedi (null = tumu)
        public int SinavSoruSuresiSaniye { get; set; } // soru basina sunucu-yetkili geri sayim
        public int GecmePuanYuzdesi { get; set; } // basarili sayilma esigi (%)
        public bool KameraKaydiZorunlu { get; set; } // altyapi hazir, Faz 5'e kadar kullanilmiyor
    }
}
