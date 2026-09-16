using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeLot
    {
        public int Id { get; set; }
        public string MalzemeKodu { get; set; }
        public string LotNo { get; set; }
        public DateTime? UretimTarihi { get; set; }
        public DateTime? SonKullanmaTarihi { get; set; }
        public string TedarikciKodu { get; set; }
        public string BelgeNo { get; set; }
        public string OnayDurumu { get; set; }
        public string Aciklama { get; set; }
        public DateTime KayitTarihi { get; set; }
        public string KayitSicilNo { get; set; }
        public string OnaySicilNo { get; set; }
    }
}
