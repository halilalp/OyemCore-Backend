using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_AkademiAtama
    {
        public int AtamaID { get; set; }
        public int AkademiEgitimID { get; set; }
        public string SicilNo { get; set; } = null!;
        public string AtayanSicil { get; set; } = null!;
        public DateTime AtamaTarihi { get; set; }
        public DateTime? SonTarih { get; set; }
        public bool ZorunluMu { get; set; }
        public bool AktifIzlemeZorunlu { get; set; }
        public bool IptalMi { get; set; }
    }
}
