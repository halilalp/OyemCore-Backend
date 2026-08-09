using System;

namespace OyemCore.DataLayer.Entities
{
    // Özel chat grubu. referans: webportal tb_ChatGroup
    public class tb_ChatGroup
    {
        public int ID { get; set; }
        public string GroupCode { get; set; }        // "GROUP_CUSTOM_..." vb.
        public string GroupName { get; set; }
        public string OlusturanSicilNo { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
