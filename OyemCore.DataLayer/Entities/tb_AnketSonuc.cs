using System;

namespace OyemCore.DataLayer.Entities
{
    // Anket cevap kaydı (bir soruya bir cevap). referans: webportal tb_AnketSonuc
    public class tb_AnketSonuc
    {
        public int AnketSonucID { get; set; }
        public int? AnketID { get; set; }
        public int? SoruID { get; set; }
        public int? SecilenSecenekID { get; set; }
        public int? Puan { get; set; }
        public string Cevap { get; set; }
        public string SicilNo { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
