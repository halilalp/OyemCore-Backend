using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_BakimPlanDetay
    {
        public int ID { get; set; }
        public string PlanKodu { get; set; }
        public string IslemNotu { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
        public string DosyaUrl { get; set; }
    }
}
