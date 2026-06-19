using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TalepAyar
    {
        public int TalepAyarID { get; set; }
        public int? KategoriID { get; set; }
        public string SicilNo { get; set; }
        public bool? YoneticiMi { get; set; }
        public string SirketKodu { get; set; }
    }
}
