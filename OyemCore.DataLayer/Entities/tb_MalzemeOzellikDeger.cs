using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeOzellikDeger
    {
        public int ID { get; set; }
        public int OzellikID { get; set; }
        public string Deger { get; set; }
        public string Kod { get; set; }
        public int? Sira { get; set; }
        public bool? Aktif { get; set; }
    }
}
