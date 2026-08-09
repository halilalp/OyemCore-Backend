using System;

namespace OyemCore.DataLayer.Entities
{
    // Chat grubu üyesi. referans: webportal tb_ChatGroupMember
    public class tb_ChatGroupMember
    {
        public int ID { get; set; }
        public string GroupCode { get; set; }
        public string SicilNo { get; set; }
        public DateTime KayitTarihi { get; set; }
        public DateTime? SonOkumaTarihi { get; set; }  // okunmamış hesabı için
    }
}
