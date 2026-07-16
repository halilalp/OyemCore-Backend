namespace OyemCore.DataLayer.Entities
{
    // sp_BakimTalepGetir stored procedure sonucu (Bakım HelpDesk performans raporu için).
    // EF Core keyless entity; SP'nin döndürdüğü ek kolonlar EF tarafından yok sayılır.
    public class SpBakimTalep
    {
        public string TalepKodu { get; set; }
        public bool? Durum { get; set; }
        public string SorumluSicil { get; set; }
        public string SorumluPer { get; set; }
        public double? MttrTamamSure { get; set; }
        public double? TalepPuan { get; set; }
    }
}
