using System;

namespace OyemCore.BusinessLayer.Dtos
{
    public class BakimPlanDetayDto
    {
        public int ID { get; set; }
        public string PlanKodu { get; set; }
        public string Aciklama { get; set; }
        public string DosyaUrl { get; set; }
        public string Personel { get; set; }
        public string KayitSicil { get; set; }
        public string TarihStr { get; set; }
    }
}
