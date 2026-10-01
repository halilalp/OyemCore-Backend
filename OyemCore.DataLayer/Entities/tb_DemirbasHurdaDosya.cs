namespace OyemCore.DataLayer.Entities
{
    // Hurda talebine eklenen kanit dosyalari (en fazla 3, PDF/resim). referans: tb_DemirbasHurdaTalep
    public class tb_DemirbasHurdaDosya
    {
        public int DosyaID { get; set; }
        public int HurdaTalepID { get; set; }
        public string DosyaUrl { get; set; }
        public string DosyaAdi { get; set; }
    }
}
