using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_TalepKategori
    {
        public int TalepKategoriID { get; set; }
        public string Tanim { get; set; }
        public int? UstKategoriID { get; set; }
        public bool Durum { get; set; }
        public string TalepTurKodu { get; set; }
        public string YetkiBelgeTur { get; set; }
    }
}
