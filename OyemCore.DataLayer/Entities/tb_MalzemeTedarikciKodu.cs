using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeTedarikciKodu
    {
        public int ID { get; set; }
        public string MalzemeKodu { get; set; }
        public string TedarikciKodu { get; set; }
        public string TedarikciStokKodu { get; set; }
    }
}
