using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Haber
    {
        public int HaberID { get; set; }
        public string Konu { get; set; }
        public string ProfilUrl { get; set; }
        public string KayitEposta { get; set; }
        public DateTime? KayitTar { get; set; }
        public string Aciklama { get; set; }
    }
}
