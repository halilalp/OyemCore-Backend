using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Kullanici
    {
        public int KullaniciID { get; set; }
        public string? KullaniciAdi { get; set; }
        public string? Sifre { get; set; }
        public string? AdSoyad { get; set; }
        public string? Eposta { get; set; }
        public string? SicilNo { get; set; }
        public string? Tel1 { get; set; }
        public string? AdminBelgeTur { get; set; }
        public bool? Durum { get; set; }
        public string? GirisSekli { get; set; }
        public DateTime? SonGirisTar { get; set; }
        public string? Unvan { get; set; }
        public string? DepartmanKod { get; set; }
        public bool? Yonetici { get; set; }
        public bool? ZimmetSorumlusu { get; set; }
        public DateTime? KayitTar { get; set; }
        public double? YillikIzin { get; set; }
        public int? DefaultProje { get; set; }
        public char? Cinsiyet { get; set; }
        public string? PushToken { get; set; }
    }
}
