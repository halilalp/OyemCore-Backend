namespace OyemCore.DataLayer.Entities
{
    // Bakim/tamir kaydina eklenen dosya/resim ekleri. referans: tb_AygitBakim
    public class tb_AygitBakimDosya
    {
        public int DosyaID { get; set; }
        public int BakimID { get; set; }
        public string DosyaUrl { get; set; }
        public string DosyaAdi { get; set; }
    }
}
