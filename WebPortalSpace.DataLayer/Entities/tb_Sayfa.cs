using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Sayfa
    {
        public int SayfaID { get; set; }
        public string SayfaAdi { get; set; }
        public int? ProjeID { get; set; }
        public string SayfaUrl { get; set; }
        public short? SiraNo { get; set; }
        public string BilgiEkrani { get; set; }
        public bool? MenudeGoster { get; set; }
        public bool? Durum { get; set; }
        public string Etiket { get; set; }
    }
}
