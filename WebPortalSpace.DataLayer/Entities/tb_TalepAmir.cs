using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TalepAmir
    {
        public int TalepAmirID { get; set; }
        public string TalepKodu { get; set; }
        public string KayitSicil { get; set; }
        public string AmirSicil { get; set; }
        public DateTime? KayitTar { get; set; }
        public bool? Durum { get; set; }
        public DateTime? IslemTar { get; set; }
        public double? Sure { get; set; }
        public string IslemTur { get; set; }
    }
}
