namespace OyemCore.DataLayer.Entities
{
    // Anket sorusu seçeneği. referans: webportal tb_AnketSoruSecenek
    public class tb_AnketSoruSecenek
    {
        public int AnketSecenekID { get; set; }
        public int? SoruID { get; set; }
        public string Secenek { get; set; }
        public int? Puan { get; set; }
    }
}
