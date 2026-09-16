using System;

namespace OyemCore.BusinessLayer.Dtos
{
    // Referans WebServicePlanTemizlikOnay.PlanTemizlikOnayDurumGetir ile birebir.
    public class TemizlikOnayDurumDto
    {
        public bool Exists { get; set; }
        public int OnayID { get; set; }
        public string OnayDurumu { get; set; }
        public string SecilenAdSoyad { get; set; }
        public string SecilenSicil { get; set; }
        public string KayitTarStr { get; set; }
        public string OnayTarStr { get; set; }
    }

    // Referans WebServicePlanTemizlikOnay.PlanTemizlikOnayGetir ile birebir.
    public class TemizlikOnayDetayDto
    {
        public int OnayID { get; set; }
        public string PlanTuru { get; set; }
        public string PlanKodu { get; set; }
        public string OnayDurumu { get; set; }
        public string PlanAciklama { get; set; }
        public string EksikSomunDurum { get; set; }
        public string YagDurum { get; set; }
        public string MiknatisDurum { get; set; }
        public string FazlaParcaDurum { get; set; }
        public string GuvRiskDurum { get; set; }
        public string MakineDurum { get; set; }
        public string TemizlikDurum { get; set; }
        public string GidaRiskDurum { get; set; }
        public string OnayAciklama { get; set; }
    }

    // Referans WebServicePlanTemizlikOnay.PlanTemizlikOnayBekleyenlerimGetir ile birebir.
    public class TemizlikOnayBekleyenDto
    {
        public int OnayID { get; set; }
        public string PlanTuru { get; set; }
        public string PlanKodu { get; set; }
        public string KayitTarStr { get; set; }
    }

    public class SaveTemizlikOnayRequest
    {
        public string EksikSomun { get; set; }
        public string Yag { get; set; }
        public string Miknatis { get; set; }
        public string FazlaParca { get; set; }
        public string Guvenlik { get; set; }
        public string Makine { get; set; }
        public string Temizlik { get; set; }
        public string Gida { get; set; }
        public string Aciklama { get; set; }
    }

    public class RejectTemizlikOnayRequest
    {
        public string Aciklama { get; set; }
    }
}
