using System;

namespace OyemCore.DataLayer.Entities
{
    // Anket başlığı. referans: webportal tb_Anket
    public class tb_Anket
    {
        public int AnketID { get; set; }
        public string Konu { get; set; }
        public string Aciklama { get; set; }
        public string KayitUser { get; set; }
        public DateTime? KayitTar { get; set; }
        public DateTime? BasTar { get; set; }
        public DateTime? BitisTar { get; set; }
        public string RefUrl { get; set; }
        public bool? Durum { get; set; }        // aktif mi
    }
}
