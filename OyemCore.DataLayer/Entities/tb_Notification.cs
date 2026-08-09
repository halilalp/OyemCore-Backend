using System;

namespace OyemCore.DataLayer.Entities
{
    // Uygulama-içi bildirim (zil ikonu bildirim merkezi).
    // referans: webportal WebServiceBildirim / tb_Notification
    public class tb_Notification
    {
        public int ID { get; set; }
        public string SicilNo { get; set; }
        public string Baslik { get; set; }
        public string Aciklama { get; set; }
        public string LinkUrl { get; set; }
        public string Kategori { get; set; }
        public string ReferansID { get; set; }
        public bool Okundu { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
