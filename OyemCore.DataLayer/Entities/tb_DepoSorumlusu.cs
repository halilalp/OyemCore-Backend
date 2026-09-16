using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_DepoSorumlusu
    {
        public int ID { get; set; }
        public string SorumluSicilNo { get; set; }
        public string DepoKodu { get; set; }
        public DateTime? KayitTarihi { get; set; }
        public string KayitSicilNo { get; set; }
    }
}
