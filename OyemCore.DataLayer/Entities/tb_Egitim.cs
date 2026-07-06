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
    }
}
