using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Proje
    {
        public int ProjeID { get; set; }
        public string ProjeAdi { get; set; }
        public string Ikon { get; set; }
        public short? SiraNo { get; set; }
        public bool? Durum { get; set; }
        public string AnaSayfa { get; set; }
    }
}
