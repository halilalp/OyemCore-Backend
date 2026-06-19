using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TalepGelisme
    {
        public int TalepGelismeID { get; set; }
        public string TalepKodu { get; set; }
        public string Aciklama { get; set; }
        public string Sicil { get; set; }
        public string Eposta { get; set; }
        public DateTime? KayitTar { get; set; }
        public string DosyaUrl { get; set; }
    }
}
