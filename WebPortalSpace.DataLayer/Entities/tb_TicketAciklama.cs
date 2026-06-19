using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TicketAciklama
    {
        public int ID { get; set; }
        public int TicketID { get; set; }
        public string SicilNo { get; set; }
        public string Aciklama { get; set; }
        public DateTime? KayitTarihi { get; set; }
    }
}
