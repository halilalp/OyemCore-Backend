using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Koleksiyon
    {
        public string KoleksiyonKodu { get; set; }
        public string KoleksiyonAdi { get; set; }
        public string Aciklama { get; set; }
        public bool Aktif { get; set; }
    }
}
