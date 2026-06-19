using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Log
    {
        public int LogID { get; set; }
        public string Eposta { get; set; }
        public string SicilNo { get; set; }
        public string Konu { get; set; }
        public string Aciklama { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
