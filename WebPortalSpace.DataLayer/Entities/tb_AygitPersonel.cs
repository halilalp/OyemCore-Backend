using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_AygitPersonel
    {
        public int AygitPersonelID { get; set; }
        public int AygitID { get; set; }
        public string PersonelSicil { get; set; }
        public string PersonelAdSoyad { get; set; }
        public DateTime? TeslimEtTar { get; set; }
        public string TeslimEdenSicil { get; set; }
        public string Aciklama { get; set; }
        public string TeslimAlanSicil { get; set; }
        public DateTime? TeslimAlTar { get; set; }
    }
}
