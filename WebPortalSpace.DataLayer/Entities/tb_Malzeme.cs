using System;

namespace WebPortalSpace.DataLayer.Entities
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
    }
}
