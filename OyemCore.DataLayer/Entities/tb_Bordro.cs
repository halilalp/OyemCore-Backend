using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Bordro
    {
        public int BordroID { get; set; }
        public string SicilNo { get; set; }
        public string Donem { get; set; }
        public string DosyaYolu { get; set; }
        public DateTime? OkunmaTarihi { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string Durum { get; set; }
        public DateTime? YuklemeTarihi { get; set; }
    }
}
