using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_TicketDosya
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public string DosyaAdi { get; set; }
        public string DosyaYolu { get; set; }
        public string DosyaTipi { get; set; }
        public DateTime? KayitTarihi { get; set; }
    }
}
