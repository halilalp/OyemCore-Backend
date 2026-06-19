using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TicketKategori
    {
        public int ID { get; set; }
        public string Tanim { get; set; }
        public string SirketKodu { get; set; }
        public bool? Durum { get; set; }
    }
}
