using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Egitim
    {
        public int EgitimID { get; set; }
        public string Konu { get; set; }
        public string DosyaUrl { get; set; }
        public DateTime? KayitTar { get; set; }
        public int? KategoriID { get; set; }
        public string KayitEposta { get; set; }
        // DB'de mevcut olmasina ragmen entity'ye eslenmemisti; bu yuzden
        // egitim aciklamalari hicbir yerde gorunmuyordu.
        public string Aciklama { get; set; }
    }
}
