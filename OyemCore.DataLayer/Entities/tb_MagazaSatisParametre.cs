using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MagazaSatisParametre
    {
        public int ParametreID { get; set; }
        public string ParametreKodu { get; set; }
        public string ParametreAdi { get; set; }
        public string Aciklama { get; set; }
        public string Modul { get; set; }
        public string DegerTipi { get; set; }
        public string VarsayilanDeger { get; set; }
        public bool Aktif { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
