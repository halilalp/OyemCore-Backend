using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Personel
    {
        public string SicilNo { get; set; }
        public string AdSoyad { get; set; }
        public string Eposta { get; set; }
        public bool? Durum { get; set; }
        public string SirketKodu { get; set; }
        public string Unvan { get; set; }
        public string DepartmanKodu { get; set; }
        public string Telefon { get; set; }
        public string Cinsiyet { get; set; }
        public DateTime? IseBasTar { get; set; }
        public string Departman { get; set; }
        public string DahiliNo { get; set; }
        public DateTime? DogumTar { get; set; }
        // İK dashboard (IKDashboardVerisiGetir) için: yaka tipi (MY/BY/GY), çıkış tarihi, ünvan kodu
        public string MyBy { get; set; }
        public DateTime? IstenCikisTar { get; set; }
        public string UnvanKodu { get; set; }
    }
}
