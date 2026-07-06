using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_BakimPlan
    {
        public int ID { get; set; }
        public string PlanKodu { get; set; }
        public string HatKodu { get; set; }
        public string BakimTuru { get; set; }
        public DateTime? HedefBaslangic { get; set; }
        public DateTime? HedefBitis { get; set; }
        public string Durum { get; set; }
        public DateTime? BaslamaTar { get; set; }
        public DateTime? BitisTar { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
        public string IslemSicil { get; set; }
    }
}
