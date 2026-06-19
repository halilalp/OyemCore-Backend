using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Ticket
    {
        public int ID { get; set; }
        public string TakipKodu { get; set; }
        public string SirketKodu { get; set; }
        public string Baslik { get; set; }
        public string Aciklama { get; set; }
        public string IslemTuru { get; set; }
        public string SurecDurumu { get; set; }
        public string Oncelik { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public DateTime? KayitTarihi { get; set; }
        public string KayitSicilNo { get; set; }
        public string SorumluSicilNo { get; set; }
        public int? KategoriID { get; set; }
        public int? Sira { get; set; }
    }
}
