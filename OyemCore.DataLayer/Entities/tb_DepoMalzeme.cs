using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_DepoMalzeme
    {
        public int ID { get; set; }
        public string DepoKodu { get; set; }
        public string MalzemeKodu { get; set; }
        public decimal Miktar { get; set; }
        public DateTime? SonIslemTarihi { get; set; }
    }
}
