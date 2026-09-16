using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_BakimPlanTemizlikOnay
    {
        public int OnayID { get; set; }
        public string PlanTuru { get; set; }
        public string PlanKodu { get; set; }
        public string SecenSicil { get; set; }
        public string SecilenSicil { get; set; }
        public string OnayDurumu { get; set; }
        public string EksikSomunDurum { get; set; }
        public string YagDurum { get; set; }
        public string MiknatisDurum { get; set; }
        public string FazlaParcaDurum { get; set; }
        public string GuvRiskDurum { get; set; }
        public string MakineDurum { get; set; }
        public string TemizlikDurum { get; set; }
        public string GidaRiskDurum { get; set; }
        public string OnayAciklama { get; set; }
        public DateTime KayitTar { get; set; }
        public DateTime? OnayTar { get; set; }
    }
}
