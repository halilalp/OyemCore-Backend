using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_TalepBakim
    {
        public string TalepKodu { get; set; }
        public string SirketKodu { get; set; }
        public string BolumKodu { get; set; }
        public string MakineKodu { get; set; }
        public string UretimDurusu { get; set; }
        public string GidaGuvOncelik { get; set; }
        public string IsGuvOncelik { get; set; }
        public double? MTTR { get; set; }
        public double? MTBF { get; set; }
        public string MttrSure { get; set; }
        public double? MttrSureDk { get; set; }
        public DateTime? MtbfTarihi { get; set; }
        public string MtbfSure { get; set; }
        public double? MtbfSureDk { get; set; }
        public string EksikSomunDurum { get; set; }
        public string YagDurum { get; set; }
        public string MiknatisDurum { get; set; }
        public string FazlaParcaDurum { get; set; }
        public string GuvRiskDurum { get; set; }
        public string MakineDurum { get; set; }
        public string TemizlikDurum { get; set; }
        public string GidaRiskDurum { get; set; }
    }
}
