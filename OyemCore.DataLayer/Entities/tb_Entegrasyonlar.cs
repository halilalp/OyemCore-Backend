using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Entegrasyonlar
    {
        public int ServisID { get; set; }
        public string ServisKodu { get; set; }
        public string ServisAdi { get; set; }
        public string ServisTipi { get; set; }
        public string EndpointUrl { get; set; }
        public string KullaniciAdi { get; set; }
        public string Sifre { get; set; }
        public int? CalismaPeriyoduDakika { get; set; }
        public DateTime? SonCalismaTarihi { get; set; }
        public string SonCalismaDurumu { get; set; }
        public string SonHataMesaji { get; set; }
        public bool? Aktif { get; set; }
        public string ZamanlamaTipi { get; set; }
        public string CalismaZamanlari { get; set; }
    }
}
