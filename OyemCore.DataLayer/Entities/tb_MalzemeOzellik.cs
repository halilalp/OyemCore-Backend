using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeOzellik
    {
        public int ID { get; set; }
        public int MalzemeID { get; set; }
        public string MalzemeKodu { get; set; }
        public int OzellikID { get; set; }
        public int? Sira { get; set; }
        public bool? ZorunluMu { get; set; }
        public string Deger { get; set; }
    }
}
