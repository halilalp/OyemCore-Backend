namespace OyemCore.DataLayer.Entities
{
    // Anket sorusu. Tur="SEC" → seçenekli; aksi halde serbest metin. referans: webportal tb_AnketSoru
    public class tb_AnketSoru
    {
        public int AnketSoruID { get; set; }
        public int? AnketID { get; set; }
        public string Soru { get; set; }
        public string Aciklama { get; set; }
        public string Tur { get; set; }
    }
}
