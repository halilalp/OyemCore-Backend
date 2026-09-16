using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MagazaSatisParametreDeger
    {
        public int DegerID { get; set; }
        public string ParametreKodu { get; set; }
        public int? MagazaID { get; set; }
        public string Deger { get; set; }
        public bool Aktif { get; set; }
        public DateTime GuncellemeTarihi { get; set; }
        public string GuncelleyenSicil { get; set; }
    }
}
