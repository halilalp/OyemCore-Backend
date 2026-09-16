using System;

namespace OyemCore.DataLayer.Entities
{
    // Faz 1'de yalnizca malzeme silme guard'i (hareket gormus mu?) icin kullaniliyor.
    // Faz 2 (Stok) kapsaminda tum kolonlarla genisletilecek.
    public class tb_MalzemeHareket
    {
        public int ID { get; set; }
        public string HareketNo { get; set; }
        public string DepoKodu { get; set; }
        public string MalzemeKodu { get; set; }
        public string LotNo { get; set; }
        public string OnayDurumu { get; set; }
        public decimal Miktar { get; set; }
        public string Aciklama { get; set; }
        public DateTime? IslemTarihi { get; set; }
        public string IslemYapan { get; set; }
        public string IslemTipi { get; set; }
        public int? FisID { get; set; }
        public string FisNo { get; set; }
        public decimal? BirimMaliyet { get; set; }
        public string ParaBirimi { get; set; }
        public string TedarikciKodu { get; set; }
        public DateTime? GirisTarihi { get; set; }
    }
}
