using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeOzellikTanim
    {
        public int ID { get; set; }
        public string Tanim { get; set; }
        public string Kod { get; set; }
        public string VeriTipi { get; set; }
        public bool? Aktif { get; set; }
        public string Tipi { get; set; }
    }
}
