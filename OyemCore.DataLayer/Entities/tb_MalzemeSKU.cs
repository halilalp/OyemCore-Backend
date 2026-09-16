using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeSKU
    {
        public int ID { get; set; }
        public string ParentMalzemeKodu { get; set; }
        public string SKUKodu { get; set; }
        public string BilesenJSON { get; set; }
        public decimal FiyatFarki { get; set; }
        public bool? Aktif { get; set; }
    }
}
